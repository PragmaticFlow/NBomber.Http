module internal NBomber.Http.HttpImpl

open System
open System.IO
open System.IO.Compression
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open NBomber.Http.Constants

#if NET8_0_OR_GREATER
/// Sets the HTTP version and version policy on every request, because <see cref="HttpClient.DefaultRequestVersion"/>
/// is not used for requests sent with <c>SendAsync(HttpRequestMessage)</c>.
/// Requests with a version set by <c>withVersion</c> are not changed: the request's version has priority over the client's version.
type internal HttpVersionHandler(innerHandler: HttpMessageHandler, version: Version, versionPolicy: HttpVersionPolicy) =
    inherit DelegatingHandler(innerHandler)

    /// Marks a request whose version was set with <c>withVersion</c>.
    static member val ExplicitVersionKey = HttpRequestOptionsKey<bool>("NBomber.Http.ExplicitVersion")

    member this.SetVersion(request: HttpRequestMessage) =
        match request.Options.TryGetValue HttpVersionHandler.ExplicitVersionKey with
        | true, true -> ()
        | _ ->
            request.Version <- version
            request.VersionPolicy <- versionPolicy

    override this.SendAsync(request, cancellationToken) =
        this.SetVersion(request)
        base.SendAsync(request, cancellationToken)

    override this.Send(request, cancellationToken) =
        this.SetVersion(request)
        base.Send(request, cancellationToken)
#endif

let mutable globalJsonSerializerOptions = JsonSerializerOptions.Web

/// Default client arguments for send/sendTyped. It is safe to share because HttpClientArgs is immutable.
let defaultClientArgs = HttpClientArgs.create(CancellationToken.None)

#if NET8_0_OR_GREATER
let defaultPooledConnectionLifetime = TimeSpan.FromMinutes 3.0
let defaultRequestTimeout = TimeSpan.FromMinutes 1.0

let createSocketsHandler (maxConnectionsPerServer: int) (pooledConnectionLifetime: TimeSpan) (dnsSelection: DnsSelection) =
    let socketsHandler = new SocketsHttpHandler(
        PooledConnectionLifetime = pooledConnectionLifetime,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes 1.5,
        MaxConnectionsPerServer = maxConnectionsPerServer,
        EnableMultipleHttp2Connections = true,
        // decompression is done by CountingDecompressionHandler (see withDecompression),
        // because the compressed bytes can't be counted once this handler has decompressed them
        AutomaticDecompression = DecompressionMethods.None
    )

    match dnsSelection with
    | DnsSelection.Random
    | DnsSelection.RoundRobin ->
        let connector = DnsConnector(dnsSelection)
        socketsHandler.ConnectCallback <- Func<_,_,_>(fun ctx ct -> connector.ConnectCallback(ctx, ct))
    | _ -> ()

    socketsHandler

/// Puts a <see cref="CountingDecompression.CountingDecompressionHandler"/> in front of the handler when
/// <paramref name="methods"/> enables a supported method (gzip or br). Otherwise returns the handler unchanged.
let withDecompression (methods: DecompressionMethods) (inner: HttpMessageHandler) : HttpMessageHandler =
    let methods = methods &&& CountingDecompression.supportedMethods
    if methods = DecompressionMethods.None then inner
    else new CountingDecompression.CountingDecompressionHandler(inner, methods)
#endif

let getHostName (request: HttpRequestMessage) =
    if isNull request.RequestUri then ""
    else request.RequestUri.Authority

/// The request target in the request line. Without a proxy, HTTP/1.1 sends only the path and query ("/users?id=1"),
/// not the absolute URI: the host is sent in the Host header.
let getRequestTarget (request: HttpRequestMessage) =
    let uri = request.RequestUri
    if isNull uri then ""
    elif uri.IsAbsoluteUri then uri.PathAndQuery
    else uri.OriginalString

/// Size of one header as HTTP/1.1 sends it: "Name: value1, value2\r\n".
let private headerSize (name: string) (values: seq<string>) =
    let lineSize = Encoding.UTF8.GetByteCount(name) + HeaderSeparatorLength + CrlfLength

    // Set-Cookie can't be joined with ", " (a cookie can contain a comma), so each value is a separate line
    if String.Equals(name, "Set-Cookie", StringComparison.OrdinalIgnoreCase) then
        values |> Seq.sumBy(fun v -> lineSize + Encoding.UTF8.GetByteCount v)
    else
        // User-Agent and Server values (products) are separated by a space, other values by ", "
        let separatorSize =
            if String.Equals(name, "User-Agent", StringComparison.OrdinalIgnoreCase)
               || String.Equals(name, "Server", StringComparison.OrdinalIgnoreCase) then SpaceLength
            else HeaderValueSeparatorLength

        let mutable size = lineSize
        let mutable count = 0
        for v in values do
            size <- size + Encoding.UTF8.GetByteCount v
            count <- count + 1

        size + (max 0 (count - 1)) * separatorSize

let private headersSize (headers: HttpHeaders) =
    let mutable size = 0
    for h in headers do
        size <- size + headerSize h.Key h.Value
    size

/// Returns the body length from the Content-Length header that was really sent, or ValueNone if there is none:
/// - a request body sent with an unknown length (for example, JsonContent or a non-seekable StreamContent):
///   HTTP/1.1 sends it with "Transfer-Encoding: chunked", HTTP/2 and HTTP/3 send it as DATA frames without Content-Length;
/// - a response body sent without Content-Length (chunked on HTTP/1.1), or one whose Content-Length .NET removed after decompressing it.
///
/// <para>The header is read with TryGetValues instead of the HttpContentHeaders.ContentLength property on purpose.
/// That property calculates a missing length for buffered content and ADDS a Content-Length header to the collection,
/// which changes the headers the caller gets back and makes the calculated size depend on the order of the calls.
/// Reading the header keeps this function free of side effects; a missing length is resolved by the caller
/// (see <see cref="tryGetResponseBodyLength"/> for a buffered response, and CountingContent for a request body).</para>
let tryGetBodyLength (content: HttpContent) =
    if isNull content then ValueNone
    else
        match content.Headers.TryGetValues ContentLengthHeader with
        | true, values ->
            match Seq.tryHead values with
            | Some value ->
                match Int64.TryParse value with
                | true, length -> ValueSome length
                | _ -> ValueNone
            | None -> ValueNone
        | _ -> ValueNone

/// A write-only pass-through stream that counts the bytes written. It never disposes the inner stream:
/// the transport stream belongs to the handler.
type private CountingWriteStream(inner: Stream, add: int -> unit) =
    inherit Stream()

    override this.CanRead = false
    override this.CanSeek = false
    override this.CanWrite = true
    override this.Length = raise (NotSupportedException())
    override this.Position
        with get () = raise (NotSupportedException())
        and set (_: int64) = raise (NotSupportedException())

    override this.Flush() = inner.Flush()
    override this.FlushAsync(ct) = inner.FlushAsync ct
    override this.Read(_, _, _) = raise (NotSupportedException())
    override this.Seek(_, _) = raise (NotSupportedException())
    override this.SetLength(_) = raise (NotSupportedException())

    override this.Write(buffer, offset, count) =
        add count
        inner.Write(buffer, offset, count)

    override this.Write(buffer: ReadOnlySpan<byte>) =
        add buffer.Length
        inner.Write buffer

    override this.WriteAsync(buffer: byte[], offset, count, ct) =
        add count
        inner.WriteAsync(buffer, offset, count, ct)

    override this.WriteAsync(buffer: ReadOnlyMemory<byte>, ct) =
        add buffer.Length
        inner.WriteAsync(buffer, ct)

/// Wraps a request body whose length the handler can't know upfront, so the bytes actually written can be counted.
/// Such a body is sent without a Content-Length header: with "Transfer-Encoding: chunked" on HTTP/1.1,
/// and as DATA frames that end with the END_STREAM flag on HTTP/2 and HTTP/3.
/// <see cref="TryComputeLength"/> keeps reporting an unknown length, so the body is still sent the same way:
/// reporting a length here would make the handler send Content-Length instead,
/// which would change the request being measured.
type internal CountingContent(inner: HttpContent) =
    inherit HttpContent()

    let mutable written = 0L
    let add (count: int) = Interlocked.Add(&written, int64 count) |> ignore

    /// The payload bytes written to the transport. The framing (HTTP/1.1 chunks, HTTP/2 or HTTP/3 frames) isn't included:
    /// the content writes the payload and the handler adds the framing around it.
    member this.BytesWritten = Interlocked.Read(&written)

    /// Copies the content headers, so the wrapper is measured and sent exactly like the inner content.
    member this.CopyHeadersFrom(source: HttpContent) =
        for h in source.Headers do
            this.Headers.TryAddWithoutValidation(h.Key, h.Value) |> ignore

    override this.TryComputeLength(length: byref<int64>) =
        length <- 0L
        false

    override this.SerializeToStreamAsync(stream, context) =
        inner.CopyToAsync(new CountingWriteStream(stream, add), context)

