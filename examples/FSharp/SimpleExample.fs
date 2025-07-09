module SimpleExample

open System.Net.Http
open NBomber
open NBomber.Contracts
open NBomber.Http
open NBomber.Http.FSharp
open NBomber.FSharp
open NBomber.Plugins.Network.Ping

let run () =

    // For this example, you'll need to start the HttpApiSimulator, which is located in the examples/simulators solution folder.
    // Make sure it’s running before executing the client tests to ensure proper communication.

    use httpClient = Http.createDefaultClient()
    let url = "http://localhost:5071/api/pingpong"

    Scenario.create("http_scenario", fun context -> task {

        let! response =
            Http.createRequest "GET" url
            |> Http.withHeader "Accept" "application/json"
            |> Http.withBody (new StringContent("{ some JSON }"))
            |> Http.send httpClient

        // let user = {| Id = "1"; Name = "Test Name" |}
        //
        // let! response =
        //     Http.createRequest "GET" "https://nbomber.com"
        //     |> Http.withJsonBody user
        //     |> Http.send httpClient

        return response
    })
    |> Scenario.withWarmUpDuration(seconds 3)
    |> Scenario.withLoadSimulations [Inject(rate = 1000, interval = seconds 1, during = minutes 1)]
    |> NBomberRunner.registerScenario
    |> NBomberRunner.withWorkerPlugins [
        new PingPlugin(PingPluginConfig.createDefault "nbomber.com")
        new HttpMetricsPlugin()
    ]
    |> NBomberRunner.run
    |> ignore
