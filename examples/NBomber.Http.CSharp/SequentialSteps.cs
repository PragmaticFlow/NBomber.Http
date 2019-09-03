using System;
using System.Net.Http;
using NBomber.CSharp;

namespace NBomber.Http.CSharp
{
    class SequentialSteps
    {
        public static void Run()
        {
            var step1 = HttpStep.Create("step 1", async (context) =>
                Http.CreateRequest("GET", "https://gitter.im"));
                        
            var step2 = HttpStep.Create("step 2", async (context) =>
            {
                var step1Response = context.Data as HttpResponseMessage;
                var content = await step1Response.Content.ReadAsStringAsync(); 
                
                return Http.CreateRequest("GET", "https://gitter.im");
            });

            var scenario = ScenarioBuilder.CreateScenario("test gitter", step1, step2)
                                          .WithConcurrentCopies(100)
                                          .WithDuration(TimeSpan.FromSeconds(10));

            NBomberRunner.RegisterScenarios(scenario)
                         .RunInConsole();
        }
    }
}
