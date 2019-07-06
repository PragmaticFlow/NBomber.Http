﻿using System;
using System.Linq;

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
            var step = HttpStep.CreateRequest("GET", "https://gitter.im")                               
                               .WithHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8")
                               .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36")                               
                               .WithCheck(response => response.IsSuccessStatusCode) // default check
                               .BuildStep("GET request");
                               //.WithVersion("2.0")
                               //.WithBody(new StringContent("{ some json }"))
                               //.WithBody(new ByteArrayContent())

            return ScenarioBuilder.CreateScenario("test gitter", step)
                .WithConcurrentCopies(50)
                .WithDuration(TimeSpan.FromSeconds(10));                
        }
    }
}
