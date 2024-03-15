namespace NBomber.Http

open System
open System.Net.Http
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open Serilog

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
open System.Text.Json
open System.Threading
open NBomber.Contracts
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

    let private tryLogRequest (clientArgs: HttpClientArgs, request: HttpRequestMessage) =
        match clientArgs.Logger with
        | Some logger ->
            let headers = String.Join(", ", request.Headers |> Seq.map(fun x -> $"""{x.Key}: {String.Join(", ", x.Value)}"""))

            logger.Debug("HTTP Request:\n TraceId: {TraceId}\n Method: {Method}\n RequestUri: {RequestUri}\n HttpVersion: {HttpVersion}\n Headers: {Headers}\n Content: {Content}\n",
                         clientArgs.TraceId, request.Method, request.RequestUri, request.Version, headers, request.Content.ReadAsStringAsync().Result)
        | None -> ()

    let private tryLogResponse (clientArgs: HttpClientArgs, response: HttpResponseMessage) =
        match clientArgs.Logger with
        | Some logger ->
            let headers = String.Join(", ", response.Headers |> Seq.map(fun x -> $"""{x.Key}: {String.Join(", ", x.Value)}"""))

            logger.Debug("HTTP Response:\n TraceId: {TraceId}\n HttpVersion: {HttpVersion}\n StatusCode: {StatusCode}\n ReasonPhrase: {ReasonPhrase}\n Headers: {Headers}\n Content: {Content}\n",
                         clientArgs.TraceId, response.Version, response.StatusCode, response.ReasonPhrase, headers, response.Content.ReadAsStringAsync().Result)

        | None -> ()

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
    /// Also, it adds HTTP header: "Accept": "application/json".
    let withJsonBody2 (data: 'T) (options: JsonSerializerOptions) (req: HttpRequestMessage) =
        let json = JsonSerializer.SerializeToUtf8Bytes(data, options)
        req.Content <- new ByteArrayContent(json)
        req.Headers.TryAddWithoutValidation("Accept", "application/json") |> ignore
        req

    /// Populates request body by serializing data record to JSON format.
    /// Also, it adds HTTP header: "Accept": "application/json".
    let withJsonBody (data: 'T) (req: HttpRequestMessage) =
        withJsonBody2 data null req

    let sendWithArgs (client: HttpClient) (clientArgs: HttpClientArgs) (request: HttpRequestMessage) = backgroundTask {
        tryLogRequest(clientArgs, request)
        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)
        tryLogResponse(clientArgs, response)

        let reqSize = getRequestSize request
        let respSize = getResponseSize response
        let dataSize = reqSize + respSize

        return
            if response.IsSuccessStatusCode then
                { StatusCode = response.StatusCode.ToString(); IsError = false; SizeBytes = dataSize; Payload = Some response; Message = "" }
            else
                { StatusCode = response.StatusCode.ToString(); IsError = true; SizeBytes = dataSize; Payload = Some response; Message = "" }
    }

    let send (client: HttpClient) (request: HttpRequestMessage) =
        let clientArgs = HttpClientArgs.create(CancellationToken.None)
        sendWithArgs client clientArgs request

    /// <summary>
    /// Send request and deserialize HTTP response JSON body to specified type 'T
    /// </summary>
    let sendTypedWithArgs<'T> (client: HttpClient) (clientArgs: HttpClientArgs) (request: HttpRequestMessage) = backgroundTask {
        tryLogRequest(clientArgs, request)
        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)
        tryLogResponse(clientArgs, response)

        let reqSize = getRequestSize request
        let respSize = getResponseSize response
        let dataSize = reqSize + respSize

        return
            if response.IsSuccessStatusCode then
                let body = response.Content.ReadAsStreamAsync().Result
                let jsonOptions = clientArgs.JsonSerializerOptions |> Option.defaultValue GlobalJsonSerializerOptions
                let value = JsonSerializer.Deserialize<'T>(body, jsonOptions)
                { StatusCode = response.StatusCode.ToString(); IsError = false; SizeBytes = dataSize; Payload = Some value; Message = "" }
            else
                { StatusCode = response.StatusCode.ToString(); IsError = true; SizeBytes = dataSize; Payload = None; Message = "" }
    }

    /// <summary>
    /// Send request and deserialize HTTP response JSON body to specified type 'T
    /// </summary>
    let sendTyped<'T> (client: HttpClient) (request: HttpRequestMessage) =
        let clientArgs = HttpClientArgs.create(CancellationToken.None)
        sendTypedWithArgs<'T> client clientArgs request
