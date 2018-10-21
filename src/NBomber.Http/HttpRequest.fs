namespace NBomber.Http

type HttpRequest = {
    Url: string
    Version: string
    Method: string
    Headers: Map<string,string>
    Body: string
}