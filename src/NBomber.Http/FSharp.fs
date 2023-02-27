namespace NBomber.Http

open System.Net.Http
open System.Threading

[<Struct>]
type HttpClientArgs = {
    HttpCompletion: HttpCompletionOption
    CancellationToken: CancellationToken
}

namespace NBomber.Http.FSharp

open System
open System.Net.Http
open System.Net.Http.Headers
open NBomber.Contracts
open NBomber.Http

module Http =

    let internal getHeadersSize (headers: HttpHeaders) =
        headers
        |> Seq.map(fun x -> x.Value |> Seq.sumBy(fun x -> x.Length))
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

    let send (client: HttpClient) (request: HttpRequestMessage) = backgroundTask {
        let! response = client.SendAsync request

        let reqSize = getRequestSize request
        let respSize = getResponseSize response
        let dataSize = reqSize + respSize

        return
            if response.IsSuccessStatusCode then
                { StatusCode = response.StatusCode.ToString(); IsError = false; SizeBytes = dataSize; Payload = Some response; Message = ""; LatencyMs = 0 }
            else
                { StatusCode = response.StatusCode.ToString(); IsError = true; SizeBytes = dataSize; Payload = Some response; Message = ""; LatencyMs = 0 }
    }

    let sendWithArgs (client: HttpClient) (clientArgs: HttpClientArgs) (request: HttpRequestMessage) = backgroundTask {
        let! response = client.SendAsync(request, clientArgs.HttpCompletion, clientArgs.CancellationToken)

        let reqSize = getRequestSize request
        let respSize = getResponseSize response
        let dataSize = reqSize + respSize

        return
            if response.IsSuccessStatusCode then
                { StatusCode = response.StatusCode.ToString(); IsError = false; SizeBytes = dataSize; Payload = Some response; Message = ""; LatencyMs = 0 }
            else
                { StatusCode = response.StatusCode.ToString(); IsError = true; SizeBytes = dataSize; Payload = Some response; Message = ""; LatencyMs = 0 }
    }