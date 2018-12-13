namespace NBomber.Http

open System.Net.Http

type HttpRequest = {
    Url: string
    Version: string
    Method: string
    Headers: Map<string,string>
    Body: HttpContent
}