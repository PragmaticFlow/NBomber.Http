# NBomber.Http benchmarks

BenchmarkDotNet benchmarks that measure the time and the allocations of NBomber.Http. Every benchmark runs twice:

| Job      | NBomber.Http build                              |
|----------|-------------------------------------------------|
| `local`  | the source in `src/NBomber.Http` (the baseline) |
| `v6.2.0` | the NuGet package `NBomber.Http 6.2.0`          |

The `Ratio` and `Alloc Ratio` columns compare each version with `local`. A value below 1.00 means that the version is faster (or allocates less) than the local build.

## Run

Run from `benchmarks/NBomber.Http.Benchmarks`, always in Release:

```bash
dotnet run -c Release -- --filter *                    # all benchmarks (full precision, slow)
dotnet run -c Release -- --filter *InMemory* --job short
dotnet run -c Release -- --filter * --job dry          # smoke test: builds and runs each benchmark once
dotnet run -c Release -- --filter * --compare 6.2.1    # compare with another NuGet version
```

- `--job`: `dry`, `short`, `medium`, `long` or `default`. It sets the run strategy of both version jobs.
- `--compare <version>`: the NuGet version to compare with. You can also set the `NBOMBER_HTTP_COMPARED_VERSION` environment variable. The default is `6.2.0`.
- The other BenchmarkDotNet options (for example, `--filter` and `--exporters`) work as usual.

The results are written to `BenchmarkDotNet.Artifacts/results/` (Markdown, HTML and CSV).

## Benchmarks

| Class                    | What it measures |
|--------------------------|------------------|
| `InMemorySendBenchmarks` | The NBomber.Http overhead on the send path: size calculation, body tracking and response wrapping. The client uses an in-memory handler (`StubHandler`), so there is no network I/O. `RawSendAsync` is `HttpClient` without NBomber.Http, for reference. |
| `RequestBuildBenchmarks` | `CreateRequest`, `WithHeader` and `WithJsonBody`, without a send. |
| `LoopbackSendBenchmarks` | Real HTTP/1.1 requests to an in-process Kestrel server. The results are closer to a load test, but they have more noise. `*_PlainClient` uses the same `SocketsHttpHandler` for both versions. `*_DefaultClient` uses `Http.CreateDefaultClient`. |

## Rules for new benchmarks

- Use only the API that exists in **both** compared versions. The BenchmarkDotNet host finds the benchmarks in the local build. A method that does not compile against the NuGet version breaks that job. Check both builds:
  ```bash
  dotnet build -c Release
  dotnet build -c Release -p:NBomberHttpVersion=6.2.0
  ```
- Dispose every response, so that the benchmark does not keep connections or buffers.

## How it works

`NBomber.Http.Benchmarks.csproj` references the local project by default. When the `NBomberHttpVersion` MSBuild property is set, it references the NuGet package of that version. `VersionsConfig` adds one job per version with `WithMsBuildArguments("/p:NBomberHttpVersion=...")`. BenchmarkDotNet builds each job separately and runs it in its own process, so the two `NBomber.Http` assemblies never load together.
