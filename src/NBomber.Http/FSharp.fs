namespace NBomber.Http.FSharp

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Runtime.InteropServices
open System.Text
open System.Text.Json
open NBomber.FSharp
open NBomber.Http

/// Provides helper functions for working with HTTP client.
[<AbstractClass; Sealed>]
type Http =

    /// Gets or sets the global JSON serializer options used by HTTP client
    static member GlobalJsonSerializerOptions
        with get() = HttpImpl.globalJsonSerializerOptions
        and set(v) = HttpImpl.globalJsonSerializerOptions <- v

#if NET8_0_OR_GREATER
    /// <summary>
    /// Creates a new instance of <see cref="HttpClient"/> with configured <see cref="SocketsHttpHandler"/>.
    /// All parameters are optional: <c>Http.createDefaultClient()</c> or, for example,
    /// <c>Http.createDefaultClient(maxConnectionsPerServer = 1000, httpVersion = "1.1")</c>.
    /// </summary>
    /// <param name="maxConnectionsPerServer">Defines the maximum number of outbound connections which will be established per endpoint. The default value: 5000.</param>
    /// <param name="pooledConnectionLifetime">
    /// Defines how long a connection can stay in the pool before it is closed. The default value (if not set): 3 minutes.
    /// When a connection is closed, the next connection resolves DNS again.
    /// Because of this, this value is the maximum time before the client sees a DNS change (for example, a blue/green switch or a failover).
    /// A smaller value finds DNS changes faster, but the client opens new connections (TCP/TLS handshakes) more frequently.
    /// </param>
    /// <param name="dnsSelection">
    /// Defines which IP address a new connection uses when the host name resolves to several addresses (for example, DNS round-robin).
    /// The default value: <c>DnsSelection.Random</c>.
    /// <c>Random</c> and <c>RoundRobin</c> spread connections over all addresses, as many independent clients do.
    /// <c>First</c> keeps the default .NET behavior: all connections usually go to the first address.
    /// If the connection to the selected address fails, the next addresses are tried.
    /// </param>
    /// <param name="httpVersion">
    /// Defines the HTTP version (for example, "1.1", "2.0" or "3.0") for every request sent with this client. The default value: "2.0".
    /// The client sets this version and <c>versionPolicy</c> on every request.
    /// A request with a version set by <c>withVersion</c> keeps its own version.
    /// If null, the client does not change requests, so the version of each request is used (by default, HTTP/1.1).
    /// With the default "2.0" and <c>RequestVersionOrLower</c>, the client uses HTTP/2 for <c>https</c> URLs if the server supports it,
    /// and HTTP/1.1 for <c>http</c> URLs and for servers without HTTP/2.
    /// </param>
    /// <param name="versionPolicy">
    /// Defines how the client selects the HTTP version when <c>httpVersion</c> is set. The default value: <c>RequestVersionOrLower</c>.
    /// - <c>RequestVersionOrLower</c>: uses <c>httpVersion</c> if the server supports it, otherwise falls back to a lower version.
    ///   For <c>http</c> URLs (without TLS), HTTP/2 is not negotiated, so the client uses HTTP/1.1.
    /// - <c>RequestVersionExact</c>: uses only <c>httpVersion</c>, otherwise the request fails. Use it to send HTTP/2 without TLS (h2c).
    /// - <c>RequestVersionOrHigher</c>: uses <c>httpVersion</c> or a higher version.
    /// </param>
    /// <param name="automaticDecompression">
    /// Defines which compressed response bodies the client decompresses. The default value: <c>DecompressionMethods.None</c> (no decompression).
    /// With any other value, the client sends an <c>Accept-Encoding</c> header and decompresses the response body.
    /// Set it to read a compressed body as text or JSON: <c>sendTyped</c> deserializes the body as it is, so a compressed body fails to deserialize.
    /// Note the effect on the reported size: a decompressed body is counted with its decompressed length, and .NET removes
    /// the <c>Content-Length</c> and <c>Content-Encoding</c> headers of the response, so those bytes are not counted either.
    /// The size is then the size of the message the scenario works with, not the number of bytes sent over the network.
    /// With <c>None</c>, the size stays byte-exact for HTTP/1.1.
    /// </param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    /// <remarks>
    /// The internal <see cref="SocketsHttpHandler"/> is configured with:
    /// - <c>PooledConnectionLifetime</c>: the value of <c>pooledConnectionLifetime</c> (3 minutes by default)
    /// - <c>PooledConnectionIdleTimeout</c>: 1.5 minutes
    /// - <c>MaxConnectionsPerServer</c>: the value of <c>maxConnectionsPerServer</c> (5000 by default)
    /// - <c>EnableMultipleHttp2Connections</c>: true. For HTTP/2, a new connection opens when the current connections reach the server's limit of parallel streams. Requests do not wait in a client-side queue.
    /// - <c>ConnectCallback</c>: selects the IP address with <c>dnsSelection</c> (not set for <c>First</c>)
    /// - <c>AutomaticDecompression</c>: the value of <c>automaticDecompression</c> (<c>None</c> by default)
    /// The client <c>Timeout</c> is 1 minute.
    /// </remarks>
    static member createDefaultClient([<Optional;DefaultParameterValue(5000)>] maxConnectionsPerServer: int,
                                      [<Optional>] pooledConnectionLifetime: Nullable<TimeSpan>,
                                      [<Optional;DefaultParameterValue(DnsSelection.Random)>] dnsSelection: DnsSelection,
                                      [<Optional;DefaultParameterValue("2.0")>] httpVersion: string,
                                      [<Optional;DefaultParameterValue(HttpVersionPolicy.RequestVersionOrLower)>] versionPolicy: HttpVersionPolicy,
                                      [<Optional;DefaultParameterValue(DecompressionMethods.None)>] automaticDecompression: DecompressionMethods) =

        let lifetime =
            if pooledConnectionLifetime.HasValue then pooledConnectionLifetime.Value
            else HttpImpl.defaultPooledConnectionLifetime

        let socketsHandler = HttpImpl.createSocketsHandler maxConnectionsPerServer lifetime dnsSelection automaticDecompression

        let client =
            if isNull httpVersion then
                new HttpClient(socketsHandler)
            else
                let version = Version.Parse httpVersion
                let client = new HttpClient(new HttpImpl.HttpVersionHandler(socketsHandler, version, versionPolicy))
                client.DefaultRequestVersion <- version
                client.DefaultVersionPolicy <- versionPolicy
                client

        client.Timeout <- TimeSpan.FromMinutes 1.0
        client
