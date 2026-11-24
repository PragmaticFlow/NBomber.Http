using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace NBomber.Http.Benchmarks;

/// Runs every benchmark twice: with the local NBomber.Http source (the baseline job)
/// and with the NBomber.Http NuGet package of the compared version.
/// Each job is built with its own MSBuild arguments and runs in its own process,
/// so the two assemblies (both named NBomber.Http) are never loaded together.
public class VersionsConfig : ManualConfig
{
    public const string DefaultComparedVersion = "6.2.0";

    /// Set this environment variable (or pass --compare) to compare with another release, for example 6.2.1.
    public const string ComparedVersionEnvVar = "NBOMBER_HTTP_COMPARED_VERSION";

    /// The values of the --job option.
    public static readonly string[] JobNames = ["dry", "short", "medium", "long", "default"];

    public VersionsConfig() : this(null, null) { }

    /// <param name="jobName">The run strategy for both jobs: dry, short, medium, long or default (null).</param>
    /// <param name="comparedVersion">The NuGet version to compare with. If null, the env var or 6.2.0 is used.</param>
    public VersionsConfig(string? jobName, string? comparedVersion)
    {
        // the default loggers, exporters and columns (DefaultConfig has no jobs)
        Add(DefaultConfig.Instance);

        comparedVersion ??=Environment.GetEnvironmentVariable(ComparedVersionEnvVar);
        if (string.IsNullOrWhiteSpace(comparedVersion))
            comparedVersion = DefaultComparedVersion;

        var baseJob = GetBaseJob(jobName);

        AddJob(baseJob
            .WithId("local")
            .AsBaseline());

        AddJob(baseJob
            .WithMsBuildArguments($"/p:NBomberHttpVersion={comparedVersion}")
            .WithId($"v{comparedVersion}"));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(StatisticColumn.P95);
        HideColumns(Column.Arguments);
        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.Declared));
    }

    static Job GetBaseJob(string? jobName) =>
        jobName?.ToLowerInvariant() switch
        {
            null or "default" => Job.Default,
            "dry"    => Job.Dry,
            "short"  => Job.ShortRun,
            "medium" => Job.MediumRun,
            "long"   => Job.LongRun,
            _ => throw new ArgumentException($"Unknown --job '{jobName}'. Use: dry, short, medium, long or default.")
        };
}
