open System

open NBomber.FSharp
open NBomber.Http.FSharp

[<EntryPoint>]
let main argv =
    
    let step =
        HttpStep.createRequest "GET" "https://gitter.im"
        |> HttpStep.withHeader "Accept" "text/html"
        |> HttpStep.withHeader "User-Agent" "Mozilla/5.0"                                         
        |> HttpStep.withCheck(fun response -> response.IsSuccessStatusCode) // default check
        |> HttpStep.build "GET request"        

    let scenario = 
        Scenario.create "test gitter" [step]
        |> Scenario.withConcurrentCopies 100
        |> Scenario.withDuration(TimeSpan.FromSeconds 10.0)
            
    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole
    0
