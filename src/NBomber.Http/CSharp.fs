namespace NBomber.Http.CSharp

open System
open System.Net
open System.Net.Http
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open NBomber.Http

/// Provides helper methods for working with HTTP client.
type Http =

    /// Gets or sets the global JSON serializer options used by HTTP client.
    static member GlobalJsonSerializerOptions
        with get() = NBomber.Http.FSharp.Http.GlobalJsonSerializerOptions
        and set(v) = NBomber.Http.FSharp.Http.GlobalJsonSerializerOptions <- v

#if NET8_0_OR_GREATER
    /// <summary>
    /// Creates a new instance of <see cref="HttpClient"/> with configured <see cref="SocketsHttpHandler"/>.
    /// </summary>
    /// <param name="maxConnectionsPerServer">Defines the maximum number of outbound connections which will be established per endpoint. The default value: 5000.</param>
    /// <param name="pooledConnectionLifetime">
    /// Defines how long a connection can stay in the pool before it is closed. The default value (if null): 3 minutes.
    /// When a connection is closed, the next connection resolves DNS again.
    /// Because of this, this value is the maximum time before the client sees a DNS change (for example, a blue/green switch or a failover).
    /// A smaller value finds DNS changes faster, but the client opens new connections (TCP/TLS handshakes) more frequently.
    /// </param>
    /// <param name="dnsSelection">
    /// Defines which IP address a new connection uses when the host name resolves to several addresses (for example, DNS round-robin).
    /// The default value: <see cref="DnsSelection.Random"/>.
    /// <c>Random</c> and <c>RoundRobin</c> spread connections over all addresses, as many independent clients do.
    /// <c>First</c> keeps the default .NET behavior: all connections usually go to the first address.
    /// If the connection to the selected address fails, the next addresses are tried.
    /// </param>
    /// <param name="httpVersion">
    /// Defines the HTTP version (for example, "1.1", "2.0" or "3.0") for every request sent with this client. The default value: "2.0".
    /// The client sets this version and <paramref name="versionPolicy"/> on every request.
    /// A request with a version set by <c>WithVersion</c> keeps its own version.
    /// If null, the client does not change requests, so the version of each request is used (by default, HTTP/1.1).
    /// With the default "2.0" and <c>RequestVersionOrLower</c>, the client uses HTTP/2 for <c>https</c> URLs if the server supports it,
    /// and HTTP/1.1 for <c>http</c> URLs and for servers without HTTP/2.
    /// </param>
    /// <param name="versionPolicy">
    /// Defines how the client selects the HTTP version when <paramref name="httpVersion"/> is set.
    /// The default value: <see cref="HttpVersionPolicy.RequestVersionOrLower"/>.
    /// - <c>RequestVersionOrLower</c>: uses <paramref name="httpVersion"/> if the server supports it, otherwise falls back to a lower version.
    ///   For <c>http</c> URLs (without TLS), HTTP/2 is not negotiated, so the client uses HTTP/1.1.
    /// - <c>RequestVersionExact</c>: uses only <paramref name="httpVersion"/>, otherwise the request fails. Use it to send HTTP/2 without TLS (h2c).
    /// - <c>RequestVersionOrHigher</c>: uses <paramref name="httpVersion"/> or a higher version.
    /// </param>
    /// <param name="automaticDecompression">
    /// Defines which compressed response bodies the client decompresses. The default value: <see cref="DecompressionMethods.None"/> (no decompression).
    /// With any other value, the client sends an <c>Accept-Encoding</c> header and decompresses the response body.
    /// Set it to read a compressed body as text or JSON: <c>SendJson</c> deserializes the body as it is, so a compressed body fails to deserialize.
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
    static member CreateDefaultClient([<Optional;DefaultParameterValue(5000)>] maxConnectionsPerServer: int,
                                      [<Optional>] pooledConnectionLifetime: Nullable<TimeSpan>,
                                      [<Optional;DefaultParameterValue(DnsSelection.Random)>] dnsSelection: DnsSelection,
                                      [<Optional;DefaultParameterValue("2.0")>] httpVersion: string,
                                      [<Optional;DefaultParameterValue(HttpVersionPolicy.RequestVersionOrLower)>] versionPolicy: HttpVersionPolicy,
                                      [<Optional;DefaultParameterValue(DecompressionMethods.None)>] automaticDecompression: DecompressionMethods) =

        NBomber.Http.FSharp.Http.createDefaultClient(maxConnectionsPerServer, pooledConnectionLifetime, dnsSelection, httpVersion, versionPolicy, automaticDecompression)
