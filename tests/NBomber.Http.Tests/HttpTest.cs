using NBomber.CSharp;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Tests;

public class HttpTest
{
    [Fact]
    public void EndToEnd()
    {
        // For this example, you'll need to start the HttpApiSimulator, which is located in the examples/simulators solution folder.
        // Make sure it’s running before executing the client tests to ensure proper communication.

        var scenario = Scenario.Create("restsharp_scenario", async ctx =>
        {
            var client = new HttpClient();
            var request = NBomberHttp.CreateRequest("GET", "http://localhost:5071/api/pingpong");

            return await NBomberHttp.Send(client, request);
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.KeepConstant(10, TimeSpan.FromSeconds(5))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        Assert.True(stats.AllOkCount > 0);
        Assert.True(stats.AllFailCount == 0);

        foreach (var scenarioStats in stats.ScenarioStats)
        {
            foreach (var stepStats in scenarioStats.StepStats)
                Assert.True(stepStats.Ok.Latency.MinMs > 0);
        }
    }
}
