module NBomber.Http.FSharp.HttpScenario

open System
open System.IO
open NBomber.FSharp
open NBomber.Http
open Newtonsoft.Json

let private toScenario requestList =
    requestList.Requests
    |> List.map (fun request ->
        let method = if isNull request.Method then "GET" else request.Method
        let stepName = sprintf "%s %s" method request.Url
        { request with Url = requestList.BaseUrl + request.Url }
        |> HttpStep.build stepName
    )
    |> Scenario.create requestList.Name
    |> Scenario.withConcurrentCopies 50
    |> Scenario.withDuration (TimeSpan.FromSeconds 10.0)

/// Loads scenario with HTTP requests from a file
let load json =
    json
    |> File.ReadAllText
    |> JsonConvert.DeserializeObject<HttpRequestList>
    |> toScenario
