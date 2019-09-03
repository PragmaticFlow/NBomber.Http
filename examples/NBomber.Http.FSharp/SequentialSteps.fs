module SequentialSteps

open System
open System.Net.Http
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber.FSharp
open NBomber.Http.FSharp

let run () =

    let step1 = 
        HttpStep.create "step 1" (fun context ->
            Http.createRequest "GET" "https://gitter.im" 
            |> Http.withHeader "Accept" "text/html"
            |> Task.FromResult
        )

    let step2 = 
        HttpStep.create "step 2" (fun context -> task {
            
            let step1Response = context.Data :?> HttpResponseMessage
            
            let! content = step1Response.Content.ReadAsStringAsync()
            
            return Http.createRequest "POST" "asdsad"
                   |> Http.withHeader "Accept" "text/html"
        })

    let scenario = 
        Scenario.create "test gitter" [step1; step2]
        |> Scenario.withConcurrentCopies 100
        |> Scenario.withDuration(TimeSpan.FromSeconds 10.0)

    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole