namespace NBomber.Plugins.Http.FSharp

open System
open System.Net.Http
open System.Threading.Tasks

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Plugins.Http

type HttpClientFactory =

    static member create (?name: string, ?httpClient: HttpClient) =

        let name = defaultArg name "nbomber_http_factory"
        let client = defaultArg httpClient (new HttpClient())

        ClientFactory.create(name,
                             initClient = (fun _ -> Task.FromResult client),
                             clientCount = 1)

module Response =

    let ofHttp (httpResponse: HttpResponseMessage) =
        HttpUtils.createNBomberResponse httpResponse

module Http =

    let createRequest (method: string) (url: string) =
        { Url = Uri(url)
          Version = Version(1, 1)
          Method = HttpMethod(method)
          Headers = Map.empty
          Body = Unchecked.defaultof<HttpContent>
          Check = None }

    let withHeader (name: string) (value: string) (req: HttpRequest) =
        { req with Headers = req.Headers.Add(name, value) }

    let withHeaders (headers: (string*string) list) (req: HttpRequest) =
        { req with Headers = headers |> Map.ofSeq }

    let withVersion (version: string) (req: HttpRequest) =
        { req with Version = Version.Parse(version) }

    let withBody (body: HttpContent) (req: HttpRequest) =
        { req with Body = body }

    let withCheck (check: HttpResponseMessage -> Task<Response>) (req: HttpRequest) =
        { req with Check = Some check }

    let send (context: IStepContext<HttpClient,'TFeedItem>) (req: HttpRequest) =
        HttpUtils.send context req
