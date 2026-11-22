/// Response decompression that also counts the compressed bytes received.
///
/// <para>With <c>SocketsHttpHandler.AutomaticDecompression</c>, .NET decompresses the body inside the handler and
/// removes Content-Length: a handler outside it can't see the compressed bytes any more, so the reported size would be
/// the decompressed size. <see cref="CountingDecompressionHandler"/> replaces that decompression in
/// <c>createDefaultClient</c> and works like the .NET DecompressionHandler
/// (dotnet/runtime: System.Net.Http/src/System/Net/Http/SocketsHttpHandler/DecompressionHandler.cs),
/// but it reads the compressed body through a stream that counts the bytes.</para>
module internal NBomber.Http.CountingDecompression

#if NET8_0_OR_GREATER

open System
open System.IO
open System.IO.Compression
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open NBomber.Http.Constants

[<Literal>]
let private Gzip = "gzip"

[<Literal>]
let private Deflate = "deflate"

[<Literal>]
let private Brotli = "br"

/// The methods this handler can decompress. Other flags (for example, from DecompressionMethods.All) are ignored.
let supportedMethods = DecompressionMethods.GZip ||| DecompressionMethods.Deflate ||| DecompressionMethods.Brotli

/// A base for the read-only streams below that wrap (and own) an inner stream. Everything except reading is not supported.
/// All read overloads go to <see cref="ReadCore"/> and <see cref="ReadCoreAsync"/>, so the default Stream methods
/// (ReadByte, CopyTo, CopyToAsync) also use them.
[<AbstractClass>]
type ReadOnlyStream(inner: Stream) =
    inherit Stream()

    abstract ReadCore: buffer: Span<byte> -> int
    abstract ReadCoreAsync: buffer: Memory<byte> * ct: CancellationToken -> ValueTask<int>

    override this.Read(buffer: byte[], offset, count) = this.ReadCore(Span<byte>(buffer, offset, count))
    override this.Read(buffer: Span<byte>) = this.ReadCore buffer

    override this.ReadAsync(buffer: byte[], offset, count, ct) =
        this.ReadCoreAsync(Memory<byte>(buffer, offset, count), ct).AsTask()

    override this.ReadAsync(buffer: Memory<byte>, ct) = this.ReadCoreAsync(buffer, ct)

    override this.Dispose(disposing) =
        if disposing then inner.Dispose()
        base.Dispose disposing

    override this.CanRead = true
    override this.CanSeek = false
    override this.CanWrite = false
    override this.Length = raise (NotSupportedException())
    override this.Position
        with get () = raise (NotSupportedException())
        and set (_: int64) = raise (NotSupportedException())

    override this.Flush() = ()
    override this.Seek(_, _) = raise (NotSupportedException())
    override this.SetLength(_) = raise (NotSupportedException())
    override this.Write(_: byte[], _: int, _: int) = raise (NotSupportedException())

/// A read-only pass-through stream that counts the bytes read from the inner stream.
type CountingReadStream(inner: Stream, add: int -> unit) =
    inherit ReadOnlyStream(inner)

    override this.ReadCore(buffer: Span<byte>) =
        let n = inner.Read buffer
        add n
        n

    override this.ReadCoreAsync(buffer: Memory<byte>, ct) =
        let read = inner.ReadAsync(buffer, ct)
        if read.IsCompletedSuccessfully then
            let n = read.Result
            add n
            ValueTask<int>(n)
        else
            ValueTask<int>(task {
                let! n = read
                add n
                return n
            })

/// Returns <paramref name="firstByte"/> on the first read, then reads from the inner stream.
/// It puts back the byte that <see cref="ZLibOrDeflateStream"/> read to select the decoder.
type FirstByteReturnStream(inner: Stream, firstByte: byte) =
    inherit ReadOnlyStream(inner)

    let mutable working = true

    override this.ReadCore(buffer: Span<byte>) =
        if working && buffer.Length > 0 then
            buffer[0] <- firstByte
            working <- false
            1
        else
            inner.Read buffer

    override this.ReadCoreAsync(buffer: Memory<byte>, ct) =
        if working && buffer.Length > 0 then
            buffer.Span[0] <- firstByte
            working <- false
            ValueTask<int>(1)
        else
            inner.ReadAsync(buffer, ct)

