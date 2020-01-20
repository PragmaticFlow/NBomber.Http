namespace NBomber.Http

open System
open System.Net.Http
open System.Runtime.CompilerServices
open System.Threading.Tasks
open NBomber.Contracts

type HttpRequest = {
    Url: Uri
    Version: Version
    Method: HttpMethod
    Headers: Map<string,string>
    Body: HttpContent
    Check: (HttpResponseMessage -> Task<bool>) option
}

[<Extension>]
type StepContextExtensions() =

    [<Extension>]
    static member GetPreviousStepResponse(context: StepContext<'T>) =
        context.Data :?> HttpResponseMessage
