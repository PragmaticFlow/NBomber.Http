using System;
using NBomber.CSharp;

namespace NBomber.Http.Api2.CSharp
{
    public class SequentialSteps
    {
        public static void Run()
        {
            var step1 = HttpStep.Create("step 1", ctx =>
                Http.CreateRequest("https://gitter.im")); // defaults to GET without headers

            var step2 = HttpStep.Create("step 2", async ctx =>
            {
                var response = ctx.GetPreviousStepResponse();
                var headers = response.Headers;
                var body = await response.Content.ReadAsStringAsync();
                return Http.CreateRequest("https://gitter.im");
            });
            var scenario = Scenario.Create("test gitter",
                concurrentCopies: 100,
                duration : TimeSpan.FromSeconds(10),
                step1, step2
            );
            NBomberRunner.RegisterScenarios(scenario).RunInConsole();
        }
    }
}