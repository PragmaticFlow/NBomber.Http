namespace NBomber.Http

open FSharp.Json


type HttpRequest =
    { Url     : string
      Version : string option
      Method  : string option
      Headers : Map<string,string> option
      [<JsonField(AsJson=true)>]
      Body    : string option
    }

type HttpRequestList =
    { Name     : string
      BaseUrl  : string option
      Requests : HttpRequest list
      Duration : int option
      ConcurrentCopies : int option
    }
