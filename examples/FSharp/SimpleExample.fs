module SimpleExample

open System.Net.Http
open NBomber
open NBomber.Contracts
open NBomber.FSharp
open NBomber.Http.FSharp

let run () =

    use httpClient = new HttpClient()

    Scenario.create("http_scenario", fun context -> task {

        let! response =
            Http.createRequest "GET" "https://nbomber.com"
            |> Http.withHeader "Accept" "text/html"
            |> Http.withBody (new StringContent("{ some JSON }"))
            |> Http.send httpClient

        return response
    })
    |> Scenario.withLoadSimulations [Inject(rate = 100, interval = seconds 1, during = seconds 30)]
    |> NBomberRunner.registerScenario
    |> NBomberRunner.run
    |> ignore
