module NBomber.Http.FSharp.HttpStep

open System
open System.Net.Http
open System.Text

open FSharp.Control.Tasks.V2.ContextInsensitive

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Http

let private createMsg (req: HttpRequest) =
    let msg = new HttpRequestMessage()
    msg.RequestUri <- Uri req.Url
    msg.Version <- defaultArg req.Version "2.0" |> Version
    msg.Content <-
        match req.Body with
        | None -> null
        | Some body ->
            if body.StartsWith "{" then
                new StringContent(body, Encoding.UTF8, "application/json")
            else
                new StringContent(body)
    msg.Method <-
        match req.Method, req.Body with
        | Some method, _ -> HttpMethod method
        | None, Some _   -> HttpMethod.Post
        | None, None     -> HttpMethod.Get
    match req.Headers with
    | Some headers ->
        for KeyValue(name, value) in headers do
            msg.Headers.TryAddWithoutValidation(name, value) |> ignore
    | None -> ()
    msg

let createRequest (method: string) (url: string) =
    { Url = url
      Version = Some "2.0"
      Method = Some method
      Headers = None
      Body = None }

let private addHeader map name value =
    match map with
    | None -> [name,value] |> Map.ofList |> Some
    | Some map -> Map.add name value map |> Some
let withHeader (name: string) (value: string) (req: HttpRequest) =
    { req with Headers = addHeader req.Headers name value }

let withHeaders (headers: (string*string) list) (req: HttpRequest) =
    { req with Headers = req.Headers
                         |> Option.defaultValue Map.empty
                         |> Map.toList
                         |> List.append headers
                         |> Map.ofList
                         |> Some }

let withVersion (version: string) (req: HttpRequest) =
    { req with Version = Some version }

let withBody (body: string) (req: HttpRequest) =
    { req with Body = Some body }

let private pool = ConnectionPool.create("nbomber.http.pool", (fun () -> new HttpClient()), connectionsCount = 1)

let build (name: string) (req: HttpRequest) =
    Step.create(name, pool, fun context -> task {
        let msg = createMsg req
        let! response = context.Connection.SendAsync(msg, context.CancellationToken)
        let responseSize = response.Content.Headers.ContentLength.GetValueOrDefault()
        match response.IsSuccessStatusCode with
        | true  -> return Response.Ok(response, sizeBytes = int responseSize)
        | false -> return Response.Fail()
    })
