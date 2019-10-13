using System.Net.Http;
using NBomber.CSharp;

namespace NBomber.Http.Api2.CSharp
{
    public class SimpleExample
    {
        public static void Run()
        {
            var step1 = HttpStep.Create("step 1", ctx => 
                Http.CreateRequest("https://github.com"));
            var step2 = HttpStep.Create("simple step",
                context => Http.CreateRequest("https://gitter.im",
                    method: HttpMethod.Get,
                    headers: new[]
                    {
                        ("Accept", "text/html"),
                        ("Auth", "Basic")
                    }
                ));
            var scenario = Scenario.Create("test_gitter", step1, step2);
            NBomberRunner.RegisterScenarios(scenario)
                .LoadConfig("config.json")
                .RunInConsole();
        }
    }
}