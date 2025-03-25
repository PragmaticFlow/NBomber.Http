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
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open NBomber.FSharp
open NBomber.Http

module Http =

    let mutable GlobalJsonSerializerOptions = JsonSerializerOptions.Default

    let private getHeadersSize (headers: HttpHeaders) =
        headers
        |> Seq.map(fun x -> x.Key.Length + (x.Value |> Seq.sumBy _.Length))
        |> Seq.sum

    let private getBodySize (body: HttpContent) =
        if not (isNull body) && body.Headers.ContentLength.HasValue then
            int32 body.Headers.ContentLength.Value
        else
            0

    let private getRequestSize (request: HttpRequestMessage) =
        let headersSize = getHeadersSize request.Headers
        let bodySize    = getBodySize request.Content
        bodySize + headersSize

    let private getResponseSize (response: HttpResponseMessage) =
        let headersSize = getHeadersSize response.Headers
        let bodySize    = getBodySize response.Content
        bodySize + headersSize

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

    let createRequest (method: string) (url: string) =
        new HttpRequestMessage(
            method = HttpMethod(method),
            requestUri = Uri(url, UriKind.RelativeOrAbsolute)
        )

    let withHeader (name: string) (value: string) (req: HttpRequestMessage) =
        req.Headers.TryAddWithoutValidation(name, value) |> ignore
        req

    let withHeaders (headers: (string * string) list) (req: HttpRequestMessage) =
        headers |> List.iter(fun (name, value) -> req.Headers.TryAddWithoutValidation(name, value) |> ignore)
        req

    let withVersion (version: string) (req: HttpRequestMessage) =
        req.Version <- Version.Parse version
        req

    let withBody (body: HttpContent) (req: HttpRequestMessage) =
        req.Content <- body
        req

    /// Populates request body by serializing data record to JSON format.
    /// Also, it adds HTTP header: "Content-Type: application/json".
    let withJsonBody2 (data: 'T) (options: JsonSerializerOptions) (req: HttpRequestMessage) =
        let json = JsonSerializer.Serialize(data, options)
        req.Content <- new StringContent(json, Encoding.UTF8, "application/json")
        req

    /// Populates request body by serializing data record to JSON format.
    /// Also, it adds HTTP header: "Content-Type: application/json".
    let withJsonBody (data: 'T) (req: HttpRequestMessage) =
        withJsonBody2 data null req

    let sendWithArgs (client: HttpClient) (clientArgs: HttpClientArgs) (request: HttpRequestMessage) = backgroundTask {
        if clientArgs.Logger.IsSome then
            do! tryLogRequest(clientArgs, request)

        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)

        if clientArgs.Logger.IsSome then
            do! tryLogResponse(clientArgs, response)

        let reqSize = getRequestSize request
        let respSize = getResponseSize response
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

        let reqSize = getRequestSize request
        let respSize = getResponseSize response
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