#endif

    /// <summary>
    /// Creates an HTTP request with the specified method and URL.
    /// <see href="https://nbomber.com/docs/protocols/http#createrequest">Documentation link</see>
    /// </summary>
    /// <param name="method">The HTTP method (e.g., "GET", "POST").</param>
    /// <param name="url">The request URL.</param>
    /// <returns>A new <see cref="HttpRequestMessage"/>.</returns>
    static member CreateRequest(method: string, url: string) =
        NBomber.Http.FSharp.Http.createRequest method url

    /// <summary>
    /// Sends an HTTP request using <see cref="HttpClient"/>.
    /// <see href="https://nbomber.com/docs/protocols/http#send">Documentation link</see>
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> to use.</param>
    /// <param name="request">The HTTP request to send.</param>
    /// <returns>The <see cref="HttpResponseMessage"/> returned by the server.</returns>
    static member Send(client: HttpClient, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.send client request

    /// <summary>
    /// Sends an HTTP request using <see cref="HttpClient"/> and additional client arguments.
    /// <see href="https://nbomber.com/docs/protocols/http#send">Documentation link</see>
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> to use.</param>
    /// <param name="clientArgs">Additional arguments for configuring: request/response serialization, enabling tracing, setting CancellationToken, etc.</param>
    /// <param name="request">The HTTP request to send.</param>
    /// <returns>The <see cref="HttpResponseMessage"/> returned by the server.</returns>
    static member Send(client: HttpClient, clientArgs: HttpClientArgs, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.sendWithArgs client clientArgs request

    /// <summary>
    /// Sends an HTTP request and deserializes the JSON body of a successful response to <typeparamref name="T"/>.
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    /// <typeparam name="T">The type to which the response JSON should be deserialized.</typeparam>
    /// <param name="client">The <see cref="HttpClient"/> to use.</param>
    /// <param name="request">The HTTP request to send.</param>
    /// <returns>
    /// A response with <see cref="HttpResponse{T}"/> as payload: <c>Data</c> holds the deserialized body,
    /// and <c>Response</c> holds the <see cref="HttpResponseMessage"/>.
    /// </returns>
    /// <remarks>
    /// The body is deserialized with <see cref="Http.GlobalJsonSerializerOptions"/> (by default, <see cref="JsonSerializerOptions.Web"/>).
    /// - 2xx with a JSON body: OK, and <c>Data</c> holds the deserialized value.
    /// - 204 No Content or an empty body: OK, and <c>Data</c> is <c>default(T)</c>.
    /// - 2xx with a body that isn't valid JSON for <typeparamref name="T"/>: failed. The status code is kept, and <c>Message</c> contains the deserialization error.
    /// - Non-2xx: failed, and the body isn't deserialized (<c>Data</c> is <c>default(T)</c>). Read <c>Response.Content</c> to inspect it.
    /// </remarks>
    static member Send<'T>(client: HttpClient, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.sendTyped<'T> client request

    /// <summary>
    /// Sends an HTTP request and deserializes the JSON body of a successful response to <typeparamref name="T"/>.
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    /// <typeparam name="T">The type to which the response JSON should be deserialized.</typeparam>
    /// <param name="client">The <see cref="HttpClient"/> to use.</param>
    /// <param name="clientArgs">Additional arguments for configuring: request/response serialization, enabling tracing, setting CancellationToken, etc.</param>
    /// <param name="request">The HTTP request to send.</param>
    /// <returns>
    /// A response with <see cref="HttpResponse{T}"/> as payload: <c>Data</c> holds the deserialized body,
    /// and <c>Response</c> holds the <see cref="HttpResponseMessage"/>.
    /// </returns>
    /// <remarks>
    /// The body is deserialized with <c>clientArgs.JsonSerializerOptions</c> or, if it isn't set, with <see cref="Http.GlobalJsonSerializerOptions"/>
    /// (by default, <see cref="JsonSerializerOptions.Web"/>). <c>clientArgs.CancellationToken</c> also cancels reading the body.
    /// - 2xx with a JSON body: OK, and <c>Data</c> holds the deserialized value.
    /// - 204 No Content or an empty body: OK, and <c>Data</c> is <c>default(T)</c>.
    /// - 2xx with a body that isn't valid JSON for <typeparamref name="T"/>: failed. The status code is kept, and <c>Message</c> contains the deserialization error.
    /// - Non-2xx: failed, and the body isn't deserialized (<c>Data</c> is <c>default(T)</c>). Read <c>Response.Content</c> to inspect it.
    /// </remarks>
    static member Send<'T>(client: HttpClient, clientArgs: HttpClientArgs, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.sendTypedWithArgs<'T> client clientArgs request

type HttpExt =

    /// <summary>
    /// Adds a custom HTTP header to the request.
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="name">The name of the header.</param>
    /// <param name="value">The value of the header.</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/>.</returns>
    [<Extension>]
    static member WithHeader(req: HttpRequestMessage, name: string, value: string) =
        req |> NBomber.Http.FSharp.Http.withHeader name value

    /// <summary>
    /// Sets the HTTP version of the request.
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="version">The HTTP version string (e.g., "1.1", "2.0").</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/>.</returns>
    /// <remarks>
    /// This version has priority over the HTTP version of the client (the <c>httpVersion</c> parameter of <c>Http.CreateDefaultClient</c>).
    /// The version policy of the request is not changed (by default, <c>RequestVersionOrLower</c>).
    /// </remarks>
    [<Extension>]
    static member WithVersion(req: HttpRequestMessage, version: string) =
        req |> NBomber.Http.FSharp.Http.withVersion version

    /// <summary>
    /// Sets the body content of the HTTP request.
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="body">The HTTP content to set as the request body.</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/>.</returns>
    [<Extension>]
    static member WithBody(req: HttpRequestMessage, body: HttpContent) =
        req |> NBomber.Http.FSharp.Http.withBody body

    /// <summary>
    /// Populates the request body by serializing the given data to JSON format.
    /// Also adds the "Content-Type: application/json" header.
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    /// <param name="req">The HTTP request message.</param>
    /// <param name="data">The data object to serialize into the request body.</param>
    /// <param name="options">Optional JSON serializer options. If null, <see cref="Http.GlobalJsonSerializerOptions"/> is used (by default, <see cref="JsonSerializerOptions.Web"/>).</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/> with a JSON body.</returns>
    [<Extension>]
    static member WithJsonBody(req: HttpRequestMessage,
                               data: 'T,
                               [<Optional;DefaultParameterValue(null:JsonSerializerOptions)>] options: JsonSerializerOptions) =

        if isNull options then
            req |> NBomber.Http.FSharp.Http.withJsonBody data
        else
            req |> NBomber.Http.FSharp.Http.withJsonBody2 data options
