module SequentialSteps

open System
open NBomber.FSharp
open NBomber.Http.FSharp

let run () =

    let step1 = 
        HttpStep.create "step 1" (
            Http.createRequest "GET" "https://gitter.im" 
            |> Http.withHeader "Accept" "text/html"
        )

    let step2 = 
        HttpStep.createFromResponse "step 2" (fun response -> 
            Http.createRequest "POST" "asdsad"
            |> Http.withHeader "Accept" "text/html"
        )

    let scenario = 
        Scenario.create "test gitter" [step1; step2]
        |> Scenario.withConcurrentCopies 100
        |> Scenario.withDuration(TimeSpan.FromSeconds 10.0)

    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole