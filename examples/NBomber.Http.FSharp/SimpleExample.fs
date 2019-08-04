module SimpleExample

open System
open NBomber.FSharp
open NBomber.Http.FSharp

let run () =
    
    let step = 
        HttpStep.create "simple step" (
            Http.createRequest "GET" "https://gitter.im"
            |> Http.withHeader "Accept" "text/html"        
            |> Http.withCheck(fun response -> response.IsSuccessStatusCode) // default check
        )

    let scenario = 
        Scenario.create "test gitter" [step]
        |> Scenario.withConcurrentCopies 100
        |> Scenario.withDuration(TimeSpan.FromSeconds 10.0)
            
    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole