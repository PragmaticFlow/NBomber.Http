namespace NBomber.Http.FSharp

open System
open System.Net.Http
open System.Threading.Tasks

open FSharp.Control.Tasks.V2.ContextInsensitive

open NBomber
open NBomber.Contracts
open NBomber.FSharp
open NBomber.Http

module Http =

    let createRequest (method: string) (url: string) =
        { Url = Uri(url)
          Version = Version.Parse("2.0")
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

    let withCheck (check: HttpResponseMessage -> Task<bool>)  (req: HttpRequest) =
        { req with Check = Some check }

type HttpStep =

    static member private createMsg (req: HttpRequest) =
        let msg = new HttpRequestMessage()
        msg.Method <- req.Method
        msg.RequestUri <- req.Url
        msg.Version <- req.Version
        msg.Content <- req.Body
        req.Headers |> Map.iter(fun name value -> msg.Headers.TryAddWithoutValidation(name, value) |> ignore)
        msg

    static member create (name: string, feed: IFeed<'TFeedItem>, createRequest: StepContext<unit,'TFeedItem> -> Task<HttpRequest>) =
        let client = new HttpClient()

        Step.create(name, feed, fun context -> task {
            let! req = createRequest(context)
            let msg = HttpStep.createMsg req
            let! response = client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)

            let responseSize =
                let headersSize = response.Headers.ToString().Length

                if response.Content.Headers.ContentLength.HasValue then
                   let bodySize = response.Content.Headers.ContentLength.Value |> Convert.ToInt32
                   headersSize + bodySize
                else
                   headersSize

            if req.Check.IsSome then
                match! req.Check.Value(response) with
                | true  -> return Response.Ok(response, sizeBytes = responseSize)
                | false -> return Response.Fail()
            else
                if response.IsSuccessStatusCode then
                    return Response.Ok(response, sizeBytes = responseSize)
                else
                    return Response.Fail()
        })

    static member create (name: string, feed: IFeed<'TFeedItem>, createRequest: StepContext<unit,'TFeedItem> -> HttpRequest) =

        let client = new HttpClient()

        Step.create(name, feed, fun context -> task {
            let req = createRequest(context)
            let msg = HttpStep.createMsg req
            let! response = client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)

            let responseSize =
                let headersSize = response.Headers.ToString().Length

                if response.Content.Headers.ContentLength.HasValue then
                   let bodySize = response.Content.Headers.ContentLength.Value |> Convert.ToInt32
                   headersSize + bodySize
                else
                   headersSize

            if req.Check.IsSome then
                match! req.Check.Value(response) with
                | true  -> return Response.Ok(response, sizeBytes = responseSize)
                | false -> return Response.Fail(String.Format("step:'{0}' check has failed"))
            else
                if response.IsSuccessStatusCode then
                    return Response.Ok(response, sizeBytes = responseSize)
                else
                    return Response.Fail(response.ToString())
        })

    static member create (name: string, createRequest: StepContext<unit,unit> -> Task<HttpRequest>) =
        HttpStep.create(name, Feed.empty, createRequest)

    static member create (name: string, createRequest: StepContext<unit,unit> -> HttpRequest) =
        HttpStep.create(name, Feed.empty, createRequest)
