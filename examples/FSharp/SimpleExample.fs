module SimpleExample

open System.Net.Http
open NBomber
open NBomber.Contracts
open NBomber.FSharp
open NBomber.Http.FSharp

let run () =

    use httpClient = new HttpClient()

    Scenario.create("http_scenario", fun context -> task {

        let request = new HttpRequestMessage(HttpMethod.Get, "https://nbomber.com")

        let! response = httpClient.SendAsync request

        let dataSize = Http.getRequestSize(request) + Http.getResponseSize(response)

        return
            if response.IsSuccessStatusCode then
                Response.ok(statusCode = response.StatusCode.ToString(), sizeBytes = dataSize)
            else
                Response.fail(statusCode = response.StatusCode.ToString(), sizeBytes = dataSize)
    })
    |> Scenario.withLoadSimulations [InjectPerSec(rate = 100, during = seconds 30)]
    |> NBomberRunner.registerScenario
    |> NBomberRunner.run
    |> ignore