#if NET8_0_OR_GREATER
    override this.SerializeToStreamAsync(stream, context, cancellationToken) =
        inner.CopyToAsync(new CountingWriteStream(stream, add), context, cancellationToken)
#endif

    override this.Dispose(disposing) =
        if disposing then inner.Dispose()
        base.Dispose(disposing)

/// Wraps a request body that is sent with an unknown length, so <see cref="calcRequestSize"/> can count it.
/// Returns the original content if the body was wrapped, so it can be put back after the size is calculated.
/// Bodies with a known length are left alone: their size comes from the Content-Length header.
let tryTrackRequestBody (request: HttpRequestMessage) =
    let content = request.Content

    // the ContentLength property is used here, and not TryGetValues like in tryGetBodyLength, because only this
    // property answers the question the handler itself asks: can a length be calculated for this body?
    // Its side effect (adding the header to a body that can calculate one) is harmless before the send:
    // the handler adds the same header when it sends such a body, so the request on the wire does not change.
    if isNull content || content.Headers.ContentLength.HasValue then ValueNone
    else
        let counting = new CountingContent(content)
        counting.CopyHeadersFrom(content)
        request.Content <- counting
        ValueSome content

/// Puts the original content back after the request size is calculated.
let restoreRequestBody (request: HttpRequestMessage) (original: HttpContent voption) =
    match original with
    | ValueSome content -> request.Content <- content
    | ValueNone -> ()

/// The Content-Encoding token of the compression. It also rejects an unknown enum value before the body is read.
let private encodingToken (compression: RequestCompression) =
    match compression with
    | RequestCompression.GZip    -> "gzip"
    | RequestCompression.Brotli  -> "br"
    | _ -> raise (ArgumentOutOfRangeException(nameof compression, compression, "Unsupported request compression."))

/// Writes the content to the compressor and disposes the compressor, which writes the end of the compressed data.
/// The builder functions are synchronous, so the copy is waited for. For in-memory content (StringContent,
/// ByteArrayContent) the copy completes synchronously. The sync CopyTo is not used: it throws for a custom
/// HttpContent that does not override SerializeToStream.
let private writeCompressed (content: HttpContent) (compressor: Stream) =
    try content.CopyToAsync(compressor).GetAwaiter().GetResult()
    finally compressor.Dispose()

let private compressBytes (compression: RequestCompression) (content: HttpContent) =
    use output = new MemoryStream()

    match compression with
    | RequestCompression.GZip ->
        writeCompressed content (new GZipStream(output, CompressionLevel.Optimal, leaveOpen = true))

    | RequestCompression.Brotli ->
        writeCompressed content (new BrotliStream(output, CompressionLevel.Optimal, leaveOpen = true))

    | _ -> raise (ArgumentOutOfRangeException(nameof compression, compression, "Unsupported request compression."))

    output.ToArray()

/// Replaces the request body with a compressed copy and adds the Content-Encoding header.
/// The body is compressed once, so it has a known length: it is sent with an exact Content-Length (not chunked),
/// and <see cref="calcRequestSize"/> counts the compressed bytes. Does nothing if there is no body or the compression is None.
let compressRequestBody (compression: RequestCompression) (request: HttpRequestMessage) =
    let content = request.Content

    if not (isNull content) && compression <> RequestCompression.None then
        let token = encodingToken compression
        let compressed = new ByteArrayContent(compressBytes compression content)

        // Content-Length is not copied: ByteArrayContent sets the length of the compressed bytes
        for h in content.Headers do
            if not (String.Equals(h.Key, ContentLengthHeader, StringComparison.OrdinalIgnoreCase)) then
                compressed.Headers.TryAddWithoutValidation(h.Key, h.Value) |> ignore

        // appended after an existing encoding: the encodings are listed in the order they were applied
        compressed.Headers.ContentEncoding.Add(token)

        request.Content <- compressed
        content.Dispose()

