module internal NBomber.Http.HttpImpl

open System
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

let createSocketsHandler (maxConnectionsPerServer: int) (pooledConnectionLifetime: TimeSpan) (dnsSelection: DnsSelection) =
    let socketsHandler = new SocketsHttpHandler(
        PooledConnectionLifetime = pooledConnectionLifetime,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes 1.5,
        MaxConnectionsPerServer = maxConnectionsPerServer,
        EnableMultipleHttp2Connections = true
    )

    match dnsSelection with
    | DnsSelection.Random
    | DnsSelection.RoundRobin ->
        let connector = DnsConnector(dnsSelection)
        socketsHandler.ConnectCallback <- Func<_,_,_>(fun ctx ct -> connector.ConnectCallback(ctx, ct))
    | _ -> ()

    socketsHandler
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

/// Returns the body length. The result is ValueNone if the length is unknown:
/// - a request body sent with "Transfer-Encoding: chunked" (for example, JsonContent or a non-seekable StreamContent);
/// - a response body without Content-Length that is not read yet (HttpCompletionOption.ResponseHeadersRead).
/// The ContentLength getter calculates a missing length for buffered content and adds a Content-Length header to the content headers.
/// That header was not sent (the body was sent chunked), so this function removes it again: the headers stay as they were received.
let tryGetBodyLength (content: HttpContent) =
    if isNull content then ValueNone
    else
        let headers = content.Headers
        let headerWasSent = headers.Contains ContentLengthHeader
        let length = headers.ContentLength

        if length.HasValue && not headerWasSent then
            headers.Remove ContentLengthHeader |> ignore

        if length.HasValue then ValueSome length.Value
        else ValueNone

/// Responses to HEAD requests and 1xx, 204 and 304 responses have no body, even if they have a Content-Length header.
let private responseCanHaveBody (response: HttpResponseMessage) =
    let status = int response.StatusCode
    let isHeadRequest = not (isNull response.RequestMessage) && response.RequestMessage.Method = HttpMethod.Head
    status >= 200 && status <> 204 && status <> 304 && not isHeadRequest

/// Calculates the request size as HTTP/1.1 sends it. Call it after the request is sent:
/// HttpClient and the handler add headers (DefaultRequestHeaders, cookies, Content-Length, Transfer-Encoding) when they send the request.
/// The chunked framing and a body with unknown length are not counted.
let calcRequestSize (request: HttpRequestMessage) =

    let bodySize =
        match tryGetBodyLength request.Content with
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

/// Calculates the response size as HTTP/1.1 sends it.
/// The chunked framing is not counted. With HttpCompletionOption.ResponseHeadersRead,
/// a body without a Content-Length header is not read yet, so its size is not counted.
let calcResponseSize (response: HttpResponseMessage) =

    // calculated before the content headers, because tryGetBodyLength removes a Content-Length header that the getter added
    let bodySize =
        if responseCanHaveBody response then
            match tryGetBodyLength response.Content with
            | ValueSome length -> length
            | ValueNone -> 0L
        else 0L

    // status line: "HTTP/1.1 404 Not Found\r\n"
    let reasonPhraseSize =
        if isNull response.ReasonPhrase then 0
        else Encoding.UTF8.GetByteCount response.ReasonPhrase

    let statusLineSize =
        HttpVersionHeaderLength + SpaceLength + StatusCodeLength + SpaceLength + reasonPhraseSize + CrlfLength

    let contentHeadersSize =
        if isNull response.Content then 0
        else headersSize response.Content.Headers

    // the empty line after the headers ("\r\n") is always sent, also without a body
    let headSize = statusLineSize + headersSize response.Headers + contentHeadersSize + CrlfLength

    int64 headSize + bodySize

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