/// Decodes the "deflate" content-coding. It is defined as zlib (RFC 1950) around deflate (RFC 1951),
/// but some servers send raw deflate. Like .NET, the first byte selects the decoder: zlib data always has
/// the compression method 8 in the low four bits of its first byte, raw deflate data never has it.
type ZLibOrDeflateStream(inner: Stream) =
    inherit ReadOnlyStream(inner)

    let mutable decoder: Stream = null

    /// <paramref name="firstByte"/> is -1 for an empty body. Like .NET, raw deflate then decodes it (to an empty result).
    let createDecoder (firstByte: int) : Stream =
        if firstByte = -1 then
            new DeflateStream(inner, CompressionMode.Decompress)
        else
            let source = new FirstByteReturnStream(inner, byte firstByte)
            if (firstByte &&& 0xF) = 8 then new ZLibStream(source, CompressionMode.Decompress)
            else new DeflateStream(source, CompressionMode.Decompress)

    override this.ReadCore(buffer: Span<byte>) =
        if isNull decoder then
            decoder <- createDecoder(inner.ReadByte())
        decoder.Read buffer

    override this.ReadCoreAsync(buffer: Memory<byte>, ct) =
        if not (isNull decoder) then
            decoder.ReadAsync(buffer, ct)
        else
            ValueTask<int>(task {
                let first = Array.zeroCreate<byte> 1
                let! n = inner.ReadAsync(first.AsMemory(), ct)
                decoder <- createDecoder(if n = 0 then -1 else int first[0])
                let! read = decoder.ReadAsync(buffer, ct)
                return read
            })

    override this.Dispose(disposing) =
        // the decoder disposes inner too; the base disposes it as well, for a stream that was never read
        if disposing && not (isNull decoder) then decoder.Dispose()
        base.Dispose disposing

/// A decompressed response body that counts the compressed bytes it reads. Its headers are made like .NET makes the
/// headers of a decompressed body: Content-Length is removed, and the decoded (last) encoding is removed from Content-Encoding.
/// The original content is kept (see <see cref="Original"/>), so the size calculation can count the head and the body
/// as they were received.
/// Create it with <see cref="Create"/>, which also sets the headers.
type CountingDecompressedContent private (original: HttpContent, encoding: string) =
    inherit HttpContent()

    let mutable compressedBytesRead = 0L
    let mutable consumed = false

    let add (count: int) =
        Interlocked.Add(&compressedBytesRead, int64 count) |> ignore

    let isHeader (expected: string) (name: string) =
        String.Equals(name, expected, StringComparison.OrdinalIgnoreCase)

    let decode (compressed: Stream) : Stream =
        let counting = new CountingReadStream(compressed, add)
        match encoding with
        | Gzip    -> new GZipStream(counting, CompressionMode.Decompress)
        | Deflate -> new ZLibOrDeflateStream(counting)
        | _       -> new BrotliStream(counting, CompressionMode.Decompress)

    let markConsumed () =
        if consumed then
            raise (InvalidOperationException "The stream was already consumed. It cannot be read again.")
        consumed <- true

    let openDecoded (ct: CancellationToken) =
        markConsumed()
        decode(original.ReadAsStream ct)

    let openDecodedAsync (ct: CancellationToken) =
        markConsumed()
        task {
            let! compressed = original.ReadAsStreamAsync ct
            return decode compressed
        }

    /// The compressed content as it was received. Its headers are the received ones (with Content-Length
    /// and the full Content-Encoding). Its body is read through this content only, so don't read it directly.
    member this.Original = original

    /// Copies the received headers, except Content-Length and the decoded (last) Content-Encoding value.
    member private this.CopyHeaders() =
        for h in original.Headers do
            if not (isHeader ContentLengthHeader h.Key || isHeader ContentEncodingHeader h.Key) then
                this.Headers.TryAddWithoutValidation(h.Key, h.Value) |> ignore

        let encodings = original.Headers.ContentEncoding |> Seq.toArray
        if encodings.Length > 1 then
            this.Headers.TryAddWithoutValidation(ContentEncodingHeader, encodings[.. encodings.Length - 2]) |> ignore

    /// Wraps <paramref name="original"/>, whose last Content-Encoding value is <paramref name="encoding"/>.
    static member Create(original: HttpContent, encoding: string) =
        let content = new CountingDecompressedContent(original, encoding)
        content.CopyHeaders()
        content

    /// The number of compressed bytes read from the response so far.
    member this.CompressedBytesRead = Interlocked.Read(&compressedBytesRead)

    override this.TryComputeLength(length: byref<int64>) =
        length <- 0L
        false

    override this.CreateContentReadStream(ct: CancellationToken) = openDecoded ct

    // the overload without a token isn't overridden: only the base version of this overload calls it
    override this.CreateContentReadStreamAsync(ct: CancellationToken) =
        openDecodedAsync ct

    override this.SerializeToStream(stream, _context, ct) =
        use decoded = openDecoded ct
        decoded.CopyTo(stream)

    override this.SerializeToStreamAsync(stream, _context) =
        let copying = openDecodedAsync CancellationToken.None
        task {
            use! decoded = copying
            do! decoded.CopyToAsync(stream)
        }

    override this.SerializeToStreamAsync(stream, _context, ct) =
        let copying = openDecodedAsync ct
        task {
            use! decoded = copying
            do! decoded.CopyToAsync(stream, ct)
        }

    override this.Dispose(disposing) =
        if disposing then
            original.Dispose()

        base.Dispose(disposing)

