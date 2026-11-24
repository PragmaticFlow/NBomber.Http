using System.Net;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBomber.Http.Benchmarks.Infrastructure;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Benchmarks;

/// Sends real HTTP/1.1 requests to an in-process Kestrel server on the loopback interface.
/// The results are closer to a real load test, but they include the network stack and the server,
/// so they have more noise than <see cref="InMemorySendBenchmarks"/>.
/// - *_PlainClient: the same SocketsHttpHandler client for both versions, so only NBomber.Http changes.
/// - *_DefaultClient: the client from Http.CreateDefaultClient, as users get it by default.
public class LoopbackSendBenchmarks
{
    WebApplication _app = null!;
    HttpClient _plainClient = null!;
    HttpClient _defaultClient = null!;
    string _textUrl = "";
    string _userUrl = "";

    [GlobalSetup]
    public async Task Setup()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        _app = builder.Build();
        var text = new string('a', 1024);
        _app.MapGet("/text", () => text);
        _app.MapGet("/user", () => Results.Content("""{"userId":7,"title":"anton"}""", "application/json"));
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _textUrl = $"{address}/text";
        _userUrl = $"{address}/user";

        _plainClient = new HttpClient(new SocketsHttpHandler());
        _defaultClient = NBomberHttp.CreateDefaultClient(5000);

        // opens the connections before the measurements
        (await _plainClient.GetAsync(_textUrl)).Dispose();
        (await _defaultClient.GetAsync(_textUrl)).Dispose();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _plainClient.Dispose();
        _defaultClient.Dispose();
        await _app.DisposeAsync();
    }

    [Benchmark]
    public async Task<int> RawSendAsync_PlainClient()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _textUrl);
        using var response = await _plainClient.SendAsync(request);
        return (int)response.StatusCode;
    }

    [Benchmark]
    public async Task<long> Send_Get_PlainClient()
    {
        using var request = NBomberHttp.CreateRequest("GET", _textUrl);
        var response = await NBomberHttp.Send(_plainClient, request);
        response.Payload.Value.Dispose();
        return response.SizeBytes;
    }

    [Benchmark]
    public async Task<long> SendTyped_GetJson_PlainClient()
    {
        using var request = NBomberHttp.CreateRequest("GET", _userUrl);
        var response = await NBomberHttp.Send<User>(_plainClient, request);
        response.Payload.Value.Response.Dispose();
        return response.SizeBytes;
    }

    [Benchmark]
    public async Task<long> Send_Get_DefaultClient()
    {
        using var request = NBomberHttp.CreateRequest("GET", _textUrl);
        var response = await NBomberHttp.Send(_defaultClient, request);
        response.Payload.Value.Dispose();
        return response.SizeBytes;
    }
}
