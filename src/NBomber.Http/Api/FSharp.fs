namespace NBomber.Plugins.Http.FSharp

open System
open System.Net.Http
open System.Threading.Tasks

open FSharp.Control.Tasks.V2.ContextInsensitive
open Serilog
open Serilog.Events

open NBomber
open NBomber.Contracts
open NBomber.FSharp
open NBomber.Http

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

type HttpStep =

    static member private createMsg (req: HttpRequest) =
        let msg = new HttpRequestMessage()
        msg.Method <- req.Method
        msg.RequestUri <- req.Url
        msg.Version <- req.Version
        msg.Content <- req.Body
        req.Headers |> Map.iter(fun name value -> msg.Headers.TryAddWithoutValidation(name, value) |> ignore)
        msg

    static member private logRequest (logger: ILogger, req: HttpRequestMessage) =

        let body = if isNull(req.Content) then ""
                   else req.Content.ReadAsStringAsync().Result

        logger.Verbose("\n [REQUEST]: \n {0} \n [REQ_BODY] \n {1} \n", req.ToString(), body)

    static member private logResponse (logger: ILogger, res: HttpResponseMessage) =

        let body = if isNull(res.Content) then ""
                   else res.Content.ReadAsStringAsync().Result

        logger.Verbose("\n [RESPONSE]: \n {0} \n [RES_BODY] \n {1} \n", res.ToString(), body)

    static member create (name: string,
                          feed: IFeed<'TFeedItem>,
                          createRequest: IStepContext<unit,'TFeedItem> -> Task<HttpRequest>,
                          completionOption: HttpCompletionOption) =

        let client = new HttpClient()

        Step.create(name, feed, fun context -> task {
            let! req = createRequest(context)
            let msg = HttpStep.createMsg req

            if context.Logger.IsEnabled(LogEventLevel.Verbose) then
                HttpStep.logRequest(context.Logger, msg)

            let! response = client.SendAsync(msg, completionOption, context.CancellationToken)

            if context.Logger.IsEnabled(LogEventLevel.Verbose) then
                HttpStep.logResponse(context.Logger, response)

            let origResSize =
                let headersSize = response.Headers.ToString().Length

                if response.Content.Headers.ContentLength.HasValue then
                   let bodySize = response.Content.Headers.ContentLength.Value |> Convert.ToInt32
                   headersSize + bodySize
                else
                   headersSize

            if req.Check.IsSome then
                let! result = req.Check.Value(response)
                let customResSize = if result.SizeBytes > 0 then result.SizeBytes else origResSize

                if result.Exception.IsNone then
                    return Response.Ok(result.Payload, sizeBytes = customResSize)
                else
                    // todo: add Response.Fail(sizeBytes)
                    return result
            else
                if response.IsSuccessStatusCode then
                    return Response.Ok(response, sizeBytes = origResSize)
                else
                    return Response.Fail("status code: " + response.StatusCode.ToString())
        })

    static member create (name: string,
                          feed: IFeed<'TFeedItem>,
                          createRequest: IStepContext<unit,'TFeedItem> -> HttpRequest,
                          completionOption: HttpCompletionOption) =

        let client = new HttpClient()

        Step.create(name, feed, fun context -> task {
            let req = createRequest(context)
            let msg = HttpStep.createMsg req

            if context.Logger.IsEnabled(LogEventLevel.Verbose) then
                HttpStep.logRequest(context.Logger, msg)

            let! response = client.SendAsync(msg, completionOption, context.CancellationToken)

            if context.Logger.IsEnabled(LogEventLevel.Verbose) then
                HttpStep.logResponse(context.Logger, response)

            let origResSize =
                let headersSize = response.Headers.ToString().Length

                if response.Content.Headers.ContentLength.HasValue then
                   let bodySize = response.Content.Headers.ContentLength.Value |> Convert.ToInt32
                   headersSize + bodySize
                else
                   headersSize

            if req.Check.IsSome then
                let! result = req.Check.Value(response)
                let customResSize = if result.SizeBytes > 0 then result.SizeBytes else origResSize

                if result.Exception.IsNone then
                    return Response.Ok(result.Payload, sizeBytes = customResSize)
                else
                    // todo: add Response.Fail(sizeBytes)
                    return result
            else
                if response.IsSuccessStatusCode then
                    return Response.Ok(response, sizeBytes = origResSize)
                else
                    return Response.Fail("status code: " + response.StatusCode.ToString())
        })

    static member create (name: string,
                          createRequest: IStepContext<unit,unit> -> HttpRequest) =
        HttpStep.create(name, Feed.empty, createRequest, HttpCompletionOption.ResponseHeadersRead)

    static member create (name: string,
                          createRequest: IStepContext<unit,unit> -> Task<HttpRequest>) =
        HttpStep.create(name, Feed.empty, createRequest, HttpCompletionOption.ResponseHeadersRead)

    static member create (name: string,
                          createRequest: IStepContext<unit,unit> -> HttpRequest,
                          completionOption: HttpCompletionOption) =
        HttpStep.create(name, Feed.empty, createRequest, completionOption)

    static member create (name: string,
                          createRequest: IStepContext<unit,unit> -> Task<HttpRequest>,
                          completionOption: HttpCompletionOption) =
        HttpStep.create(name, Feed.empty, createRequest, completionOption)

    static member create (name: string,
                          feed: IFeed<'TFeedItem>,
                          createRequest: IStepContext<unit,'TFeedItem> -> HttpRequest) =
        HttpStep.create(name, feed, createRequest, HttpCompletionOption.ResponseHeadersRead)

    static member create (name: string,
                          feed: IFeed<'TFeedItem>,
                          createRequest: IStepContext<unit,'TFeedItem> -> Task<HttpRequest>) =
        HttpStep.create(name, feed, createRequest, HttpCompletionOption.ResponseHeadersRead)
