using System;
using System.Net.Http;
using System.Text;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;

namespace CSharp
{
    class SimpleExample
    {
        public static void Run()
        {
            var httpFactory = HttpClientFactory.Create();

            var step = Step.Create("simple step", clientFactory: httpFactory, execute: async context =>
            {
                var request =
                    Http.CreateRequest("GET", "https://nbomber.com")
                        .WithHeader("Accept", "text/html")
                        .WithBody(new StringContent("{ some JSON }"))
                        .WithCheck(async (response) =>
                            //response.ToNBomberResponse() - you can convert HttpResponseMessage to NBomber's Response
                            response.IsSuccessStatusCode
                                ? Response.Ok()
                                : Response.Fail()
                        );

                var response = await Http.Send(request, context);
                return response;
            });

            var scenario = ScenarioBuilder
                    .CreateScenario("test_gitter", step)
                    .WithoutWarmUp()
                    .WithLoadSimulations(Simulation.InjectPerSec(100, TimeSpan.FromSeconds(30)));

            NBomberRunner
                .RegisterScenarios(scenario)
                //.LoadConfig("test_config.json")
                .Run();
        }
    }
}
