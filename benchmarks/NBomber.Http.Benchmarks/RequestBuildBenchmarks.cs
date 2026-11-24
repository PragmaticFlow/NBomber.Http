using BenchmarkDotNet.Attributes;
using NBomber.Http.Benchmarks.Infrastructure;
using NBomber.Http.CSharp;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Benchmarks;

/// Measures the request builder functions, without sending the request.
public class RequestBuildBenchmarks
{
    static readonly User UserData = new() { UserId = 7, Title = "anton" };

    [Benchmark]
    public HttpRequestMessage CreateRequest() =>
        NBomberHttp.CreateRequest("GET", $"{StubHandler.BaseUrl}/text");

    [Benchmark]
    public HttpRequestMessage CreateRequest_WithHeaders() =>
        NBomberHttp.CreateRequest("GET", $"{StubHandler.BaseUrl}/text")
            .WithHeader("Accept", "text/plain")
            .WithHeader("User-Agent", "NBomber/7.0 benchmark")
            .WithHeader("X-Correlation-Id", "8b0e6f0a-7a52-4d6e-9d55-2f0c7a3c1e11");

    [Benchmark]
    public HttpRequestMessage CreateRequest_WithJsonBody() =>
        NBomberHttp.CreateRequest("POST", $"{StubHandler.BaseUrl}/user")
            .WithJsonBody(UserData, null);
}
