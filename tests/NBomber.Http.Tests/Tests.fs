module Tests

open Expecto
open NBomber.Http
open FSharp.Json
open System.Net
open NBomber.FSharp

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
            "body": { "id":1, "title": "Test ticket 1" },
            "expectCode": "Created"
        },
        {
            "url" : "/api/ticket/1/text",
            "method": "PUT",
            "body": "Text content",
            "headers": {
                "ContentType": "application/text",
                "Authorization": "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ=="
            },
            "expectCode": "Accepted"
        },
        {
            "url": "/api/ticket/1",
            "method": "DELETE",
            "expectCode": "NoContent"
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
            ExpectCode = None
          }
          { Url = "/api/ticket/"
            Version = None
            Method = Some "POST"
            Headers = None
            Body = Some """{"id":1,"title":"Test ticket 1"}"""
            ExpectCode = Some HttpStatusCode.Created
          }
          { Url = "/api/ticket/1/text"
            Version = None
            Method = Some "PUT"
            Headers =
               [ "Authorization", "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ=="
                 "ContentType", "application/text"
               ] |> Map.ofList |> Some
            Body = Some "Text content"
            ExpectCode = Some HttpStatusCode.Accepted
          }
          { Url = "/api/ticket/1"
            Version = None
            Method = Some "DELETE"
            Headers = None
            Body = None
            ExpectCode = Some HttpStatusCode.NoContent
          }]
    }

[<Tests>]
let tests =
  testList "all tests" [
      testCase "not fail on expected code" <| fun _ ->
          // TODO actually integration test
          """{ "name": "Not existing host",
               "baseUrl": "https://not-found.com",
               "requests": [
                 { "url": "/not/exists",
                   "expectCode": "NotFound"
                 }
               ]
          }"""
          |> NBomber.Http.FSharp.HttpScenario.deserialize
          |> Scenario.withDuration (System.TimeSpan.FromSeconds 1.)
          |> NBomberRunner.registerScenario
          |> NBomberRunner.runTest

      testList "json serializers" [
        testCase "FSharp.Json" <| fun _ ->
          let config = JsonConfig.create(jsonFieldNaming = Json.lowerCamelCase)
          let actual = Json.deserializeEx<HttpRequestList> config json
          Expect.equal actual expected "Deserialized scenario is not valid"
      ]
  ]

