
using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;

namespace CSharp
{
    class SequentialSteps
    {
        public static void Run()
        {
            var httpFactory = HttpClientFactory.Create();

            var step1 = Step.Create("step 1", clientFactory: httpFactory, execute: async context =>
            {
                var request = Http.CreateRequest("GET", "https://gitter.im");
                var response = await Http.Send(request, context);
                return response;
            });

            var step2 = Step.Create("step 2", clientFactory: httpFactory, execute: async context =>
            {
                var step1Response = context.GetPreviousStepResponse<HttpResponseMessage>();
                var headers = step1Response.Headers;
                var body = await step1Response.Content.ReadAsStringAsync();

                var request = Http.CreateRequest("GET", "https://gitter.im");
                var response = await Http.Send(request, context);
                return response;
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
