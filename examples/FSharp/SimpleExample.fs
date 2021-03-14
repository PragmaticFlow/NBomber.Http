module SimpleExample

open System
open System.Net.Http

open FSharp.Control.Tasks.NonAffine

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Plugins.Http.FSharp

let run () =

    let httpFactory = HttpClientFactory.create()

    let step = Step.create("simple step", clientFactory = httpFactory, exec = fun context ->
        Http.createRequest "GET" "https://nbomber.com"
        |> Http.withHeader "Accept" "text/html"
        |> Http.withBody(new StringContent("{ some JSON }"))
        |> Http.withCheck(fun response -> task {
            return if response.IsSuccessStatusCode then Response.ok()
                   else Response.fail()
        })
        |> Http.send context
    )

    Scenario.create "test gitter" [step]
    |> Scenario.withLoadSimulations [InjectPerSec(rate = 100, during = seconds 30)]
    |> NBomberRunner.registerScenario
    |> NBomberRunner.run
    |> ignore
