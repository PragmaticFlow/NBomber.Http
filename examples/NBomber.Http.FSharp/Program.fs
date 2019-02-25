open System
open NBomber.FSharp
open NBomber.Http.FSharp

let buildScenario () =

    let step =
        HttpStep.createRequest "GET" "https://www.youtube.com"
        |> HttpStep.withHeader "Accept" "text/html"
        |> HttpStep.withHeader "User-Agent" "Mozilla/5.0"                                         
        |> HttpStep.build "GET request"

        // |> HttpStep.withVersion "2.0"
        // |> HttpStep.withBody(new StringContent ("""{"some":"jsonvalue"}"""))
        // |> HttpStep.withBody(new ByteArrayContent("some byte array"B))

    Scenario.create("test youtube.com", [step])
    |> Scenario.withConcurrentCopies 50
    |> Scenario.withDuration(TimeSpan.FromSeconds 10.0)

[<EntryPoint>]
let main argv =
    buildScenario()
    |> NBomberRunner.registerScenario
    |> NBomberRunner.runInConsole
    0
