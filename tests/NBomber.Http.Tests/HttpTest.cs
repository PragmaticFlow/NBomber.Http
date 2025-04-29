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

        var client = new HttpClient();

        var scenario = Scenario.Create("http_scenario", async ctx =>
        {
            var getStep = Step.Run("get", ctx, async () =>
            {
                var request = NBomberHttp.CreateRequest("GET", "http://localhost:5071/api/pingpong");
                var response = await NBomberHttp.Send(client, request);

                if (await response.Payload.Value.Content.ReadAsStringAsync() != "Get")
                    throw new ArgumentException();

                return response;
            });

            var postStep = Step.Run("post", ctx, async () =>
            {
                var request = NBomberHttp.CreateRequest("POST", "http://localhost:5071/api/pingpong");
                var response = await NBomberHttp.Send(client, request);

                if (await response.Payload.Value.Content.ReadAsStringAsync() != "Post")
                    throw new ArgumentException();

                return response;
            });

            var putStep = Step.Run("put", ctx, async () =>
            {
                var request = NBomberHttp.CreateRequest("PUT", "http://localhost:5071/api/pingpong");
                var response = await NBomberHttp.Send(client, request);

                if (await response.Payload.Value.Content.ReadAsStringAsync() != "Put")
                    throw new ArgumentException();

                return response;
            });

            var patchStep = Step.Run("patch", ctx, async () =>
            {
                var request = NBomberHttp.CreateRequest("PATCH", "http://localhost:5071/api/pingpong");
                var response = await NBomberHttp.Send(client, request);

                if (await response.Payload.Value.Content.ReadAsStringAsync() != "Patch")
                    throw new ArgumentException();

                return response;
            });

            var deleteStep = Step.Run("delete", ctx, async () =>
            {
                var request = NBomberHttp.CreateRequest("DELETE", "http://localhost:5071/api/pingpong");
                var response = await NBomberHttp.Send(client, request);

                if (await response.Payload.Value.Content.ReadAsStringAsync() != "Delete")
                    throw new ArgumentException();

                return response;
            });

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.KeepConstant(1, TimeSpan.FromSeconds(5))
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
