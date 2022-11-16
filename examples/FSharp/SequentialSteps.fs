module SequentialSteps

open System.Net.Http
open NBomber.FSharp
open NBomber.Http.FSharp

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
    |> NBomberRunner.run
    |> ignore
