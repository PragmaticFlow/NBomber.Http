﻿namespace NBomber.Plugins.Http

open System
open System.Net.Http
open System.Threading.Tasks

open Serilog
open Serilog.Events
open FSharp.Control.Tasks.NonAffine

open NBomber.Contracts

type HttpRequest = {
    Url: Uri
    Version: Version
    Method: HttpMethod
    Headers: Map<string,string>
    Body: HttpContent
    Check: (HttpResponseMessage -> Task<Response>) option
}

module internal HttpUtils =

    let createHttpMsg (req: HttpRequest) =
        let msg = new HttpRequestMessage()
        msg.Method <- req.Method
        msg.RequestUri <- req.Url
        msg.Version <- req.Version
        msg.Content <- req.Body
        req.Headers |> Map.iter(fun name value -> msg.Headers.TryAddWithoutValidation(name, value) |> ignore)
        msg

    let logRequest (logger: ILogger, req: HttpRequestMessage) =

        let body = if isNull(req.Content) then ""
                   else req.Content.ReadAsStringAsync().Result

        logger.Verbose("\n [REQUEST]: \n {0} \n [REQ_BODY] \n {1} \n", req.ToString(), body)

    let logResponse (logger: ILogger, res: HttpResponseMessage) =

        let body = if isNull(res.Content) then ""
                   else res.Content.ReadAsStringAsync().Result

        logger.Verbose("\n [RESPONSE]: \n {0} \n [RES_BODY] \n {1} \n", res.ToString(), body)

    let send (context: IStepContext<HttpClient,'TFeedItem>) (req: HttpRequest) = task {
        let msg = createHttpMsg req

        if context.Logger.IsEnabled(LogEventLevel.Verbose) then
            logRequest(context.Logger, msg)
        try
            let! response = context.Client.SendAsync(msg, context.CancellationToken)

            if context.Logger.IsEnabled(LogEventLevel.Verbose) then
                logResponse(context.Logger, response)

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
                    return Response.ok(result.Payload, sizeBytes = customResSize)
                else
                    // todo: add Response.Fail(sizeBytes)
                    return result
            else
                if response.IsSuccessStatusCode then
                    return Response.ok(response, sizeBytes = origResSize)
                else
                    return Response.fail("status code: " + response.StatusCode.ToString())
        with
        | :? TaskCanceledException when not context.CancellationToken.IsCancellationRequested ->
            return Response.fail("request timed out")
    }
