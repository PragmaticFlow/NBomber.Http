module SimpleExample

open System.Net.Http
open NBomber
open NBomber.Contracts
open NBomber.Http
open NBomber.Http.FSharp
open NBomber.FSharp

let run () =

    // let socketsHandler = new SocketsHttpHandler(MaxConnectionsPerServer = 5)
    // use httpClient = new HttpClient(socketsHandler)

    use httpClient = new HttpClient()

    Scenario.create("http_scenario", fun context -> task {

        let! response =
            Http.createRequest "GET" "https://nbomber.com"
            |> Http.withHeader "Accept" "text/html"
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
    |> Scenario.withoutWarmUp
    |> Scenario.withLoadSimulations [Inject(rate = 100, interval = seconds 1, during = minutes 1)]
    |> NBomberRunner.registerScenario
    |> NBomberRunner.withWorkerPlugins [new HttpMetricsPlugin()]
    |> NBomberRunner.run
    |> ignore
