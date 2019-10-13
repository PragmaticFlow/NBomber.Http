module SimpleExample

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks
open NBomber.FSharp
open NBomber.Http.Api2.FSharp

let run () =
    let step1 =
        Http.request "https://github.com"
        |> HttpStep.withRequest "github step"
    let step2 =
        HttpStep.create("gitter step", fun context -> 
            { Http.request "https://gitter.im" with
                Method = HttpMethod.Post
                Headers = [| "Accept", "text/html" |]
//                Body = new StringContent ("""{"someJson":"Content"}""")
//                Version = Version "1.1"
//                Check = (fun response -> response.IsSuccessStatusCode |> Task.FromResult) // default check
            })
            
    let scenario = 
        Scenario.create "test gitter" [step1;step2]
        |> Scenario.withConcurrentCopies 100
        |> Scenario.withDuration (TimeSpan.FromSeconds 10.0)
            
    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole