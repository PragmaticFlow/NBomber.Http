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
            NBomberRunner.RegisterScenarios(scenario)
                         .RunInConsole();
        }

        static Scenario BuildScenario()
        {
            var step = HttpStep.CreateRequest("GET", "https://www.youtube.com")
                               //.WithHeader("accept", "application/json")                               
                               //.WithBody(new StringContent("{ some json }"))
                               //.WithBody(new ByteArrayContent())
                               .BuildStep("GET request");

            return ScenarioBuilder.CreateScenario("test youtube.com", step)
                .WithConcurrentCopies(100)
                .WithDuration(TimeSpan.FromSeconds(10));                
        }
    }
}