#endif

    /// <summary>
    /// Creates an HTTP request with the specified method and URL.
    /// <see href="https://nbomber.com/docs/protocols/http#createrequest">Documentation link</see>
    /// </summary>
    /// <param name="method">The HTTP method (e.g., "GET", "POST").</param>
    /// <param name="url">The request URL.</param>
    /// <returns>A new <see cref="HttpRequestMessage"/>.</returns>
    static member createRequest (method: string) (url: string) =
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
    static member withHeader (name: string) (value: string) (req: HttpRequestMessage) =
        req.Headers.TryAddWithoutValidation(name, value) |> ignore
        req

    /// Adds a custom HTTP headers to the request.
    static member withHeaders (headers: (string * string) list) (req: HttpRequestMessage) =
        headers |> List.iter(fun (name, value) -> req.Headers.TryAddWithoutValidation(name, value) |> ignore)
        req

    /// <summary>
    /// Sets the HTTP version of the request.
    /// <see href="https://nbomber.com/docs/protocols/http#createrequest">Documentation link</see>
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="version">The HTTP version string (e.g., "1.1", "2.0").</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/>.</returns>
    /// <remarks>
    /// This version has priority over the HTTP version of the client (the <c>httpVersion</c> parameter of <c>createDefaultClient</c>).
    /// The version policy of the request is not changed (by default, <c>RequestVersionOrLower</c>).
    /// </remarks>
    static member withVersion (version: string) (req: HttpRequestMessage) =
        req.Version <- Version.Parse version
#if NET8_0_OR_GREATER
        req.Options.Set(HttpImpl.HttpVersionHandler.ExplicitVersionKey, true)