/// Responses to HEAD requests and 1xx, 204 and 304 responses have no body, even if they have a Content-Length header.
let private responseCanHaveBody (response: HttpResponseMessage) =
    let status = int response.StatusCode
    let isHeadRequest = not (isNull response.RequestMessage) && response.RequestMessage.Method = HttpMethod.Head
    status >= 200 && status <> 204 && status <> 304 && not isHeadRequest

/// Calculates the request size as HTTP/1.1 sends it. Call it after the request is sent:
/// HttpClient and the handler add headers (DefaultRequestHeaders, cookies, Content-Length, Transfer-Encoding) when they send the request.
/// The body framing (HTTP/1.1 chunks, HTTP/2 or HTTP/3 frames) is not counted. A body sent with an unknown length is counted only if
/// <see cref="tryTrackRequestBody"/> wrapped it before the request was sent.
let calcRequestSize (request: HttpRequestMessage) =

    let bodySize =
        match request.Content with
        // the length wasn't known upfront, so the body was counted while it was written
        | :? CountingContent as counting -> counting.BytesWritten
        | content ->
            match tryGetBodyLength content with
            | ValueSome length -> length
            | ValueNone -> 0L

    // request line: "GET /users?id=1 HTTP/1.1\r\n"
    let requestLineSize =
        Encoding.UTF8.GetByteCount(request.Method.Method) + SpaceLength
        + Encoding.UTF8.GetByteCount(getRequestTarget request) + SpaceLength
        + HttpVersionHeaderLength + CrlfLength

    // the handler sends a Host header created from the RequestUri only if the user did not set request.Headers.Host
    let hostHeaderSize =
        if isNull request.Headers.Host then headerSize "Host" [ getHostName request ]
        else 0 // the user's Host header is counted in request.Headers

    let contentHeadersSize =
        if isNull request.Content then 0
        else headersSize request.Content.Headers

    // the empty line after the headers ("\r\n") is always sent, also without a body
    let headSize = requestLineSize + hostHeaderSize + headersSize request.Headers + contentHeadersSize + CrlfLength

    int64 headSize + bodySize

/// The content headers as they were received. A body decompressed by CountingDecompressionHandler has lost
/// Content-Length and the decoded Content-Encoding value, so the headers of the original content are used.
let private receivedContentHeaders (content: HttpContent) =
    match content with
#if NET8_0_OR_GREATER
    | :? CountingDecompression.CountingDecompressedContent as c -> c.Original.Headers
#endif
    | _ -> content.Headers

/// The length of a buffered body, read from its content stream. Call it only for a buffered body:
/// reading the stream of an unbuffered body consumes it.
let private tryGetBufferedLength (content: HttpContent) = backgroundTask {
    try
        let! stream = content.ReadAsStreamAsync()
        return if stream.CanSeek then ValueSome stream.Length else ValueNone
    with _ ->
        return ValueNone
}

/// The status line and headers of the response, as HTTP/1.1 sends them.
let private calcResponseHeadSize (response: HttpResponseMessage) =

    // status line: "HTTP/1.1 404 Not Found\r\n"
    let reasonPhraseSize =
        if isNull response.ReasonPhrase then 0
        else Encoding.UTF8.GetByteCount response.ReasonPhrase

    let statusLineSize =
        HttpVersionHeaderLength + SpaceLength + StatusCodeLength + SpaceLength + reasonPhraseSize + CrlfLength

    let contentHeadersSize =
        if isNull response.Content then 0
        else headersSize (receivedContentHeaders response.Content)

    // the empty line after the headers ("\r\n") is always sent, also without a body
    statusLineSize + headersSize response.Headers + contentHeadersSize + CrlfLength

/// Resolves the response body length. The result is ValueNone when the length can't be known yet.
///
/// <para><paramref name="buffered"/> must be true only when the whole body is already in memory
/// (<see cref="HttpCompletionOption.ResponseContentRead"/>). Only then may the length be taken from the
/// content stream: reading the stream of an unbuffered response consumes it, and the caller could no
/// longer read the body.</para>
///
/// <para>A body decompressed by CountingDecompressionHandler (createDefaultClient with automaticDecompression)
/// is counted with its COMPRESSED length, the bytes that were received: the original Content-Length if there was one
/// (known also for an unbuffered response), otherwise the compressed bytes read while the body was buffered.</para>
///
/// <para>A body decompressed by .NET itself (an HttpClient created by the user with AutomaticDecompression) has no
/// Content-Length any more, and its ContentLength stays null even after the body is read. The buffered stream is then
/// the only source, and the length is the DECOMPRESSED size.</para>
let tryGetResponseBodyLength (response: HttpResponseMessage) (buffered: bool) = backgroundTask {
    if not (responseCanHaveBody response) then
        return ValueSome 0L
    else
        match response.Content with
#if NET8_0_OR_GREATER
        | :? CountingDecompression.CountingDecompressedContent as c ->
            match tryGetBodyLength c.Original with
            | ValueSome length -> return ValueSome length
            | ValueNone ->
                // HttpClient buffered the body before SendAsync returned, so every compressed byte was counted
                return if buffered then ValueSome c.CompressedBytesRead else ValueNone
#endif
        | content ->
            match tryGetBodyLength content with
            | ValueSome length -> return ValueSome length
            | ValueNone ->
                if not buffered then return ValueNone
                else return! tryGetBufferedLength content
}

