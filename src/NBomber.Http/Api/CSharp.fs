namespace NBomber.Plugins.Http.CSharp

open System
open System.Net.Http
open System.Threading.Tasks
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

open NBomber
open NBomber.Contracts
open NBomber.Http
open NBomber.Plugins.Http.FSharp

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
    static member WithCheck(req: HttpRequest, check: System.Func<HttpResponseMessage,Task<Response>>) =
        req |> Http.withCheck(fun response -> check.Invoke(response))

type HttpStep =

    static member Create
        (name: string,
         createRequest: Func<IStepContext<Unit,Unit>, HttpRequest>,
         [<Optional; DefaultParameterValue(HttpCompletionOption.ResponseHeadersRead:HttpCompletionOption)>] completionOption: HttpCompletionOption,
         [<Optional; DefaultParameterValue(Nullable<TimeSpan>())>] timeout: Nullable<TimeSpan>) =

        HttpStep.create(name, createRequest = createRequest.Invoke, completionOption = completionOption, ?timeout = (Option.ofNullable timeout))

    static member CreateAsync
        (name: string,
         createRequest: Func<IStepContext<Unit,Unit>, Task<HttpRequest>>,
         [<Optional; DefaultParameterValue(HttpCompletionOption.ResponseHeadersRead:HttpCompletionOption)>] completionOption: HttpCompletionOption,
         [<Optional; DefaultParameterValue(Nullable<TimeSpan>())>] timeout: Nullable<TimeSpan>) =

        HttpStep.createAsync(name, createRequest = createRequest.Invoke, completionOption = completionOption, ?timeout = (Option.ofNullable timeout))

    static member Create
        (name: string,
         feed: IFeed<'TFeedItem>,
         createRequest: Func<IStepContext<unit,'TFeedItem>,HttpRequest>,
         [<Optional; DefaultParameterValue(HttpCompletionOption.ResponseHeadersRead:HttpCompletionOption)>] completionOption: HttpCompletionOption,
         [<Optional; DefaultParameterValue(Nullable<TimeSpan>())>] timeout: Nullable<TimeSpan>) =

        HttpStep.create(name, feed = feed, createRequest = createRequest.Invoke, completionOption = completionOption, ?timeout = (Option.ofNullable timeout))

    static member CreateAsync
        (name: string,
         feed: IFeed<'TFeedItem>,
         createRequest: Func<IStepContext<unit,'TFeedItem>,Task<HttpRequest>>,
         [<Optional; DefaultParameterValue(HttpCompletionOption.ResponseHeadersRead:HttpCompletionOption)>] completionOption: HttpCompletionOption,
         [<Optional; DefaultParameterValue(Nullable<TimeSpan>())>] timeout: Nullable<TimeSpan>) =

        HttpStep.createAsync(name, feed = feed, createRequest = createRequest.Invoke, completionOption = completionOption, ?timeout = (Option.ofNullable timeout))
