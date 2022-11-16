﻿using System;
using System.Net.Http;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace CSharp;

class SimpleExample
{
    public void Run()
    {
        using var httpClient = new HttpClient();

        var scenario = Scenario.Create("http_scenario", async context =>
        {
            var request =
                Http.CreateRequest("GET", "https://nbomber.com")
                    .WithHeader("Accept", "text/html")
                    .WithBody(new StringContent("{ some JSON }"));

            var response = await Http.Send(httpClient, request);

            return response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.InjectPerSec(100, TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
