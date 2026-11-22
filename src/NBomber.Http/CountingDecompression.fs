/// Response decompression that also counts the compressed bytes received.
///
/// <para>With <c>SocketsHttpHandler.AutomaticDecompression</c>, .NET decompresses the body inside the handler and
/// removes Content-Length: a handler outside it can't see the compressed bytes any more, so the reported size would be
/// the decompressed size. <see cref="CountingDecompressionHandler"/> replaces that decompression in
/// <c>createDefaultClient</c> and works like the .NET DecompressionHandler
/// (dotnet/runtime: System.Net.Http/src/System/Net/Http/SocketsHttpHandler/DecompressionHandler.cs),
/// but it reads the compressed body through a stream that counts the bytes.</para>
///
/// <para>Unlike .NET, only gzip and br are decoded: a "deflate" body is returned as it was received.</para>
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
let private Brotli = "br"

/// The methods this handler can decompress. Other flags (Deflate, or the extra flags of DecompressionMethods.All) are ignored.
let supportedMethods = DecompressionMethods.GZip ||| DecompressionMethods.Brotli

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
        | Gzip -> new GZipStream(counting, CompressionMode.Decompress)
        | _    -> new BrotliStream(counting, CompressionMode.Decompress)

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
    let brotliEnabled = methods.HasFlag DecompressionMethods.Brotli

    // in the same order as .NET sends them (.NET sends "deflate" between them, this handler doesn't decode it)
    let enabledEncodings = [|
        if gzipEnabled then Gzip
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
