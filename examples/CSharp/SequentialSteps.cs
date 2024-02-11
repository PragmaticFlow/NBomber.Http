using System;
using System.Net.Http;
using System.Text.Json;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;

namespace CSharp;

class SequentialSteps
{
    public void Run()
    {
        // sets global JsonSerializerOptions to use CamelCase naming
        Http.GlobalJsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

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

            // example of WithJsonBody<T>(data)
            // populates request body by serializing data record to JSON format. Also, it adds HTTP header: "Accept": "application/json".

            var step2 = await Step.Run("step_2", context, async () =>
            {
                var user = new UserData { UserId = 1, Title = "anton" };

                var request = Http.CreateRequest("GET", "https://nbomber.com")
                                  .WithJsonBody(user);

                var response = await Http.Send(httpClient, request);

                return response;
            });

            // example of Http.Send<TResponse>
            // send request and deserialize HTTP response body to JSON format

            var step3 = await Step.Run("step_3", context, async () =>
            {
                var request =
                    Http.CreateRequest("GET", "https://jsonplaceholder.typicode.com/todos/1")
                        .WithHeader("Accept", "application/json");

                var response = await Http.Send<UserData>(httpClient, request);

                // user: UserData type
                var user = response.Payload.Value;
                var userId = response.Payload.Value.UserId;

                return response;
            });

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithWorkerPlugins(new HttpMetricsPlugin())
            .Run();
    }
}
