using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NBomber.CSharp;

namespace NBomber.Http.CSharp
{
    class SimpleExample
    {
        public static void Run()
        {
            var step = HttpStep.Create("simple step", async (context) =>
                Http.CreateRequest("GET", "https://gitter.im")
                    .WithHeader("Accept", "text/html")
                    //.WithBody(new StringContent("{ some JSON }", Encoding.UTF8, "application/json"))
                    //.WithVersion("1.1")
                    //.WithCheck(response => Task.FromResult(response.IsSuccessStatusCode)) // default check
            );

            var scenario = ScenarioBuilder.CreateScenario("test gitter", step)
                                          .WithConcurrentCopies(100)                                          
                                          .WithDuration(TimeSpan.FromSeconds(10));

            NBomberRunner.RegisterScenarios(scenario)
                         .RunInConsole();
        }
    }
}
