namespace NBomber.Http.CSharp

open System
open System.Net.Http
open System.Runtime.CompilerServices

open NBomber.Http
open NBomber.Http.FSharp

type Http = 
    static member CreateRequest(method: string, url: string) = Http.createRequest method url

[<Extension>]
type HttpRequestExt =

    [<Extension>]
    static member WithHeader(req: HttpRequest, name: string, value: string) = 
        req |> Http.withHeader name  value

    [<Extension>]
    static member WithVersion(req: HttpRequest, version: string) = 
        req |> Http.withVersion(version)

    [<Extension>]
    static member WithBody(req: HttpRequest, body: HttpContent) = 
        req |> Http.withBody(body)

    [<Extension>]
    static member WithCheck(req: HttpRequest, check: System.Func<HttpResponseMessage,bool>) = 
        req |> Http.withCheck(fun response -> check.Invoke(response))    
        
type HttpStep =
    static member Create(name: string, req: HttpRequest) = HttpStep.create name req
    
    static member CreateFromResponse(name: string, createReqFn: Func<HttpResponseMessage, HttpRequest>) = 
        HttpStep.createFromResponse name (createReqFn.Invoke)