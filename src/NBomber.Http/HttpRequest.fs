namespace NBomber.Http

open System
open System.Net
open System.Net.Http
open System.Runtime.CompilerServices
open System.Threading.Tasks
open NBomber.Contracts

module Version =
    let defaultVersion = Version.Parse "2.0"
    let parse v =
        if isNull v then defaultVersion
        else
            match Version.TryParse v with
            | true, validVersion -> validVersion
            | _ -> defaultVersion
            
module Map =
    let ofStructTuples (values : struct(string*string) array) =
        let mutable result = Map.empty
        if isNull values then result else
        for struct(name,value) in values do
            result <- Map.add name value result
        result
        
type HttpRequest = {
    Url: Uri
    Version: Version
    Method: HttpMethod
    Headers: (string*string) array  
    Body: HttpContent
    ResponseCode : HttpStatusCode ValueOption
    Check: HttpResponseMessage -> Task<bool>
}

[<Extension>]
type StepContextExtensions() =
    
    [<Extension>]
    static member GetPreviousStepResponse(context: StepContext<'T>) =
        context.Data :?> HttpResponseMessage