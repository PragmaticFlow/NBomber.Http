namespace NBomber.Http

open Newtonsoft.Json.Linq


type HttpRequest =
    { Url     : string
      Version : string
      Method  : string
      Headers : Map<string,string>
      Body    : JToken
    }

type HttpRequestList =
    { Name     : string
      BaseUrl  : string
      Requests : HttpRequest list
    }
