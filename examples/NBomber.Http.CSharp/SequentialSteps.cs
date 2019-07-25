using System;
using NBomber.CSharp;

namespace NBomber.Http.CSharp
{
    class SequentialSteps
    {
        public static void Run()
        {
            var step1 = HttpStep.Create("step 1",
                Http.CreateRequest("GET", "https://gitter.im")                                        
            );
                        
            var step2 = HttpStep.CreateFromResponse("step 2", response =>
                Http.CreateRequest("GET", "https://gitter.im")                    
            );

            var scenario = ScenarioBuilder.CreateScenario("test gitter", step1, step2)
                                          .WithConcurrentCopies(200)
                                          .WithDuration(TimeSpan.FromSeconds(10));

            NBomberRunner.RegisterScenarios(scenario)
                         .RunInConsole();
        }
    }
}
