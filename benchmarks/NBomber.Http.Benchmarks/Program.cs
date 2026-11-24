using System.CommandLine;
using BenchmarkDotNet.Running;
using NBomber.Http.Benchmarks;

// Examples:
//   dotnet run -c Release -- --filter *                     (all benchmarks, local vs NuGet 6.2.0)
//   dotnet run -c Release -- --filter *InMemory*            (library overhead only, no network)
//   dotnet run -c Release -- --filter * --job short         (quick run)
//   dotnet run -c Release -- --filter * --compare 6.2.1     (compare with another NuGet version)
//
// --job and --compare are read here, not by BenchmarkDotNet: BenchmarkDotNet would add --job as
// an extra job, while it must set the run strategy of both version jobs.
// All other arguments (for example, --filter) are passed to BenchmarkDotNet unchanged.

var jobOption = new Option<string?>("--job")
{
    Description = "The run strategy of both version jobs: dry, short, medium, long or default."
};
jobOption.AcceptOnlyFromAmong(VersionsConfig.JobNames);

var compareOption = new Option<string?>("--compare")
{
    Description = $"The NuGet version of NBomber.Http to compare with. The default: {VersionsConfig.DefaultComparedVersion}."
};

var rootCommand = new RootCommand("NBomber.Http benchmarks: the local build vs a NuGet version.")
{
    jobOption,
    compareOption
};

// the BenchmarkDotNet arguments are not declared here, so they must not fail the parse
rootCommand.TreatUnmatchedTokensAsErrors = false;

var parseResult = rootCommand.Parse(args);

if (parseResult.Errors.Count > 0)
{
    foreach (var error in parseResult.Errors)
        Console.Error.WriteLine(error.Message);

    return 1;
}

var config = new VersionsConfig(parseResult.GetValue(jobOption), parseResult.GetValue(compareOption));
var benchmarkArgs = parseResult.UnmatchedTokens.ToArray();

BenchmarkSwitcher
    .FromAssembly(typeof(VersionsConfig).Assembly)
    .Run(benchmarkArgs, config);

return 0;
