# NBomber.Http

[![build](https://github.com/PragmaticFlow/NBomber.Http/actions/workflows/build.yml/badge.svg)](https://github.com/PragmaticFlow/NBomber.Http/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/nbomber.http.svg)](https://www.nuget.org/packages/nbomber.http/)

NBomber plugin for defining HTTP scenarios.

#### Documentation is located [here](https://nbomber.com/docs/protocols/http)

```csharp
using var httpClient = new HttpClient();

var scenario = Scenario.Create("http_scenario", async context =>
{
    var request =
        Http.CreateRequest("GET", "https://nbomber.com")
            .WithHeader("Content-Type", "application/json");
         // .WithBody(new StringContent("{ some JSON }", Encoding.UTF8, "application/json"));

    var response = await Http.Send(httpClient, request);

    return response;
});
```
