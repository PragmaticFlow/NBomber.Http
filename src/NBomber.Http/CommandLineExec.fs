namespace NBomber.Http.CommandLine

open System
open CommandLine

open NBomber.Contracts
open NBomber.FSharp
open NBomber.Plugins.Http.FSharp

type HttpHeader(value: string) =
    // validation
    do value.Split(':').[0].Trim() |> ignore
       value.Split(':').[1].Trim() |> ignore

    member x.Name = value.Split(':').[0].Trim()
    member x.Value = value.Split(':').[1].Trim()

type CommandLineArgs = {
    [<Option('r', "requests", HelpText = "number of HTTP requests which will be send in second")>] Requests: int
    [<Option('d', "duration", HelpText = "duration of the test in minutes")>] Duration: float
    [<Option('h', "headers", HelpText = "HTTP header to add to request, e.g. \"Accept: text/html\"")>] Headers: HttpHeader seq
    [<Option('u', "urls", Required = true, HelpText = "URL www.example.com")>] Urls: Uri seq
}

module CommandLineExec =

    let exec (args: string[]) =
        let result = CommandLine.Parser.Default.ParseArguments<CommandLineArgs>(args)
        match result with
        | :? Parsed<CommandLineArgs> as parsed ->

            let values = parsed.Value

            let requests =
                if values.Requests > 0 then values.Requests
                else 200

            let duration =
                if values.Duration > 0.0 then values.Duration
                else 0.3
                |> TimeSpan.FromMinutes

            let headers =
                values.Headers
                |> Seq.map(fun x -> x.Name, x.Value)
                |> Seq.toList

            parsed.Value.Urls
            |> Seq.map(fun url ->
                let req = Http.createRequest "GET" url.AbsoluteUri
                          |> Http.withHeaders headers

                HttpStep.create(url.AbsoluteUri, fun _ -> req))

            |> Seq.mapi(fun i step ->
                let name = sprintf "http test %i" i
                Scenario.create name [step]
                |> Scenario.withWarmUpDuration(TimeSpan.FromSeconds 5.0)
                |> Scenario.withLoadSimulations [
                    InjectScenariosPerSec(requests, duration)
                ]
            )
            |> Seq.toList
            |> NBomberRunner.registerScenarios
            |> NBomberRunner.run
            |> ignore

        | :? NotParsed<CommandLineArgs> as notParsed -> ()
        | _ -> ()
