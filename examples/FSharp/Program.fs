open System
open System.Diagnostics.Tracing
open System.Threading.Tasks

[<EntryPoint>]
let main argv =

    SimpleExample.run()
    // SequentialSteps.run()
    
    0
