namespace NBomber.Http

open FSharp.Json
open System.Net


type HttpRequest =
    { Url     : string
      Version : string option
      Method  : string option
      Headers : Map<string,string> option
      [<JsonField(AsJson=true)>]
      Body    : string option
      ExpectCode : HttpStatusCode option
    }

type HttpRequestList =
    { Name     : string
      BaseUrl  : string option
      Requests : HttpRequest list
      Duration : int option
      ConcurrentCopies : int option
    }
