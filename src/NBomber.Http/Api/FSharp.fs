module NBomber.Http.FSharp.HttpStep

open System
open System.Net.Http

open FSharp.Control.Tasks.V2.ContextInsensitive

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Http

let private createMsg (req: HttpRequest) =
    let msg = new HttpRequestMessage()
    msg.Method <- req.Method
    msg.RequestUri <- req.Url
    msg.Version <- req.Version
    msg.Content <- req.Body
    req.Headers |> Map.iter(fun name value -> msg.Headers.TryAddWithoutValidation(name, value) |> ignore)
    msg

let createRequest (method: string) (url: string) =
    { Url = Uri(url)
      Version = Version.Parse("2.0")
      Method = HttpMethod(method)
      Headers = Map.empty
      Body = Unchecked.defaultof<HttpContent>
      Check = fun response -> response.IsSuccessStatusCode }

let withHeader (name: string) (value: string) (req: HttpRequest) =
    { req with Headers = req.Headers.Add(name, value) }  

let withHeaders (headers: (string*string) list) (req: HttpRequest) =
    { req with Headers = headers |> Map.ofSeq }

let withVersion (version: string) (req: HttpRequest) =
    { req with Version = Version.Parse(version) }     

let withBody (body: HttpContent) (req: HttpRequest) =
    { req with Body = body }

let withCheck (check: HttpResponseMessage -> bool)  (req: HttpRequest) =
    { req with Check = check }

let private pool = ConnectionPool.create("nbomber.http.pool", (fun () -> lazy new HttpClient()))

let build (name: string) (req: HttpRequest) =
    Step.create(name, pool, fun context -> task { 
        let msg = createMsg(req)
        let! response = context.Connection.Value.SendAsync(msg, context.CancellationToken)
        
        let responseSize =
            if response.Content.Headers.ContentLength.HasValue then 
               response.Content.Headers.ContentLength.Value |> Convert.ToInt32
            else
               0

        match req.Check(response) with
        | true  -> return Response.Ok(response, sizeBytes = responseSize) 
        | false -> return Response.Fail()
    })