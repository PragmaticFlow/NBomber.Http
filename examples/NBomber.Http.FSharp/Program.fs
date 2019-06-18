open System

open NBomber.FSharp
open NBomber.Http.FSharp

let buildScenario () =

    let step =
        HttpStep.createRequest "GET" "https://gitter.im"
        |> HttpStep.withHeader "Accept" "text/html"
        |> HttpStep.withHeader "User-Agent" "Mozilla/5.0"                                         
        |> HttpStep.withCheck(fun response -> response.IsSuccessStatusCode) // default check
        |> HttpStep.build "GET request"

        // |> HttpStep.withVersion "2.0"
        // |> HttpStep.withBody(new StringContent ("""{"some":"jsonvalue"}"""))
        // |> HttpStep.withBody(new ByteArrayContent("some byte array"B))

    Scenario.create "test gitter" [step]
    |> Scenario.withConcurrentCopies 100
    |> Scenario.withDuration(TimeSpan.FromSeconds 10.0)

[<EntryPoint>]
let main argv =
    let scenario = buildScenario()
    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.runInConsole
    0
