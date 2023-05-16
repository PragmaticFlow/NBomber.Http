module SequentialSteps

open System.Net.Http
open NBomber.Http
open NBomber.Http.FSharp
open NBomber.FSharp

let run () =

    use httpClient = new HttpClient()

    Scenario.create("http_scenario", fun context -> task {

        let! step1 = Step.run("step_1", context, fun _ -> task {

            let! response =
                Http.createRequest "GET" "https://nbomber.com"
                |> Http.send httpClient

            return response
        })

        let! step2 = Step.run("step_2", context, fun _ -> task {

            let! response =
                Http.createRequest "GET" "https://nbomber.com"
                |> Http.send httpClient

            return response
        })

        return Response.ok()
    })
    |> NBomberRunner.registerScenario
    |> NBomberRunner.withWorkerPlugins [new HttpMetricsPlugin()]
    |> NBomberRunner.run
    |> ignore
