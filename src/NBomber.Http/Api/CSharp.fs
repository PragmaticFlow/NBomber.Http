namespace NBomber.Http.CSharp

open System.Net.Http
open System.Runtime.CompilerServices

open NBomber.Http
open NBomber.Http.FSharp

type HttpStep =
    static member CreateRequest(method: string, url: string) = 
        HttpStep.createRequest method url

type HttpScenario =
    static member Load(fileName: string) = 
        HttpScenario.load fileName

[<Extension>]
type HttpRequestExt =
    [<Extension>]
    static member WithHeader(req: HttpRequest, name: string, value: string) =
        HttpStep.withHeader name value req

    [<Extension>]
    static member WithVersion(req: HttpRequest, version: string) =
        HttpStep.withVersion version req

    [<Extension>]
    static member WithBody(req: HttpRequest, body: HttpContent) =
        HttpStep.withBody body req

    [<Extension>]
    static member BuildStep(req: HttpRequest, name: string) =
        HttpStep.build name req
