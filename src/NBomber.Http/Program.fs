open System
open NBomber.FSharp
open NBomber.Http.FSharp

[<EntryPoint>]
let main argv =
    match argv with
    | [| jsonFile |] when jsonFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ->
        jsonFile
        |> HttpScenario.load
        |> NBomberRunner.registerScenario
        |> NBomberRunner.runInConsole // TODO make runner overload returning exit code
        0
    | _ ->
        printfn "Usage: dotnet nbomber-http filename.json"
        1

