namespace NBomber.Plugins.Http.CSharp

open System
open System.Net.Http
open System.Threading.Tasks
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

open NBomber.Contracts
open NBomber.Plugins.Http
open NBomber.Plugins.Http.FSharp

type HttpClientFactory =

    static member Create ([<Optional;DefaultParameterValue("nbomber_http_factory")>] name: string,
                          [<Optional;DefaultParameterValue(null:HttpClient)>] httpClient: HttpClient) =

        HttpClientFactory.create(name, ?httpClient = (Option.ofObj httpClient))

type Http =
    static member CreateRequest(method: string, url: string) = Http.createRequest method url
    static member Send(req: HttpRequest, context: IStepContext<HttpClient,'TFeedItem>) = Http.send context req

[<Extension>]
type HttpExt =

    [<Extension>]
    static member WithHeader(req: HttpRequest, name: string, value: string) =
        req |> Http.withHeader name value

    [<Extension>]
    static member WithVersion(req: HttpRequest, version: string) =
        req |> Http.withVersion version

    [<Extension>]
    static member WithBody(req: HttpRequest, body: HttpContent) =
        req |> Http.withBody(body)

    [<Extension>]
    static member WithCheck(req: HttpRequest, check: System.Func<HttpResponseMessage,Task<Response>>) =
        req |> Http.withCheck(fun response -> check.Invoke response)

    [<Extension>]
    static member ToNBomberResponse(response: HttpResponseMessage) =
        Response.ofHttp response
