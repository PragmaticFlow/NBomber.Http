module NBomber.Http.FSharp.HttpScenario

open System
open NBomber.FSharp
open NBomber.Http
open Thoth.Json.Net

let private toStep request =
    request
    |> HttpStep.build (sprintf "%A %s" request.Method request.Url)

let private toScenario requestList =
    let steps =
        requestList.Requests
        |> List.map (fun request ->
            let stepName = sprintf "%A %s" request.Method request.Url
            { request with Url = requestList.BaseUrl + request.Url }
            |> HttpStep.build stepName
        )

    Scenario.create(requestList.Name, steps)
    |> Scenario.withConcurrentCopies 50
    |> Scenario.withDuration (TimeSpan.FromSeconds 10.0)

/// Loads scenario with HTTP requests from a file
let load json =
    json
    |> System.IO.File.ReadAllText
    |> Decode.fromString HttpRequestList.Decoder
    |> Result.map toScenario
    |> function
       | Ok scenario -> scenario
       | Error err -> failwith err
