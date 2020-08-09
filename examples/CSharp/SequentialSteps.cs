
using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Plugins.Http.CSharp;

namespace CSharp
{
    class SequentialSteps
    {
        public static void Run()
        {
            var step1 = HttpStep.Create("step 1", (context) =>
                Http.CreateRequest("GET", "https://gitter.im"));

            var step2 = HttpStep.Create("step 2", async (context) =>
            {
                var step1Response = context.GetPreviousStepResponse<HttpResponseMessage>();
                var headers = step1Response.Headers;
                var body = await step1Response.Content.ReadAsStringAsync();

                return Http.CreateRequest("GET", "https://gitter.im");
            });

            var scenario = ScenarioBuilder
                .CreateScenario("test_gitter", step1, step2)
                    .WithoutWarmUp()
                    .WithLoadSimulations(Simulation.InjectPerSec(100, TimeSpan.FromSeconds(30)));

            NBomberRunner
                .RegisterScenarios(scenario)
                .LoadConfig("test_config.json")
                .Run();
        }
    }
}
