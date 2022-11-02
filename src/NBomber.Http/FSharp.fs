namespace NBomber.Http.FSharp

open System.Net.Http
open System.Net.Http.Headers
open NBomber.Contracts

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

    let getRequestSize (request: HttpRequestMessage) =
        let headersSize = getHeadersSize request.Headers
        let bodySize    = getBodySize request.Content
        bodySize + headersSize

    let getResponseSize (response: HttpResponseMessage) =
        let headersSize = getHeadersSize response.Headers
        let bodySize    = getBodySize response.Content
        bodySize + headersSize

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
