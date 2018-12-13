namespace NBomber.Http.CSharp

open System.Net.Http
open System.Runtime.CompilerServices

open NBomber.Http
open NBomber.Http.FSharp

type HttpStep =
    static member CreateRequest(method: string, url: string) = HttpStep.createRequest(method, url)

[<Extension>]
type HttpRequestExt =

    [<Extension>]
    static member WithHeader(req: HttpRequest, name: string, value: string) = 
        req |> HttpStep.withHeader(name, value)

    [<Extension>]
    static member WithVersion(req: HttpRequest, version: string) = 
        req |> HttpStep.withVersion(version)

    [<Extension>]
    static member WithBody(req: HttpRequest, body: HttpContent) = 
        req |> HttpStep.withBody(body)

    [<Extension>]
    static member BuildStep(req: HttpRequest, name: string) = 
        req |> HttpStep.build(name)