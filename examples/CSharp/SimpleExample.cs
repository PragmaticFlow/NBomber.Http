using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace CSharp;

class SimpleExample
{
    public void Run()
    {
        using var httpClient = new HttpClient();

        var scenario = Scenario.Create("http_scenario", async context =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://nbomber.com");

            var response = await httpClient.SendAsync(request);

            var dataSize = Http.GetRequestSize(request) + Http.GetResponseSize(response);

            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: response.StatusCode.ToString(), sizeBytes: dataSize)
                : Response.Fail(statusCode: response.StatusCode.ToString(), sizeBytes: dataSize);
        })
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.InjectPerSec(100, TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
