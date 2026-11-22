using System;
using System.Net.Http;
using System.Text.Json;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;
using NBomber.Plugins.Network.Ping;

namespace CSharp;

class SequentialSteps
{
    public void Run()
    {
        var httpClient = Http.CreateDefaultClient();

        var scenario = Scenario.Create("http_scenario", async context =>
        {
            var step1 = await Step.Run("step_1", context, async () =>
            {
                var request =
                    Http.CreateRequest("GET", "https://nbomber.com")
                        .WithHeader("Accept", "application/json")
                        .WithBody(new StringContent("{ some JSON }"));

                var response = await httpClient.SendRequest(request);

                return response;
            });

            // example of WithJsonBody<T>(data)
            // populates request body by serializing data record to JSON format.
            // Also, it adds HTTP header: "Content-Type": "application/json".

            var step2 = await Step.Run("step_2", context, async () =>
            {
                var user = new UserData { UserId = 1, Title = "anton" };

                var request = Http.CreateRequest("GET", "https://nbomber.com")
                    .WithJsonBody(user);

                // example of tracing: the request and the response are written to the log (Debug level).
                // traceId is optional: if it isn't set, a new GUID is used.
                var response = await httpClient.SendRequest(request, logger: context.Logger, traceId: $"step_2_{context.InvocationNumber}");

                return response;
            });

            // example of httpClient.SendRequest<TResponse>
            // send request and deserialize HTTP response body to JSON format

            var step3 = await Step.Run("step_3", context, async () =>
            {
                var request =
                    Http.CreateRequest("GET", "https://jsonplaceholder.typicode.com/todos/1")
                        .WithHeader("Accept", "application/json");

                // jsonOptions is optional: by default, Http.GlobalJsonSerializerOptions is used
                var response = await httpClient.SendRequest<UserData>(request, jsonOptions: JsonSerializerOptions.Web);

                // user: UserData type
                var user = response.Payload.Value.Data;
                var userId = user.UserId;

                return response;
            });

            return Response.Ok();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(3))
        .WithLoadSimulations(Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithWorkerPlugins(
                new PingPlugin(PingPluginConfig.CreateDefault("nbomber.com", "jsonplaceholder.typicode.com")),
                new HttpMetricsPlugin([HttpVersion.Version1])
            )
            .Run();
    }
}
