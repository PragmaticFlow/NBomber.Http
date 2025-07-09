module SequentialSteps

open NBomber
open NBomber.Contracts
open NBomber.Http
open NBomber.Http.FSharp
open NBomber.FSharp
open NBomber.Plugins.Network.Ping

[<CLIMutable>]
type UserData = {
    UserId: int
    Id: int
    Title: string
    Completed: bool
}

let run () =

    use httpClient = Http.createDefaultClient()

    Scenario.create("http_scenario", fun context -> task {

        let! step1 = Step.run("step_1", context, fun _ -> task {

            let! response =
                Http.createRequest "GET" "https://nbomber.com"
                |> Http.send httpClient

            return response
        })

        // example of WithJsonBody
        let! step2 = Step.run("step_2", context, fun _ -> task {

            let user = {| Id = "1"; Name = "Test Name" |}

            let! response =
                Http.createRequest "GET" "https://nbomber.com"
                |> Http.withJsonBody user
                |> Http.send httpClient

            return response
        })

        // example of Http.sendTyped
        let! step3 = Step.run("step_3", context, fun _ -> task {

            let! response =
                Http.createRequest "GET" "https://jsonplaceholder.typicode.com/todos/1"
                |> Http.sendTyped<UserData> httpClient

            return response
        })

        return Response.ok()
    })
    |> Scenario.withWarmUpDuration(seconds 3)
    |> Scenario.withLoadSimulations [Inject(rate = 5, interval = seconds 1, during = minutes 1)]
    |> NBomberRunner.registerScenario
    |> NBomberRunner.withWorkerPlugins [
        new PingPlugin(PingPluginConfig.createDefault "nbomber.com")
        new HttpMetricsPlugin()
    ]
    |> NBomberRunner.run
    |> ignore
