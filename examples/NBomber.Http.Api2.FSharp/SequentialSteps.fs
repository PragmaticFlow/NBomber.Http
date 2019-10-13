module SequentialSteps

open System
open System.Net.Http
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber.FSharp
open NBomber.Http
open NBomber.Http.Api2.FSharp

let run () =

    let step1 =
        Http.request "https://gitter.im"
        |> HttpStep.withRequest "step0"
        
    let step2 = 
        HttpStep.create("step 1", fun _ ->
            { Http.request "https://gitter.im" with
                Headers = [| "Accept", "text/html" |] }
        )

    let step3 = 
        HttpStep.create("step 2", fun context -> task {
            
            let step1Response = context.GetPreviousStepResponse()            
            let headers = step1Response.Headers
                          |> Seq.map (|KeyValue|)
                          |> Seq.map(fun(k,v) -> k, Seq.head v)
                          |> Array.ofSeq
            let! body = step1Response.Content.ReadAsStringAsync()
            
            return { Http.request "https://github.com" with
                        Method = HttpMethod.Post
                        Headers = [| "Accept", "text/html"
                                     "AcceptLanguage", "de-DE" |]
                                  |> Array.append headers
                        Body = new StringContent(body) }
        })

    let scenario = 
        Scenario.create "test gitter and github" [step1; step2; step3]
        |> Scenario.withConcurrentCopies 1
        |> Scenario.withDuration(TimeSpan.FromSeconds 10.)

    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole