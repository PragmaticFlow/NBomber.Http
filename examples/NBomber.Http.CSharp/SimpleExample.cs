using System;
using NBomber.CSharp;

namespace NBomber.Http.CSharp
{
    class SimpleExample
    {
        public static void Run()
        {
            var step = HttpStep.Create("simple step",
                
                Http.CreateRequest("GET", "https://gitter.im")
                    .WithHeader("Accept", "text/html")
                    .WithCheck(response => response.IsSuccessStatusCode) // default check
            );

            var scenario = ScenarioBuilder.CreateScenario("test gitter", step)
                                          .WithConcurrentCopies(200)                                          
                                          .WithDuration(TimeSpan.FromSeconds(10));

            NBomberRunner.RegisterScenarios(scenario)
                         .RunInConsole();
        }
    }
}
