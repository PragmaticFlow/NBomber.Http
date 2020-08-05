namespace NBomber.Http.CommandLine

open System
open CommandLine

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Plugins.Network.Ping
open NBomber.Plugins.Http.FSharp

type HttpHeader(value: string) =
    // validation
    do value.Split(':').[0].Trim() |> ignore
       value.Split(':').[1].Trim() |> ignore

    member x.Name = value.Split(':').[0].Trim()
    member x.Value = value.Split(':').[1].Trim()

type CommandLineArgs = {
    [<Option('r', "rate", HelpText = "request rate per second")>] RequestRate: int
    [<Option('d', "duration", HelpText = "duration of the test in minutes")>] Duration: float
    [<Option('h', "headers", HelpText = "HTTP header to add to request, e.g. \"Accept: text/html\"")>] Headers: HttpHeader seq
    [<Option('u', "url", Required = true, HelpText = "URL www.example.com")>] Url: Uri
}

module CommandLineExec =

    let exec (args: string[]) =
        let result = CommandLine.Parser.Default.ParseArguments<CommandLineArgs>(args)
        match result with
        | :? Parsed<CommandLineArgs> as parsed ->

            let values = parsed.Value

            let rate =
                if values.RequestRate > 0 then values.RequestRate
                else 200

            let duration =
                if values.Duration > 0.0 then values.Duration
                else 0.3
                |> TimeSpan.FromMinutes

            let headers =
                values.Headers
                |> Seq.map(fun x -> x.Name, x.Value)
                |> Seq.toList

            let pingPluginConfig = PingPluginConfig.CreateDefault [values.Url.Host]
            use pingPlugin = new PingPlugin(pingPluginConfig)

            let step = HttpStep.create("send request", fun _ ->
                Http.createRequest "GET" values.Url.AbsoluteUri
                |> Http.withHeaders headers
            )

            Scenario.create "http scenario" [step]
            |> Scenario.withWarmUpDuration(seconds 5)
            |> Scenario.withLoadSimulations [
                InjectPerSec(rate, duration)
            ]
            |> NBomberRunner.registerScenario
            |> NBomberRunner.withPlugins [pingPlugin]
            |> NBomberRunner.run
            |> ignore

        | :? NotParsed<CommandLineArgs> as notParsed -> ()
        | _ -> ()
