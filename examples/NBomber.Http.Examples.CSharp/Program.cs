using NBomber.Http.Examples.CSharp.Scenarios;

namespace NBomber.Http.Examples.CSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            var scenario = SimpleScenario.BuildScenario();
            ScenarioRunner.Run(scenario);
        }
    }
}