/// Decompresses response bodies like <c>SocketsHttpHandler.AutomaticDecompression</c>, but counts the compressed bytes
/// (see <see cref="CountingDecompressedContent"/>). The inner handler must not decompress itself.
type CountingDecompressionHandler(innerHandler: HttpMessageHandler, methods: DecompressionMethods) =
    inherit DelegatingHandler(innerHandler)

    let gzipEnabled = methods.HasFlag DecompressionMethods.GZip
    let deflateEnabled = methods.HasFlag DecompressionMethods.Deflate
    let brotliEnabled = methods.HasFlag DecompressionMethods.Brotli

    // the same values in the same order as .NET sends them
    let enabledEncodings = [|
        if gzipEnabled then Gzip
        if deflateEnabled then Deflate
        if brotliEnabled then Brotli
    |]

    // created once and shared by all requests, like .NET does (the values are immutable)
    let acceptEncodingValues = enabledEncodings |> Array.map(fun e -> Headers.StringWithQualityHeaderValue e)

    let isEncoding (expected: string) (value: string) =
        String.Equals(value, expected, StringComparison.OrdinalIgnoreCase)

    /// Adds each enabled encoding that the request doesn't accept yet, like .NET does. Only the name is compared:
    /// a user value with a quality (for example, "gzip;q=0.5") is kept as it is, and "gzip" isn't added again.
    member this.PrepareRequest(request: HttpRequestMessage) =
        let acceptEncoding = request.Headers.AcceptEncoding
        for value in acceptEncodingValues do
            if not (acceptEncoding |> Seq.exists(fun v -> isEncoding value.Value v.Value)) then
                acceptEncoding.Add(value)

    member this.ProcessResponse(response: HttpResponseMessage) =
        let content = response.Content
        if not (isNull content) then
            let encodings = content.Headers.ContentEncoding
            if encodings.Count > 0 then
                let last = Seq.last encodings

                // only the last encoding is decoded, like .NET does
                let decoded =
                    if gzipEnabled && isEncoding Gzip last then Gzip
                    elif deflateEnabled && isEncoding Deflate last then Deflate
                    elif brotliEnabled && isEncoding Brotli last then Brotli
                    else null

                if not (isNull decoded) then
                    response.Content <- CountingDecompressedContent.Create(content, decoded)
        response

    override this.SendAsync(request, cancellationToken) =
        this.PrepareRequest(request)
        // base can't be used inside the task closure
        let sending = base.SendAsync(request, cancellationToken)
        task {
            let! response = sending
            return this.ProcessResponse(response)
        }

    override this.Send(request, cancellationToken) =
        this.PrepareRequest(request)
        this.ProcessResponse(base.Send(request, cancellationToken))

#endif
