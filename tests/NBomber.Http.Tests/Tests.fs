module Tests

open Expecto
open NBomber.Http
open FSharp.Json

let json = """{
    "name": "Test HTTP Scenario",
    "baseUrl": "http://localhost:55042",
    "duration": 10,
    "concurrentCopies": 50,
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

let expected =
    { Name =  "Test HTTP Scenario"
      BaseUrl = Some "http://localhost:55042"
      Duration = Some 10
      ConcurrentCopies = Some 50
      Requests =
        [ { Url = "/api/ticket"
            Version = None
            Method = None
            Headers = unbox null
            Body = None
          }
          { Url = "/api/ticket/"
            Version = None
            Method = Some "POST"
            Headers = None
            Body = Some """{"id":1,"title":"Test ticket 1"}"""
          }
          { Url = "/api/ticket/1/text"
            Version = None
            Method = Some "PUT"
            Headers =
               [ "Authorization", "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ=="
                 "ContentType", "application/text"
               ] |> Map.ofList |> Some
            Body = Some "Text content"
          }
          { Url = "/api/ticket/1"
            Version = None
            Method = Some "DELETE"
            Headers = None
            Body = None
          }]
    }
[<Tests>]
let tests =
  testList "json serializers" [
    testCase "FSharp.Json" <| fun _ ->
      let config = JsonConfig.create(jsonFieldNaming = Json.lowerCamelCase)
      let actual = Json.deserializeEx<HttpRequestList> config json
      Expect.equal actual expected "I compute, therefore I am."
  ]
