namespace NBomber.Http.CSharp

open System.Net.Http
open System.Runtime.CompilerServices
open NBomber.Http

type Http =

    static member CreateRequest(method: string, url: string) =
        NBomber.Http.FSharp.Http.createRequest method url

    static member Send(client: HttpClient, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.send client request
        
    static member Send(client: HttpClient, clientArgs: HttpClientArgs, request: HttpRequestMessage) =
        NBomber.Http.FSharp.Http.sendWithArgs client clientArgs request

[<Extension>]
type HttpExt =

    [<Extension>]
    static member WithHeader(req: HttpRequestMessage, name: string, value: string) =
        req |> NBomber.Http.FSharp.Http.withHeader name value

    [<Extension>]
    static member WithVersion(req: HttpRequestMessage, version: string) =
        req |> NBomber.Http.FSharp.Http.withVersion version

    [<Extension>]
    static member WithBody(req: HttpRequestMessage, body: HttpContent) =
        req |> NBomber.Http.FSharp.Http.withBody body
