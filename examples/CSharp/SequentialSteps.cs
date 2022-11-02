using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace CSharp;

class SequentialSteps
{
    public void Run()
    {
        using var httpClient = new HttpClient();

        var scenario = Scenario.Create("http_scenario", async context =>
        {
            var step1 = await Step.Run("step_1", context, async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://nbomber.com");
                var response = await Http.Send(httpClient, request);
                return response;
            });

            var step2 = await Step.Run("step_2", context, async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://nbomber.com");
                var response = await Http.Send(httpClient, request);
                return response;
            });

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.InjectPerSec(100, TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
