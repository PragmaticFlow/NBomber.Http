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
                               //.WithVersion("2.0")
                               .WithHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8")
                               .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36")
                               //.WithBody(new StringContent("{ some json }"))
                               //.WithBody(new ByteArrayContent())
                               .BuildStep("GET request");

            return ScenarioBuilder.CreateScenario("test youtube.com", step)
                .WithConcurrentCopies(10)
                .WithDuration(TimeSpan.FromSeconds(10));                
        }
    }
}
