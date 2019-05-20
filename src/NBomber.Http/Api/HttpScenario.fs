module NBomber.Http.FSharp.HttpScenario

open System
open System.IO
open NBomber.FSharp
open NBomber.Http
open FSharp.Json

let private toScenario httpScenario =
    httpScenario.Requests
    |> List.map (fun request ->
        let method = defaultArg request.Method "GET"
        let url = defaultArg httpScenario.BaseUrl "" + request.Url
        let stepName = sprintf "%s %s" method url
        { request with Url = url }
        |> HttpStep.build stepName
    )
    |> Scenario.create httpScenario.Name
    |> Scenario.withConcurrentCopies (defaultArg httpScenario.ConcurrentCopies 50)
    |> Scenario.withDuration (defaultArg httpScenario.Duration 10 |> float |> TimeSpan.FromSeconds)

/// Deserializes scenario with HTTP requests from a json string
let deserialize json =
      let config = JsonConfig.create(jsonFieldNaming = Json.lowerCamelCase)
      Json.deserializeEx<HttpRequestList> config json

/// Loads scenario with HTTP requests from a file
let load path =
    path
    |> File.ReadAllText
    |> deserialize
    |> toScenario
