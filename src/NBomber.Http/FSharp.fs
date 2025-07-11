namespace NBomber.Http

open System
open System.Net.Http
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open Serilog

[<Struct; IsReadOnly>]
type HttpResponse<'T> = {
    Data: 'T
    Response: HttpResponseMessage
}

type HttpClientArgs = {
    mutable HttpCompletion: HttpCompletionOption
    mutable CancellationToken: CancellationToken
    mutable JsonSerializerOptions: JsonSerializerOptions option
    mutable Logger: ILogger option
    mutable TraceId: string
}
with
    [<CompiledName("Create")>]
    static member create(cancellationToken: CancellationToken,
                         [<Optional;DefaultParameterValue(HttpCompletionOption.ResponseContentRead)>] httpCompletion,
                         [<Optional;DefaultParameterValue(null:JsonSerializerOptions)>] jsonOptions: JsonSerializerOptions,
                         [<Optional;DefaultParameterValue(null:ILogger)>] logger: ILogger) = {

        HttpCompletion = httpCompletion
        CancellationToken = cancellationToken
        JsonSerializerOptions = jsonOptions |> Option.ofObj
        Logger = logger |> Option.ofObj
        TraceId = if isNull logger then "" else Guid.NewGuid().ToString("N")
    }

    [<CompiledName("Create")>]
    static member create([<Optional;DefaultParameterValue(HttpCompletionOption.ResponseContentRead)>] httpCompletion,
                         [<Optional;DefaultParameterValue(null:JsonSerializerOptions)>] jsonOptions: JsonSerializerOptions,
                         [<Optional;DefaultParameterValue(null:ILogger)>] logger: ILogger) = {

        HttpCompletion = httpCompletion
        CancellationToken = CancellationToken.None
        JsonSerializerOptions = jsonOptions |> Option.ofObj
        Logger = logger |> Option.ofObj
        TraceId = if isNull logger then "" else Guid.NewGuid().ToString("N")
    }

namespace NBomber.Http.FSharp

open System
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open NBomber.FSharp
open NBomber.Http
open NBomber.Http.Constants