#endif
        req

    /// <summary>
    /// Sets the body content of the HTTP request.
    /// <see href="https://nbomber.com/docs/protocols/http#createrequest">Documentation link</see>
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="body">The HTTP content to set as the request body.</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/>.</returns>
    static member withBody (body: HttpContent) (req: HttpRequestMessage) =
        req.Content <- body
        req

    /// Populates request body by serializing data record to JSON format.
    /// Also, it adds HTTP header: "Content-Type: application/json".
    static member withJsonBody2 (data: 'T) (options: JsonSerializerOptions) (req: HttpRequestMessage) =
        let json = JsonSerializer.Serialize(data, options)
        req.Content <- new StringContent(json, Encoding.UTF8, "application/json")
        req

    /// <summary>
    /// Populates request body by serializing data record to JSON format.
    /// Also, it adds HTTP header: "Content-Type: application/json".
    /// Serialization uses <see cref="GlobalJsonSerializerOptions"/> (by default, <see cref="JsonSerializerOptions.Web"/>).
    /// To use custom options for a single request, use <c>withJsonBody2</c>.
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    /// <param name="data">The data object to serialize into the request body.</param>
    /// <param name="req">The HTTP request message.</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/> with a JSON body.</returns>
    static member withJsonBody (data: 'T) (req: HttpRequestMessage) =
        Http.withJsonBody2 data Http.GlobalJsonSerializerOptions req

    static member sendWithArgs (client: HttpClient) (clientArgs: HttpClientArgs) (request: HttpRequestMessage) = backgroundTask {
        if clientArgs.Logger.IsSome then
            do! HttpImpl.tryLogRequest(clientArgs, request)

        // a body without a known length is sent chunked, so it has to be counted while it's written
        let originalContent = HttpImpl.tryTrackRequestBody request

        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)

        if clientArgs.Logger.IsSome then
            do! HttpImpl.tryLogResponse(clientArgs, response)

        let reqSize = HttpImpl.calcRequestSize request

        // a decompressed body has no Content-Length, so its length can only come from the buffered content
        let buffered = clientArgs.HttpCompletion = HttpCompletionOption.ResponseContentRead
        let! respBodySize = HttpImpl.tryGetResponseBodyLength response buffered
        let respSize = HttpImpl.calcResponseSizeWithBody (ValueOption.defaultValue 0L respBodySize) response

        let dataSize = reqSize + respSize

        HttpImpl.restoreRequestBody request originalContent

        return
            if response.IsSuccessStatusCode then
                Response.ok(statusCode = response.StatusCode.ToString(), sizeBytes = dataSize, payload = response)
            else
                Response.fail(statusCode = response.StatusCode.ToString(), sizeBytes = dataSize, payload = response)
    }

    static member send (client: HttpClient) (request: HttpRequestMessage) =
        Http.sendWithArgs client HttpImpl.defaultClientArgs request

    /// <summary>
    /// Sends an HTTP request and deserializes the JSON body of a successful response to 'T,
    /// using clientArgs.JsonSerializerOptions or, if it isn't set, <see cref="GlobalJsonSerializerOptions"/>.
    /// A 2xx response whose body isn't valid JSON for 'T is returned as failed, with the deserialization error in Message.
    /// 204 No Content or an empty body is OK with the default Data. Non-2xx responses fail, and their body isn't deserialized.
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    static member sendTypedWithArgs<'T> (client: HttpClient) (clientArgs: HttpClientArgs) (request: HttpRequestMessage) = backgroundTask {
        if clientArgs.Logger.IsSome then
            do! HttpImpl.tryLogRequest(clientArgs, request)

        // a body without a known length is sent chunked, so it has to be counted while it's written
        let originalContent = HttpImpl.tryTrackRequestBody request

        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)

        if clientArgs.Logger.IsSome then
            do! HttpImpl.tryLogResponse(clientArgs, response)

        let reqSize = HttpImpl.calcRequestSize request

        // a decompressed body has no Content-Length, so its length can only come from the buffered content
        let buffered = clientArgs.HttpCompletion = HttpCompletionOption.ResponseContentRead
        let! respBodySize = HttpImpl.tryGetResponseBodyLength response buffered
        let respSize = HttpImpl.calcResponseSizeWithBody (ValueOption.defaultValue 0L respBodySize) response

        let dataSize = reqSize + respSize

        HttpImpl.restoreRequestBody request originalContent

        let statusCode = response.StatusCode.ToString()
        let noData = { Data = Unchecked.defaultof<'T>; Response = response }

        // reuse the already resolved length, instead of asking tryGetBodyLength a second time
        let emptyBody = respBodySize = ValueSome 0L

        if not response.IsSuccessStatusCode then
            return Response.fail(statusCode = statusCode, sizeBytes = dataSize, payload = noData)

        // 204 No Content or an empty 2xx body: nothing to deserialize, and it's not an error
        elif response.StatusCode = HttpStatusCode.NoContent || emptyBody then
            return Response.ok(statusCode = statusCode, sizeBytes = dataSize, payload = noData)

        else
            let jsonOptions = clientArgs.JsonSerializerOptions |> Option.defaultValue Http.GlobalJsonSerializerOptions
            try
                let! value = response.Content.ReadFromJsonAsync<'T>(jsonOptions, clientArgs.CancellationToken)
                return Response.ok(statusCode = statusCode, sizeBytes = dataSize, payload = { Data = value; Response = response })
            with
            | :? JsonException as ex ->
                // a 2xx response whose body isn't the expected JSON is a failed request
                return Response.fail(statusCode = statusCode, message = $"JSON deserialization failed: {ex.Message}",
                                     sizeBytes = dataSize, payload = noData)
    }

    /// <summary>
    /// Sends an HTTP request and deserializes the JSON body of a successful response to 'T,
    /// using <see cref="GlobalJsonSerializerOptions"/>. Behaves like <c>sendTypedWithArgs</c> with default client arguments.
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    static member sendTyped<'T> (client: HttpClient) (request: HttpRequestMessage) =
        Http.sendTypedWithArgs<'T> client HttpImpl.defaultClientArgs request
