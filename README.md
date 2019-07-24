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
```csharp
class Program
{
	static void Main(string[] args)
	{
		var step = HttpStep.CreateRequest("GET", "https://gitter.im")
						   .WithHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8")
						   .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36")
						   .WithCheck(response => response.IsSuccessStatusCode) // default check
						   .BuildStep("GET request");                   

		var scenario = ScenarioBuilder.CreateScenario("test gitter", step)
									  .WithConcurrentCopies(200)                                          
									  .WithDuration(TimeSpan.FromSeconds(10));

		NBomberRunner.RegisterScenarios(scenario)
					 .RunInConsole();
	}
}
```
