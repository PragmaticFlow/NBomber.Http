namespace NBomber.Http.CSharp

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
    static member CreateDefaultClient() =
        NBomber.Http.FSharp.Http.createDefaultClient()

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
    /// Send request and deserialize HTTP response JSON body to specified type <typeparamref name="T"/>
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    /// <typeparam name="T">The type to which the response JSON should be deserialized.</typeparam>
    /// <param name="client">The <see cref="HttpClient"/> to use.</param>
    /// <param name="request">The HTTP request to send.</param>
    /// <returns>The deserialized response body of type <typeparamref name="T"/>.</returns>
    static member Send<'T>(client: HttpClient, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.sendTyped<'T> client request

    /// <summary>
    /// Send request and deserialize HTTP response JSON body to specified type <typeparamref name="T"/>
    /// <see href="https://nbomber.com/docs/protocols/http#json-support">Documentation link</see>
    /// </summary>
    /// <typeparam name="T">The type to which the response JSON should be deserialized.</typeparam>
    /// <param name="client">The <see cref="HttpClient"/> to use.</param>
    /// <param name="clientArgs">Additional arguments for configuring: request/response serialization, enabling tracing, setting CancellationToken, etc.</param>
    /// <param name="request">The HTTP request to send.</param>
    /// <returns>The deserialized response body of type <typeparamref name="T"/>.</returns>
    static member Send<'T>(client: HttpClient, clientArgs: HttpClientArgs, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.sendTypedWithArgs client clientArgs request

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
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The modified <see cref="HttpRequestMessage"/> with a JSON body.</returns>
    [<Extension>]
    static member WithJsonBody(req: HttpRequestMessage,
                               data: 'T,
                               [<Optional;DefaultParameterValue(null:JsonSerializerOptions)>] options: JsonSerializerOptions) =

        req |> NBomber.Http.FSharp.Http.withJsonBody2 data options
