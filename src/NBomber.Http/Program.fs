open NBomber.Http.CommandLine

[<EntryPoint>]
let main args =
    CommandLineExec.exec(args)
    0