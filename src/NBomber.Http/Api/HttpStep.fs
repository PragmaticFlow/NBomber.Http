module NBomber.Http.FSharp.HttpStep

open System
open System.Net.Http
open System.Text

open FSharp.Control.Tasks.V2.ContextInsensitive

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Http
open Newtonsoft.Json.Linq

let private createMsg (req: HttpRequest) =
    let notNull a =
        a |> box |> isNull |> not
    let mapOrDefault f defaultValue obj =
        if isNull obj then defaultValue else f obj

    let msg = new HttpRequestMessage()
    msg.RequestUri <- req.Url  |> mapOrDefault Uri msg.RequestUri
    msg.Method  <- req.Method  |> mapOrDefault HttpMethod HttpMethod.Get
    msg.Version <- req.Version |> mapOrDefault Version msg.Version
    msg.Content <-
        if isNull req.Body then null
        else
            match req.Body.Type with
            | JTokenType.Object -> new StringContent(req.Body.ToString(), Encoding.UTF8, "application/json")
            | JTokenType.Null   -> null
            | _                 -> new StringContent(req.Body.ToString())
    if notNull req.Headers then
        for KeyValue(name, value) in req.Headers do
            msg.Headers.TryAddWithoutValidation(name, value) |> ignore
    msg

let createRequest (method: string) (url: string) =
    { Url = url
      Version = "2.0"
      Method = method
      Headers = Map.empty
      Body = JValue.CreateNull() }

let withHeader (name: string) (value: string) (req: HttpRequest) =
    { req with Headers = req.Headers.Add(name, value) }

let withHeaders (headers: (string*string) list) (req: HttpRequest) =
    { req with Headers = headers |> Map.ofSeq }

let withVersion (version: string) (req: HttpRequest) =
    { req with Version = version }

let withBody (body: string) (req: HttpRequest) =
    { req with Body = JToken.op_Implicit body }

let withJsonBody json (req: HttpRequest) =
    { req with Body = JObject.Parse json }

let private pool = ConnectionPool.create("nbomber.http.pool", (fun () -> new HttpClient()), connectionsCount = 1)

let build (name: string) (req: HttpRequest) =
    Step.create(name, pool, fun context -> task {
        let msg = createMsg(req)
        let! response = context.Connection.SendAsync(msg, context.CancellationToken)

        let responseSize =
            response.Content.Headers.ContentLength.GetValueOrDefault()

        match response.IsSuccessStatusCode with
        | true  -> return Response.Ok(response, sizeBytes = int responseSize)
        | false -> return Response.Fail()
    })
