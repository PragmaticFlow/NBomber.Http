module SequentialSteps

open System
open FSharp.Control.Tasks.V2.ContextInsensitive
open NBomber.FSharp
open NBomber.Http
open NBomber.Http.FSharp

let run () =

    let step1 = 
        HttpStep.create("step 1", fun context ->
            Http.createRequest "GET" "https://gitter.im" 
            |> Http.withHeader "Accept" "text/html"            
        )

    let step2 = 
        HttpStep.create("step 2", fun context -> task {
            
            let step1Response = context.GetPreviousStepResponse()            
            let headers = step1Response.Headers
            let! body = step1Response.Content.ReadAsStringAsync()
            
            return Http.createRequest "POST" "asdsad"
                   |> Http.withHeader "Accept" "text/html"
        })

    let scenario = 
        Scenario.create "test gitter" [step1; step2]
        |> Scenario.withConcurrentCopies 1
        |> Scenario.withDuration(TimeSpan.FromSeconds 10.0)

    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole