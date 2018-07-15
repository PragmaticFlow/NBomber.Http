using System;

namespace NBomber.Http.Examples.CSharp.Scenarios
{
    class SimpleScenario
    {
        public static Scenario BuildScenario()
        {
            var step = HttpRequest.Create("GET", "https://github.com/VIP-Logic/NBomber")
                                  .BuildStep();

            return new ScenarioBuilder(scenarioName: "SimpleScenario with 100 concurrent users")
                .AddTestFlow("GET flow", steps: new[] { step }, concurrentCopies: 100)
                .Build(duration: TimeSpan.FromSeconds(10));
        }
    }
}
