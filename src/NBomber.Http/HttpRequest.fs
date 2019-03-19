namespace NBomber.Http

open System
open System.Net.Http
open Thoth.Json.Net

type HttpRequest =
    { Url     : string
      Version : Version
      Method  : HttpMethod
      Headers : Map<string,string>
      Body    : HttpContent
    }
    static member Decoder : Decode.Decoder<HttpRequest> =
        Decode.object
            (fun get ->
                { Url     = get.Required.Field "url" Decode.string
                  Version = get.Optional.Field "version" Decode.string
                            |> Option.map Version
                            |> Option.toObj
                  Method  = get.Optional.Field "method" Decode.string
                            |> Option.map HttpMethod
                            |> Option.defaultValue HttpMethod.Get
                  Headers = get.Optional.Field "headers" (Decode.dict Decode.string)
                            |> Option.defaultValue Map.empty
                  Body    = get.Optional.Field "body" Decode.string
                            |> Option.map (fun content -> new StringContent(content))
                            |> Option.toObj
                }
            )

type HttpRequestList =
    { Name     : string
      BaseUrl  : string
      Requests : HttpRequest list
    }
    static member Decoder : Decode.Decoder<HttpRequestList> =
        Decode.object
            (fun get ->
                { Name     = get.Required.Field "name" Decode.string
                  BaseUrl  = get.Optional.Field "baseUrl" Decode.string
                             |> Option.defaultValue ""
                  Requests = get.Required.Field "requests" (Decode.list HttpRequest.Decoder)
                }
            )
