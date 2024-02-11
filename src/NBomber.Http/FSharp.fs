namespace NBomber.Http

open System.Net.Http
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading

type HttpClientArgs = {
    HttpCompletion: HttpCompletionOption
    CancellationToken: CancellationToken
    JsonSerializerOptions: JsonSerializerOptions option
}
with
    [<CompiledName("Create")>]
    static member create(cancellationToken: CancellationToken,
                         [<Optional;DefaultParameterValue(HttpCompletionOption.ResponseContentRead)>] httpCompletion,
                         [<Optional;DefaultParameterValue(null:JsonSerializerOptions)>] jsonOptions: JsonSerializerOptions) = {

        HttpCompletion = httpCompletion
        CancellationToken = cancellationToken
        JsonSerializerOptions = jsonOptions |> Option.ofObj
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

    let internal getHeadersSize (headers: HttpHeaders) =
        headers
        |> Seq.map(fun x -> x.Key.Length + (x.Value |> Seq.sumBy _.Length))
        |> Seq.sum

    let internal getBodySize (body: HttpContent) =
        if not (isNull body) && body.Headers.ContentLength.HasValue then
            int32 body.Headers.ContentLength.Value
        else
            0

    let internal getRequestSize (request: HttpRequestMessage) =
        let headersSize = getHeadersSize request.Headers
        let bodySize    = getBodySize request.Content
        bodySize + headersSize

    let internal getResponseSize (response: HttpResponseMessage) =
        let headersSize = getHeadersSize response.Headers
        let bodySize    = getBodySize response.Content
        bodySize + headersSize

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
        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)

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
        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)

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
