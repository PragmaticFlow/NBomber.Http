using BenchmarkDotNet.Attributes;
using NBomber.Http.Benchmarks.Infrastructure;
using NBomber.Http.CSharp;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Benchmarks;

/// Measures the overhead of NBomber.Http on the send path: request and response size calculation,
/// request body tracking, and response wrapping. The HttpClient uses <see cref="StubHandler"/>,
/// so there is no network I/O. RawSendAsync is a reference row: HttpClient without NBomber.Http.
/// Only the API that exists in both compared versions is used (see VersionsConfig).
public class InMemorySendBenchmarks
{
    static readonly User UserData = new() { UserId = 7, Title = "anton" };
    static readonly byte[] StreamBody = new byte[1024];

    HttpClient _client = null!;

    [GlobalSetup]
    public void Setup() => _client = new HttpClient(new StubHandler());

    [GlobalCleanup]
    public void Cleanup() => _client.Dispose();

    [Benchmark]
    public async Task<int> RawSendAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{StubHandler.BaseUrl}/text");
        using var response = await _client.SendAsync(request);
        return (int)response.StatusCode;
    }

    [Benchmark]
    public async Task<long> Send_Get()
    {
        using var request = NBomberHttp.CreateRequest("GET", $"{StubHandler.BaseUrl}/text");
        var response = await NBomberHttp.Send(_client, request);
        response.Payload.Value.Dispose();
        return response.SizeBytes;
    }

    [Benchmark]
    public async Task<long> Send_Get_WithHeaders()
    {
        using var request = NBomberHttp.CreateRequest("GET", $"{StubHandler.BaseUrl}/text")
            .WithHeader("Accept", "text/plain")
            .WithHeader("User-Agent", "NBomber/7.0 benchmark")
            .WithHeader("X-Correlation-Id", "8b0e6f0a-7a52-4d6e-9d55-2f0c7a3c1e11");

        var response = await NBomberHttp.Send(_client, request);
        response.Payload.Value.Dispose();
        return response.SizeBytes;
    }

    [Benchmark]
    public async Task<long> Send_Get_NoContentLength()
    {
        using var request = NBomberHttp.CreateRequest("GET", $"{StubHandler.BaseUrl}/chunked");
        var response = await NBomberHttp.Send(_client, request);
        response.Payload.Value.Dispose();
        return response.SizeBytes;
    }

    [Benchmark]
    public async Task<long> Send_PostJson()
    {
        using var request = NBomberHttp.CreateRequest("POST", $"{StubHandler.BaseUrl}/text")
            .WithJsonBody(UserData, null);

        var response = await NBomberHttp.Send(_client, request);
        response.Payload.Value.Dispose();
        return response.SizeBytes;
    }

    [Benchmark]
    public async Task<long> Send_PostStream_UnknownLength()
    {
        using var request = NBomberHttp.CreateRequest("POST", $"{StubHandler.BaseUrl}/text")
            .WithBody(new StreamContent(new NonSeekableStream(StreamBody)));

        var response = await NBomberHttp.Send(_client, request);
        response.Payload.Value.Dispose();
        return response.SizeBytes;
    }

    [Benchmark]
    public async Task<long> SendTyped_GetJson()
    {
        using var request = NBomberHttp.CreateRequest("GET", $"{StubHandler.BaseUrl}/user");
        var response = await NBomberHttp.Send<User>(_client, request);
        response.Payload.Value.Response.Dispose();
        return response.SizeBytes;
    }
}
