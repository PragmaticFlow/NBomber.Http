namespace NBomber.Http

open System
open System.Net.Http
open System.Threading.Tasks
open NBomber.Contracts

type HttpRequest = {
    Url: Uri
    Version: Version
    Method: HttpMethod
    Headers: Map<string,string>
    Body: HttpContent
    Check: (HttpResponseMessage -> Task<Response>) option
}
