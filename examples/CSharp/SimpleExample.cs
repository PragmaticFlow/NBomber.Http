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
            var step = HttpStep.Create("simple step", (context) =>
                Http.CreateRequest("GET", "https://nbomber.com")
                    .WithHeader("Accept", "text/html")
                    // .WithBody(new StringContent("{ some JSON }"))
                    // .WithCheck(async (response) =>
                    //     response.IsSuccessStatusCode
                    //         ? Response.Ok()
                    //         : Response.Fail()
                    // )
            );

            var scenario = ScenarioBuilder.CreateScenario("test_gitter", step);

            NBomberRunner
                .RegisterScenarios(scenario)
                .LoadConfig("test_config.json")
                .Run();
        }
    }
}
