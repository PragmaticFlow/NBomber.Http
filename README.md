[![Build status](https://ci.appveyor.com/api/projects/status/639k1l877whni54c?svg=true)](https://ci.appveyor.com/project/PragmaticFlowOrg/nbomber-http)
[![NuGet](https://img.shields.io/nuget/v/nbomber.http.svg)](https://www.nuget.org/packages/nbomber.http/)
[![Gitter](https://badges.gitter.im/nbomber/community.svg)](https://gitter.im/nbomber/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

NBomber plugin for defining HTTP scenarios

### How to install
To install NBomber.Http via NuGet, run this command in NuGet package manager console:
```code
PM> Install-Package NBomber.Http
```

### Documentation
Documentation is located [here](https://nbomber.com)

### Contributing
Would you like to help make NBomber even better? We keep a list of issues that are approachable for newcomers under the [good-first-issue](https://github.com/PragmaticFlow/NBomber.Http/issues?q=is%3Aopen+is%3Aissue+label%3A%22good+first+issue%22) label.

### Examples
```fsharp
// FSharp example

let httpFactory = HttpClientFactory.create()

let step = Step.create("simple step", clientFactory = httpFactory, execute = fun context ->
	Http.createRequest "GET" "https://nbomber.com"
	|> Http.withHeader "Accept" "text/html"
	|> Http.send context
)

Scenario.create "test_nbomber" [step]
|> Scenario.withLoadSimulations [InjectPerSec(rate = 100, during = seconds 30)]
|> NBomberRunner.registerScenario
|> NBomberRunner.run
```

```csharp
// CSharp example

var httpFactory = HttpClientFactory.Create();

var step = Step.Create("simple step", clientFactory: httpFactory, execute: async context =>
{
	var request = Http.CreateRequest("GET", "https://nbomber.com")
					  .WithHeader("Accept", "text/html");

	var response = await Http.Send(request, context);
	return response;
});

var scenario = ScenarioBuilder
		.CreateScenario("test_nbomber", step)
		.WithLoadSimulations(Simulation.InjectPerSec(100, TimeSpan.FromSeconds(30)));

NBomberRunner
	.RegisterScenarios(scenario)
	.Run();
```
