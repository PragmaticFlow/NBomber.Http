namespace NBomber.Http

open System
open System.Net.Http

type HttpRequest = {
    Url: Uri
    Version: Version
    Method: HttpMethod
    Headers: Map<string,string>    
    Body: HttpContent
}