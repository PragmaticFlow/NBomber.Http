module SequentialSteps

open System.Net.Http
open NBomber.FSharp
open NBomber.Http.FSharp

let run () =

    use httpClient = new HttpClient()

    Scenario.create("http_scenario", fun context -> task {

        let! step1 = Step.run("step_1", context, fun () -> task {
            let request = new HttpRequestMessage(HttpMethod.Get, "https://nbomber.com")

            let! response = Http.send httpClient request

            return response
        })

        let! step2 = Step.run("step_2", context, fun () -> task {
            let request = new HttpRequestMessage(HttpMethod.Get, "https://nbomber.com")

            let! response = Http.send httpClient request

            return response
        })

        return Response.ok()
    })
    |> NBomberRunner.registerScenario
    |> NBomberRunner.run
    |> ignore
