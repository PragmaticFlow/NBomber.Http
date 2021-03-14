module SequentialSteps

open System

open System.Net.Http
open FSharp.Control.Tasks.NonAffine

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Plugins.Http.FSharp

let run () =

    let httpFactory = HttpClientFactory.create();

    let step1 =
        Step.create("step 1", clientFactory = httpFactory, exec = fun context ->
            Http.createRequest "GET" "https://gitter.im"
            |> Http.withHeader "Accept" "text/html"
            |> Http.send context
        )

    let step2 =
        Step.create("step 2", clientFactory = httpFactory, exec = fun context -> task {
            let step1Response = context.GetPreviousStepResponse<HttpResponseMessage>()
            let headers = step1Response.Headers
            let! body = step1Response.Content.ReadAsStringAsync()

            return! Http.createRequest "POST" "asdsad"
                    |> Http.withHeader "Accept" "text/html"
                    |> Http.send context
        })

    let scenario =
        Scenario.create "test gitter" [step1; step2]
        |> Scenario.withLoadSimulations [
            InjectPerSec(100, TimeSpan.FromSeconds 10.0)
        ]

    NBomberRunner.registerScenarios [scenario]
    |> NBomberRunner.run
    |> ignore
