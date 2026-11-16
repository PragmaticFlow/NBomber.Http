module internal NBomber.Http.HttpImpl

open System
open System.Net.Http
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
    if (isNull request.RequestUri) then ""
    else request.RequestUri.Authority

let calcRequestSize (request: HttpRequestMessage) =

    // The body needs to be calculated first — for some reason, Content-Length only becomes available after request.Content is initialized.
    let bodySize =
        if not (isNull request.Content) && request.Content.Headers.ContentLength.HasValue then
            request.Content.Headers.ContentLength.Value + int64 CrlfLength
        else 0

    let headersSize =
        request.Headers
        |> Seq.sumBy(fun h ->
            Encoding.UTF8.GetByteCount(h.Key) + HeaderSeparatorLength
            + (h.Value |> Seq.sumBy Encoding.UTF8.GetByteCount) + CrlfLength
        )

    let contentHeaderSize =
        if not (isNull request.Content) then
            request.Content.Headers
            |> Seq.sumBy(fun h ->
                Encoding.UTF8.GetByteCount(h.Key) + HeaderSeparatorLength
                + (h.Value |> Seq.sumBy Encoding.UTF8.GetByteCount) + CrlfLength
            )
        else 0

    let methodSize  = Encoding.UTF8.GetByteCount(request.Method.Method) + SpaceLength
    let urlSize     = Encoding.UTF8.GetByteCount(request.RequestUri.OriginalString) + SpaceLength
    let httpVersionSize = HttpVersionHeaderLength + CrlfLength

    let hostHeaderSize =
        HostHeaderLength + HeaderSeparatorLength
        + (request |> getHostName |> Encoding.UTF8.GetByteCount)
        + CrlfLength

    let allHeadersSize = methodSize + urlSize + httpVersionSize + hostHeaderSize + headersSize + contentHeaderSize

    int64 allHeadersSize + bodySize

let calcResponseSize (response: HttpResponseMessage) =

    // The body needs to be calculated first — for some reason, Content-Length only becomes available after response.Content is initialized.
    let bodySize =
        if not (isNull response.Content) && response.Content.Headers.ContentLength.HasValue then
            response.Content.Headers.ContentLength.Value + int64 CrlfLength
        else 0

    let headersSize =
        response.Headers
        |> Seq.sumBy(fun h ->
            Encoding.UTF8.GetByteCount(h.Key) + HeaderSeparatorLength
            + (h.Value |> Seq.sumBy Encoding.UTF8.GetByteCount) + CrlfLength
        )

    let contentHeaderSize =
        if not (isNull response.Content) then
            response.Content.Headers
            |> Seq.sumBy(fun h ->
                Encoding.UTF8.GetByteCount(h.Key) + HeaderSeparatorLength
                + (h.Value |> Seq.sumBy Encoding.UTF8.GetByteCount) + CrlfLength
            )
        else 0

    let httpVersionSize = HttpVersionHeaderLength + SpaceLength
    let statusCodeSize = StatusCodeLength + SpaceLength + Encoding.UTF8.GetByteCount(response.StatusCode.ToString()) + CrlfLength

    let allHeadersSize = httpVersionSize + statusCodeSize + headersSize + contentHeaderSize

    int64 allHeadersSize + bodySize

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
