using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;

namespace CSharp;

public class UserData
{
    public string Id { get; set; }
    public string Name { get; set; }
}

class SequentialSteps
{
    public void Run()
    {
        using var httpClient = new HttpClient();

        var scenario = Scenario.Create("http_scenario", async context =>
        {
            var step1 = await Step.Run("step_1", context, async () =>
            {
                var request =
                    Http.CreateRequest("GET", "https://nbomber.com")
                        .WithHeader("Accept", "application/json")
                        .WithBody(new StringContent("{ some JSON }"));

                var response = await Http.Send(httpClient, request);

                return response;
            });

            var step2 = await Step.Run("step_2", context, async () =>
            {
                var user = new UserData { Id = "1", Name = "anton" };

                var request = Http.CreateRequest("GET", "https://nbomber.com")
                                  .WithJsonBody(user);

                var response = await Http.Send(httpClient, request);

                return response;
            });

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithWorkerPlugins(new HttpMetricsPlugin())
            .Run();
    }
}