/// Provides helper functions for working with HTTP client.
module Http =

    /// Gets or sets the global JSON serializer options used by HTTP client
    let mutable GlobalJsonSerializerOptions = JsonSerializerOptions.Web

    let private getHostName (request: HttpRequestMessage) =
        if (isNull request.RequestUri) then ""
        else request.RequestUri.Authority

    let private calcRequestSize (request: HttpRequestMessage) =

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

    let private calcResponseSize (response: HttpResponseMessage) =

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

    let private tryLogRequest (clientArgs: HttpClientArgs, request: HttpRequestMessage) = backgroundTask {
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

    let private tryLogResponse (clientArgs: HttpClientArgs, response: HttpResponseMessage) = backgroundTask {
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

    /// <summary>
    /// Creates a new instance of <see cref="HttpClient"/> with configured <see cref="SocketsHttpHandler"/>.
    /// </summary>
    /// <returns>A default-configured <see cref="HttpClient"/> instance.</returns>
    /// <remarks>
    /// The internal <see cref="SocketsHttpHandler"/> is configured with:
    /// - <c>PooledConnectionLifetime</c>: 10 minutes
    /// - <c>PooledConnectionIdleTimeout</c>: 5 minutes
    /// - <c>MaxConnectionsPerServer</c>: int.MaxValue
    /// </remarks>
    let createDefaultClient () =
        let socketsHandler = new SocketsHttpHandler(
            PooledConnectionLifetime = TimeSpan.FromMinutes(10.0),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5.0),
            MaxConnectionsPerServer = Int32.MaxValue
        )
        new HttpClient(socketsHandler)

    /// <summary>
    /// Creates an HTTP request with the specified method and URL.
    /// <see href="https://nbomber.com/docs/protocols/http#createrequest">Documentation link</see>
    /// </summary>
    /// <param name="method">The HTTP method (e.g., "GET", "POST").</param>
    /// <param name="url">The request URL.</param>
    /// <returns>A new <see cref="HttpRequestMessage"/>.</returns>
    let createRequest (method: string) (url: string) =
        new HttpRequestMessage(
            method = HttpMethod(method),
            requestUri = Uri(url, UriKind.RelativeOrAbsolute)
        )

    /// <summary>
    /// Adds a custom HTTP header to the request.
    /// <see href="https://nbomber.com/docs/protocols/http#createrequest">Documentation link</see>
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="name">The name of the header.</param>
    /// <param name="value">The value of the header.</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/>.</returns>
    let withHeader (name: string) (value: string) (req: HttpRequestMessage) =
        req.Headers.TryAddWithoutValidation(name, value) |> ignore
        req

    /// Adds a custom HTTP headers to the request.
    let withHeaders (headers: (string * string) list) (req: HttpRequestMessage) =
        headers |> List.iter(fun (name, value) -> req.Headers.TryAddWithoutValidation(name, value) |> ignore)
        req

    /// <summary>
    /// Sets the HTTP version of the request.
    /// <see href="https://nbomber.com/docs/protocols/http#createrequest">Documentation link</see>
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="version">The HTTP version string (e.g., "1.1", "2.0").</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/>.</returns>
    let withVersion (version: string) (req: HttpRequestMessage) =
        req.Version <- Version.Parse version
        req

    /// <summary>
    /// Sets the body content of the HTTP request.
    /// <see href="https://nbomber.com/docs/protocols/http#createrequest">Documentation link</see>
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="body">The HTTP content to set as the request body.</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/>.</returns>
    let withBody (body: HttpContent) (req: HttpRequestMessage) =
        req.Content <- body
        req

    /// Populates request body by serializing data record to JSON format.
    /// Also, it adds HTTP header: "Content-Type: application/json".
    let withJsonBody2 (data: 'T) (options: JsonSerializerOptions) (req: HttpRequestMessage) =
        let json = JsonSerializer.Serialize(data, options)
        req.Content <- new StringContent(json, Encoding.UTF8, "application/json")
        req

    /// <summary>
    /// Populates request body by serializing data record to JSON format.
    /// Also, it adds HTTP header: "Content-Type: application/json".
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    let withJsonBody (data: 'T) (req: HttpRequestMessage) =
        withJsonBody2 data null req

    let sendWithArgs (client: HttpClient) (clientArgs: HttpClientArgs) (request: HttpRequestMessage) = backgroundTask {
        if clientArgs.Logger.IsSome then
            do! tryLogRequest(clientArgs, request)

        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)

        if clientArgs.Logger.IsSome then
            do! tryLogResponse(clientArgs, response)

        let reqSize = calcRequestSize request
        let respSize = calcResponseSize response
        let dataSize = reqSize + respSize

        return
            if response.IsSuccessStatusCode then
                Response.ok(statusCode = response.StatusCode.ToString(), sizeBytes = dataSize, payload = response)
            else
                Response.fail(statusCode = response.StatusCode.ToString(), sizeBytes = dataSize, payload = response)
    }

    let send (client: HttpClient) (request: HttpRequestMessage) =
        let clientArgs = HttpClientArgs.create(CancellationToken.None)
        sendWithArgs client clientArgs request

    /// <summary>
    /// Send request and deserialize HTTP response JSON body to specified type 'T
    /// </summary>
    let sendTypedWithArgs<'T> (client: HttpClient) (clientArgs: HttpClientArgs) (request: HttpRequestMessage) = backgroundTask {
        if clientArgs.Logger.IsSome then
            do! tryLogRequest(clientArgs, request)

        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)

        if clientArgs.Logger.IsSome then
            do! tryLogResponse(clientArgs, response)

        let reqSize = calcRequestSize request
        let respSize = calcResponseSize response
        let dataSize = reqSize + respSize

        let body = response.Content.ReadAsStreamAsync().Result
        let jsonOptions = clientArgs.JsonSerializerOptions |> Option.defaultValue GlobalJsonSerializerOptions

        let value =
            try
                JsonSerializer.Deserialize<'T>(body, jsonOptions)
            with
                ex -> Unchecked.defaultof<_>

        let httpRes = { Data = value; Response = response }

        return
            if response.IsSuccessStatusCode then
                Response.ok(statusCode = response.StatusCode.ToString(), sizeBytes = dataSize, payload = httpRes)
            else
                Response.fail(statusCode = response.StatusCode.ToString(), sizeBytes = dataSize, payload = httpRes)
    }

    /// <summary>
    /// Send request and deserialize HTTP response JSON body to specified type 'T
    /// </summary>
    let sendTyped<'T> (client: HttpClient) (request: HttpRequestMessage) =
        let clientArgs = HttpClientArgs.create(CancellationToken.None)
        sendTypedWithArgs<'T> client clientArgs request