/// Returns true when the response body that the caller reads is empty, so there is nothing to deserialize.
/// <paramref name="bodySize"/> is the result of <see cref="tryGetResponseBodyLength"/>.
///
/// <para>For a body decompressed by CountingDecompressionHandler, the size is the compressed size, which is not 0 even
/// for an empty body (an empty gzip body has about 20 bytes). The decompressed length is then checked instead,
/// which is possible only for a buffered body.</para>
let isResponseBodyEmpty (response: HttpResponseMessage) (buffered: bool) (bodySize: int64 voption) = backgroundTask {
    match response.Content with
#if NET8_0_OR_GREATER
    | :? CountingDecompression.CountingDecompressedContent as c when responseCanHaveBody response ->
        if not buffered then return false
        else
            let! length = tryGetBufferedLength c
            return length = ValueSome 0L
#endif
    | _ ->
        return bodySize = ValueSome 0L
}

/// Calculates the response size from an already resolved body length (see <see cref="tryGetResponseBodyLength"/>).
let calcResponseSizeWithBody (bodySize: int64) (response: HttpResponseMessage) =
    int64 (calcResponseHeadSize response) + bodySize

/// Calculates the response size as HTTP/1.1 sends it: resolves the body length and adds the head.
/// This is the same composition the send functions use, so there is one code path for the calculation.
/// <paramref name="buffered"/> has the meaning it has in <see cref="tryGetResponseBodyLength"/>.
let calcResponseSize (buffered: bool) (response: HttpResponseMessage) = backgroundTask {
    let! bodySize = tryGetResponseBodyLength response buffered
    return calcResponseSizeWithBody (ValueOption.defaultValue 0L bodySize) response
}

let tryLogRequest (clientArgs: HttpClientArgs, request: HttpRequestMessage) = backgroundTask {
    try
        match clientArgs.Logger with
        | Some logger ->
            let headers = String.Join(", ", request.Headers |> Seq.map(fun x -> $"""{x.Key}: {String.Join(", ", x.Value)}"""))

            let! content =
                if isNull request.Content then Task.FromResult ""
                else request.Content.ReadAsStringAsync()

            logger.Debug("HTTP Request:\n TraceId: {TraceId}\n Method: {Method}\n RequestUri: {RequestUri}\n HttpVersion: {HttpVersion}\n Headers: {Headers}\n Content: {Content}\n",
                         clientArgs.TraceId, request.Method, request.RequestUri, request.Version, headers, content)
        | None -> ()
    with
    | ex -> clientArgs.Logger |> Option.iter(_.Fatal(ex.ToString()))
}

let tryLogResponse (clientArgs: HttpClientArgs, response: HttpResponseMessage) = backgroundTask {
    try
        match clientArgs.Logger with
        | Some logger ->
            let headers = String.Join(", ", response.Headers |> Seq.map(fun x -> $"""{x.Key}: {String.Join(", ", x.Value)}"""))

            let! content =
                if isNull response.Content then Task.FromResult ""
                else response.Content.ReadAsStringAsync()

            logger.Debug("HTTP Response:\n TraceId: {TraceId}\n HttpVersion: {HttpVersion}\n StatusCode: {StatusCode}\n ReasonPhrase: {ReasonPhrase}\n Headers: {Headers}\n Content: {Content}\n",
                         clientArgs.TraceId, response.Version, response.StatusCode, response.ReasonPhrase, headers, content)
        | None -> ()
    with
    | ex -> clientArgs.Logger |> Option.iter(_.Fatal(ex.ToString()))
}
