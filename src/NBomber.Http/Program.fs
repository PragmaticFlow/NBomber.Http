open System.IO
open System.Reflection
open NBomber.FSharp
open NBomber.Http.FSharp
open System

let private stripMargins (s: string) =
    s.Split('\n')
    |> Array.map (fun x -> x.Trim(' '))
    |> String.concat "\n"

let private printVersion() =
    Assembly.GetEntryAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion
            .ToString()
    |> printfn "nbomber-http v%s"

let private printUsage() =
    sprintf
        "
        -----------------------------

        Usage:
        \tnbomber-http http_scenario.json - run http scenario from the specified file path
        \tnbomber-http [--version|-v]     - print version
        \tnbomber-http [--help|-h]        - print usage"
    |> stripMargins
    |> printfn "%s"

[<EntryPoint>]
let main argv =
    match argv with
    | [| "-v"|] | [|"--version"|] ->
        printVersion()
        0
    | [| "-h"|] | [|"--help"|] ->
        printVersion()
        printUsage()
        0
    | [|jsonPath|] ->
        if jsonPath.StartsWith "-" then
            printfn """Unknown option "%s" """ jsonPath
            1
        elif jsonPath |> File.Exists |> not then
            printfn """File "%s" not found """ jsonPath
            1
        else
            HttpScenario.load jsonPath
            |> NBomberRunner.registerScenario
            |> NBomberRunner.run
            0 // TODO exit code needed
    | _ ->
        printVersion()
        printUsage()
        1
