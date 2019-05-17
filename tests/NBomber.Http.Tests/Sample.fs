module Tests

open Expecto
open NBomber.Http
open Newtonsoft.Json.Linq
open FSharp.Json

let json = """{
    "name": "Test HTTP Scenario",
    "baseUrl": "http://localhost:55042",
    "requests": [
        {
            "url": "/api/ticket"
        },
        {
            "url" : "/api/ticket/",
            "method": "POST",
            "body": { "id":1, "title": "Test ticket 1" }
        },
        {
            "url" : "/api/ticket/1/text",
            "method": "PUT",
            "body": "Text content",
            "headers": {
                "ContentType": "application/text",
                "Authorization": "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ=="
            }
        },
        {
            "url": "/api/ticket/1",
            "method": "DELETE"
        }
    ]
}"""

// type HttpRequest2 =
//     { Url     : string

//       Version : string
//       Method  : string
//       Headers : Map<string,string>
//       Body    : JToken
//     }

// type HttpRequestList2 =
//     { Name     : string
//       BaseUrl  : string
//       Requests : HttpRequest2 list
//     }

let expected =
    { Name =  "Test HTTP Scenario"
      BaseUrl = "http://localhost:55042"
      Requests =
        [ { Url = "/api/ticket"
            Version = null
            Method = null
            Headers = unbox null
            Body = null
          }
          { Url = "/api/ticket/"
            Version = null
            Method = "POST"
            Headers = unbox null
            Body = JObject.Parse """{ "id":1, "title": "Test ticket 1" }"""
          }
          { Url = "/api/ticket/1/text"
            Version = null
            Method = "PUT"
            Headers =
               [ "Authorization", "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ=="
                 "ContentType", "application/text"
               ] |> Map.ofList
            Body = JValue.CreateNull()
          }
          { Url = "/api/ticket/1"
            Version = null
            Method = "DELETE"
            Headers = unbox null
            Body = null
          }]
    }
[<Tests>]
let tests =
  testList "json serializers" [
    // testCase "newtonsoft" <| fun _ ->
    //   let actual = Newtonsoft.Json.JsonConvert.DeserializeObject<HttpRequestList> json
    //   Expect.equal actual expected "I compute, therefore I am."
    testCase "FSharp.Json" <| fun _ ->
      let config = JsonConfig.create(jsonFieldNaming = Json.lowerCamelCase)
      let actual = Json.deserializeEx<HttpRequestList> config json
      Expect.equal actual expected "I compute, therefore I am."
  ]
