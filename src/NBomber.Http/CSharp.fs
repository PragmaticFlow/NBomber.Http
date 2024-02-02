namespace NBomber.Http.CSharp

open System.Net.Http
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
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

    /// <summary>
    /// Populates request BODY by serializing data to JSON format.
    /// Also, it adds HTTP header: "Accept": "application/json".
    /// </summary>
    [<Extension>]
    static member WithJsonBody(req: HttpRequestMessage,
                               data: 'T,
                               [<Optional;DefaultParameterValue(null:JsonSerializerOptions)>] options: JsonSerializerOptions) =

        req |> NBomber.Http.FSharp.Http.withJsonBody2 data options
