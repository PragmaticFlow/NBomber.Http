using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using NBomber.Http.CSharp;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Tests;

public class Http2ConnectionsTest
{
    // Starts Kestrel on a free port without TLS. The endpoint returns "<protocol> <connection id>".
    static async Task<(WebApplication App, string Address)> StartServer(HttpProtocols protocols, int maxStreamsPerConnection = 100)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.Http2.MaxStreamsPerConnection = maxStreamsPerConnection;
            options.Listen(IPAddress.Loopback, 0, o => o.Protocols = protocols);
        });

        var app = builder.Build();
        app.MapGet("/", async (HttpContext ctx) =>
        {
            await Task.Delay(200); // keep the stream open, so parallel requests can reach the stream limit
            return $"{ctx.Request.Protocol} {ctx.Connection.Id}";
        });
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return (app, address);
    }

    // Sends the request and returns the protocol that the server received.
    static async Task<string> SendAndGetProtocol(HttpClient client, HttpRequestMessage request) =>
        (await SendAndRead(client, request)).Split(' ')[0];

    static async Task<string> SendAndRead(HttpClient client, HttpRequestMessage request)
    {
        var response = await NBomberHttp.Send(client, request);
        Assert.False(response.IsError, response.Message);
        return await response.Payload.Value.Content.ReadAsStringAsync();
    }

    // h2c (HTTP/2 without TLS) needs RequestVersionExact, because without TLS there is no ALPN.
    static HttpClient CreateH2cClient() =>
        NBomberHttp.CreateDefaultClient(httpVersion: "2.0", versionPolicy: HttpVersionPolicy.RequestVersionExact);

    [Theory]
    [InlineData(false)] // the client does not change the version (httpVersion: null), it is set manually on each request
    [InlineData(true)]  // the version is set by the client (httpVersion)
    public async Task WhenStreamLimitIsReached_OpensMoreHttp2Connections(bool useClientVersion)
    {
        var (app, address) = await StartServer(HttpProtocols.Http2, maxStreamsPerConnection: 2);
        await using var _ = app;

        using var client = useClientVersion ? CreateH2cClient() : NBomberHttp.CreateDefaultClient(httpVersion: null);

        Task<string> Send()
        {
            var request = NBomberHttp.CreateRequest("GET", address);
            if (!useClientVersion)
            {
                request.Version = System.Net.HttpVersion.Version20;
                request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            }
            return SendAndRead(client, request);
        }

        // Until the client receives the server's SETTINGS frame, .NET assumes a limit of 100 streams.
        // This first request opens the connection, so the client knows the real limit (2) before the parallel requests.
        await Send();

        var connectionIds = (await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Send())))
            .Select(body => body.Split(' ')[1])
            .Distinct()
            .ToArray();

        // with one HTTP/2 connection (EnableMultipleHttp2Connections = false), all requests use the same connection
        Assert.True(connectionIds.Length > 1, $"Expected more than 1 HTTP/2 connection, but got {connectionIds.Length}.");
    }

    [Fact]
    public async Task WithHttpVersion_SendsRequestsWithThisVersion_WithoutSettingVersionOnRequest()
    {
        var (app, address) = await StartServer(HttpProtocols.Http2);
        await using var _ = app;

        using var client = CreateH2cClient();
        Assert.Equal("HTTP/2", await SendAndGetProtocol(client, NBomberHttp.CreateRequest("GET", address)));
    }

    [Fact]
    public async Task WithVersion_HasPriorityOverClientVersion()
    {
        // Without TLS, a Kestrel endpoint with HTTP/1.1 and HTTP/2 accepts only HTTP/1.1,
        // so the request fails if the client changes it to HTTP/2 (RequestVersionExact).
        var (app, address) = await StartServer(HttpProtocols.Http1AndHttp2);
        await using var _ = app;

        using var client = CreateH2cClient();
        var request = NBomberHttp.CreateRequest("GET", address).WithVersion("1.1");

        Assert.Equal("HTTP/1.1", await SendAndGetProtocol(client, request));
    }

    [Fact]
    public async Task WithHttpVersion_OverridesVersionSetDirectlyOnRequest()
    {
        // HTTP/2-only server: a request sent with HTTP/1.1 would fail
        var (app, address) = await StartServer(HttpProtocols.Http2);
        await using var _ = app;

        using var client = CreateH2cClient();
        var request = NBomberHttp.CreateRequest("GET", address);
        request.Version = System.Net.HttpVersion.Version11; // not set with WithVersion, so the client changes it

        Assert.Equal("HTTP/2", await SendAndGetProtocol(client, request));
    }

    [Fact]
    public async Task DefaultClient_UsesHttp2OrLower_SoPlainHttpFallsBackToHttp1()
    {
        var (app, address) = await StartServer(HttpProtocols.Http1);
        await using var _ = app;

        using var client = NBomberHttp.CreateDefaultClient(); // httpVersion "2.0", RequestVersionOrLower
        Assert.Equal("HTTP/1.1", await SendAndGetProtocol(client, NBomberHttp.CreateRequest("GET", address)));
    }

    [Fact]
    public void DefaultClient_UsesHttp2OrLowerByDefault()
    {
        using var client = NBomberHttp.CreateDefaultClient();
        Assert.Equal(System.Net.HttpVersion.Version20, client.DefaultRequestVersion);
        Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, client.DefaultVersionPolicy);

        using var fsharpClient = NBomber.Http.FSharp.Http.createDefaultClient();
        Assert.Equal(System.Net.HttpVersion.Version20, fsharpClient.DefaultRequestVersion);
        Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, fsharpClient.DefaultVersionPolicy);
    }

    [Fact]
    public async Task WithHttpVersion_PolicyExact_WhenServerSupportsOnlyHttp1_FailsInsteadOfFallingBack()
    {
        var (app, address) = await StartServer(HttpProtocols.Http1);
        await using var _ = app;

        using var client = CreateH2cClient();

        await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
            NBomberHttp.Send(client, NBomberHttp.CreateRequest("GET", address)));
    }

    [Fact]
    public async Task FSharpCreateDefaultClient_WithHttpVersion_SendsHttp2()
    {
        var (app, address) = await StartServer(HttpProtocols.Http2);
        await using var _ = app;

        // the F# member has the same optional parameters as the C# API
        using var client = NBomber.Http.FSharp.Http.createDefaultClient(httpVersion: "2.0", versionPolicy: HttpVersionPolicy.RequestVersionExact);
        Assert.Equal("HTTP/2", await SendAndGetProtocol(client, NBomberHttp.CreateRequest("GET", address)));
    }
}
