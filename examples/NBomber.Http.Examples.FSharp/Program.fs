open System

open NBomber.FSharp
open NBomber.Http.FSharp


let step1 =
    HttpStep.createRequest("GET", "https://www.youtube.com")
    |> HttpStep.withHeaders["Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8"
                            "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36"
                           ]
    // |> HttpStep.withVersion "2.0"
    // |> HttpStep.withBody (new StringContent ("""{"some":"jsonvalue"}"""))
    // |> HttpStep.withBody (new ByteArrayContent("some byte array"B))
    |> HttpStep.build "GET request"

let buildScenario() =
    Scenario.create("test youtube.com", [step1])
    |> Scenario.withConcurrentCopies 100
    |> Scenario.withDuration (TimeSpan.FromSeconds 10.)

[<EntryPoint>]
let main argv =
    buildScenario()
    |> NBomberRunner.registerScenario
    |> NBomberRunner.runInConsole

    0
