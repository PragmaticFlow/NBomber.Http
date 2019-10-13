namespace NBomber.Http.FSharp

open System
open System.Net.Http
open System.Threading.Tasks

open FSharp.Control.Tasks.V2.ContextInsensitive

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Http

module Http =
    
    let createRequest (method: string) (url: string) =        
        { Url = Uri(url)
          Version = Version.Parse("2.0")
          Method = HttpMethod(method)
          Headers = [||]
          Body = Unchecked.defaultof<HttpContent>
          ResponseCode = ValueNone
          Check = fun _ -> Task.FromResult true }

    let withHeader (name: string) (value: string) (req: HttpRequest) =
        { req with Headers = req.Headers |> Array.append [|(name, value)|] }  

    let withHeaders (headers: (string*string) list) (req: HttpRequest) =
        { req with Headers = headers |> Array.ofSeq }

    let withVersion (version: string) (req: HttpRequest) =
        { req with Version = Version.Parse(version) }     

    let withBody (body: HttpContent) (req: HttpRequest) =
        { req with Body = body }

    let withCheck (check: HttpResponseMessage -> Task<bool>)  (req: HttpRequest) =
        { req with Check = check }

type HttpStep =    

    static member private createMsg (req: HttpRequest) =
        let msg = new HttpRequestMessage()
        msg.Method <- req.Method
        msg.RequestUri <- req.Url
        msg.Version <- req.Version
        msg.Content <- req.Body
        req.Headers |> Array.iter(fun (name,value) -> msg.Headers.TryAddWithoutValidation(name, value) |> ignore)
        msg    
    
    static member create (name: string, createRequest: StepContext<unit> -> Task<HttpRequest>) =
        let client = new HttpClient()
        
        Step.create(name, ConnectionPool.none, fun context -> task {
            let! req = createRequest(context)
            let msg = HttpStep.createMsg req
            let! response = client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)
            
            let isCodeOk =
                match req.ResponseCode with
                | ValueSome code when response.StatusCode = code -> true
                | ValueNone when response.IsSuccessStatusCode -> true
                | _ -> false
                
            if not isCodeOk then
                return Response.Fail()
            else
                let! isResponseOk = req.Check response
                if not isResponseOk then
                    return Response.Fail()
                else
                    let responseSize =
                        let headersSize = response.Headers.ToString().Length
                        
                        if response.Content.Headers.ContentLength.HasValue then
                           let bodySize = int response.Content.Headers.ContentLength.Value
                           headersSize + bodySize
                        else
                           headersSize
                    return Response.Ok(response, sizeBytes = responseSize)
        })
    
    static member create (name: string, createRequest: StepContext<unit> -> HttpRequest) =
        HttpStep.create(name, fun ctx -> Task.FromResult(createRequest ctx))