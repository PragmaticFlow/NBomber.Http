using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;

namespace CSharp;

class SimpleExample
{
    public void Run()
    {
        // var socketsHandler = new SocketsHttpHandler
        // {
        //     MaxConnectionsPerServer = 5
        // };
        //
        // using var httpClient = new HttpClient(socketsHandler);

        using var httpClient = new HttpClient();

        var scenario = Scenario.Create("http_scenario", async context =>
        {
            var request =
                Http.CreateRequest("GET", "https://nbomber.com")
                    .WithHeader("Accept", "application/json")
                    .WithBody(new StringContent("{ some JSON }"));

            // var user = new UserData { Id = "1", Name = "anton" };
            //
            // var request2 =
            //     Http.CreateRequest("GET", "https://nbomber.com")
            //         .WithJsonBody(user);

            var response = await Http.Send(httpClient, request);

            return response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithWorkerPlugins(new HttpMetricsPlugin(new [] {HttpVersion.Version1 }))
            .Run();
    }
}
