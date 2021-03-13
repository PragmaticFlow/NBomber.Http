open NBomber.Plugins.Http.CommandLine

[<EntryPoint>]
let main args =
    CommandLineExec.exec(args)
    0
