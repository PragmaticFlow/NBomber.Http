namespace rec NBomber.Http

open System
open System.Net.Http

open NBomber
open FSharp.Control.Tasks.V2.ContextInsensitive

type HttpRequest = {
    Url: string
    Version: string
    Method: string
    Headers: Map<string,string>
    Body: string
} with
  static member Create(method: string, url: string) = FSharpAPI.httpRequest(method, url)     
  member x.WithHeader(name: string, value: string) = FSharpAPI.withHeader(name, value) x    
  member x.WithVersion(input: string) = FSharpAPI.withVersion(input) x  
  member x.BuildStep() = FSharpAPI.buildStep(x)


module FSharpAPI =

    let httpRequest (method: string, url: string) =
        { Url = url; Version = "1.1"; Method = method
          Headers = Map.empty; Body = "" }

    let withHeader (name: string, value: string) (req: HttpRequest) =
        { req with Headers = req.Headers.Add(name, value) }  

    let withVersion (input: string) (req: HttpRequest) =
        { req with Version = input }     

    let buildStep (httpReq: HttpRequest) =
        let httpClient = new HttpClient()
        let name = httpReq.Method + " " + httpReq.Url

        Step.Create(name, fun req -> task { 
            let msg = createMsg(httpReq)
            let! response = httpClient.SendAsync(msg)                                            
            match response.IsSuccessStatusCode with
            | true -> return Response.Ok(response) 
            | false -> return Response.Fail(response.StatusCode.ToString()) 
        })

    let private createMsg (req: HttpRequest) =
        let msg = new HttpRequestMessage()
        msg.Method <- HttpMethod(req.Method)
        msg.RequestUri <- Uri(req.Url)
        msg.Version <- Version.Parse(req.Version)
        req.Headers |> Map.iter(fun name value -> msg.Headers.TryAddWithoutValidation(name, value) |> ignore)
        msg