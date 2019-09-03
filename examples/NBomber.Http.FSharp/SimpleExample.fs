module SimpleExample

open System
open System.Text
open System.Threading.Tasks
open System.Net.Http

open NBomber.FSharp
open NBomber.Http.FSharp

let run () =
    
    let step = 
        HttpStep.create "simple step" (fun context ->                
            Http.createRequest "GET" "https://gitter.im"
            |> Http.withHeader "Accept" "text/html"
            //|> Http.withBody(new StringContent("{ some JSON }", Encoding.UTF8, "application/json"))
            //|> Http.withVersion("1.1")            
            //|> Http.withCheck(fun response -> response.IsSuccessStatusCode |> Task.FromResult) // default check
            |> Task.FromResult
        )

    let scenario = 
        Scenario.create "test gitter" [step]
        |> Scenario.withConcurrentCopies 100
        |> Scenario.withDuration(TimeSpan.FromSeconds 10.0)
            
    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole