using System;

using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace NBomber.Http.Examples.CSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            var scenario = BuildScenario();
            scenario.RunInConsole();
        }

        static Scenario BuildScenario()
        {
            var step = HttpStep.CreateRequest("GET", "https://github.com/PragmaticFlow/NBomber")
                               .BuildStep();

            return new ScenarioBuilder(scenarioName: "HTTP scenario with 100 concurrent requests")
                .AddTestFlow("GET flow", steps: new[] { step }, concurrentCopies: 100)
                .Build(duration: TimeSpan.FromSeconds(10));
        }
    }
}
