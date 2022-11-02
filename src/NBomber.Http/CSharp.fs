namespace NBomber.Http.CSharp

open System.Net.Http

type Http =

    static member GetRequestSize (request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.getRequestSize request

    static member GetResponseSize (response: HttpResponseMessage) =
        NBomber.Http.FSharp.Http.getResponseSize response

    static member Send (client: HttpClient, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.send client request
