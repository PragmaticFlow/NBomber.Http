using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NBomber.Http.CSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Tests;

/// Tests for the httpClient.SendRequest(...) and httpClient.SendRequest<T>(...) extension methods.
public class SendRequestTest : IAsyncLifetime
{
    public class User
    {
        public int UserId { get; set; }
        public string? Title { get; set; }
    }

    class CollectingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    WebApplication _app = null!;
    string _address = "";
    readonly HttpClient _client = NBomberHttp.CreateDefaultClient();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        _app = builder.Build();
        _app.MapGet("/text", () => "hello");
        _app.MapGet("/user", () => Results.Content("""{"userId":7,"title":"anton"}""", "application/json"));
        _app.MapGet("/user-snake", () => Results.Content("""{"user_id":7,"title":"anton"}""", "application/json"));
        _app.MapGet("/no-content", () => Results.NoContent());
        _app.MapGet("/invalid-json", () => Results.Content("not json", "application/json"));
        _app.MapGet("/error", () => Results.StatusCode(500));
        await _app.StartAsync();

        _address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    HttpRequestMessage Get(string path) => NBomberHttp.CreateRequest("GET", $"{_address}{path}");

    static (ILogger Logger, CollectingSink Sink) CreateLogger()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        return (logger, sink);
    }

    static string? GetTraceId(LogEvent logEvent) =>
        (logEvent.Properties["TraceId"] as ScalarValue)?.Value as string;

    [Fact]
    public async Task SendRequest_Success_ReturnsOkWithSizeAndPayload()
    {
        var response = await _client.SendRequest(Get("/text"));

        Assert.False(response.IsError, response.Message);
        Assert.Equal("OK", response.StatusCode);
        Assert.True(response.SizeBytes > 0);
        Assert.Equal("hello", await response.Payload.Value.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SendRequest_Non2xx_ReturnsFail()
    {
        var response = await _client.SendRequest(Get("/error"));

        Assert.True(response.IsError);
        Assert.Equal("InternalServerError", response.StatusCode);
    }

    [Fact]
    public async Task SendRequestT_DeserializesJsonWithGlobalOptions()
    {
        var response = await _client.SendRequest<User>(Get("/user"));

        Assert.False(response.IsError, response.Message);
        Assert.Equal(7, response.Payload.Value.Data.UserId);
        Assert.Equal("anton", response.Payload.Value.Data.Title);
    }

    [Fact]
    public async Task SendRequestT_WithJsonOptions_UsesThem()
    {
        var snakeCase = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        var withOptions = await _client.SendRequest<User>(Get("/user-snake"), jsonOptions: snakeCase);
        Assert.False(withOptions.IsError, withOptions.Message);
        Assert.Equal(7, withOptions.Payload.Value.Data.UserId);

        // the global options (JsonSerializerOptions.Web) don't map "user_id" to UserId
        var withoutOptions = await _client.SendRequest<User>(Get("/user-snake"));
        Assert.Equal(0, withoutOptions.Payload.Value.Data.UserId);
    }

    [Fact]
    public async Task SendRequestT_NoContent_ReturnsOkWithDefaultData()
    {
        var response = await _client.SendRequest<User>(Get("/no-content"));

        Assert.False(response.IsError, response.Message);
        Assert.Equal("NoContent", response.StatusCode);
        Assert.Null(response.Payload.Value.Data);
    }

    [Fact]
    public async Task SendRequestT_InvalidJson_ReturnsFailWithMessage()
    {
        var response = await _client.SendRequest<User>(Get("/invalid-json"));

        Assert.True(response.IsError);
        Assert.Equal("OK", response.StatusCode);
        Assert.Contains("JSON deserialization failed", response.Message);
    }

    [Fact]
    public async Task SendRequest_WithLoggerAndTraceId_LogsRequestAndResponseWithThisTraceId()
    {
        var (logger, sink) = CreateLogger();

        await _client.SendRequest(Get("/text"), logger: logger, traceId: "my-trace");

        var events = sink.Events.ToArray();
        Assert.Equal(2, events.Length);
        Assert.StartsWith("HTTP Request", events[0].MessageTemplate.Text);
        Assert.StartsWith("HTTP Response", events[1].MessageTemplate.Text);
        Assert.All(events, e => Assert.Equal("my-trace", GetTraceId(e)));
    }

    [Fact]
    public async Task SendRequest_WithLoggerWithoutTraceId_UsesOneGeneratedTraceId()
    {
        var (logger, sink) = CreateLogger();

        await _client.SendRequest(Get("/text"), logger: logger);

        var traceIds = sink.Events.Select(GetTraceId).ToArray();
        Assert.Equal(2, traceIds.Length);
        Assert.Equal(traceIds[0], traceIds[1]);
        Assert.Equal(32, traceIds[0]!.Length); // Guid "N" format
    }

    [Fact]
    public async Task SendRequestT_WithLogger_LogsRequestAndResponse()
    {
        var (logger, sink) = CreateLogger();

        var response = await _client.SendRequest<User>(Get("/user"), logger: logger, traceId: "typed");

        Assert.False(response.IsError, response.Message);
        Assert.Equal(2, sink.Events.Count);
        Assert.All(sink.Events, e => Assert.Equal("typed", GetTraceId(e)));
    }

    [Fact]
    public async Task SendRequest_WithCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _client.SendRequest(Get("/text"), cancellationToken: cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _client.SendRequest<User>(Get("/user"), cancellationToken: cts.Token));
    }
}
