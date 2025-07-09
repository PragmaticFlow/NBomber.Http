using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;
using NBomber.Plugins.Network.Ping;

namespace CSharp;

class SimpleExample
{
    public void Run()
    {
        // For this example, you'll need to start the HttpApiSimulator, which is located in the examples/simulators solution folder.
        // Make sure it’s running before executing the client tests to ensure proper communication.

        using var httpClient = Http.CreateDefaultClient();
        var url = "http://localhost:5071/api/pingpong";

        var scenario = Scenario.Create("http_scenario", async context =>
        {
            var request =
                Http.CreateRequest("GET", url)
                    .WithHeader("Accept", "application/json")
                    .WithBody(new StringContent("{ some JSON }"));

            var response = await Http.Send(httpClient, request);

            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(3))
        .WithLoadSimulations(Simulation.Inject(rate: 1000, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithWorkerPlugins(
                new HttpMetricsPlugin([HttpVersion.Version1])
            )
            .Run();
    }
}
