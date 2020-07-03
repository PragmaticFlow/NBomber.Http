using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;

namespace Example
{
    class SimpleExample
    {
        public static void Run()
        {
            var step = HttpStep.Create("simple step", (context) =>
                Http.CreateRequest("GET", "https://gitter.im")
                    .WithHeader("Accept", "text/html")
            );

            var scenario = ScenarioBuilder.CreateScenario("test_gitter", step);

            NBomberRunner
                .RegisterScenarios(scenario)
                .LoadConfig("test_config.json")
                .Run();
        }
    }
}
