using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using NBomber.Contracts;
using NBomber.Http.CSharp;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Tests;

/// <summary>
/// Covers CreateDefaultClient with automaticDecompression: the client decompresses the body itself
/// (CountingDecompressionHandler) instead of .NET, so it can count the compressed bytes received.
/// Most tests are differential: the same raw response is sent to a client with .NET AutomaticDecompression
/// and to the default client. The caller must see the same response from both; only the size differs.
/// </summary>
public class HttpCountingDecompressionTest
{
    // deflate isn't supported: with it, the native client would send and decode "deflate", and the differential tests would differ
    const DecompressionMethods Supported = DecompressionMethods.GZip | DecompressionMethods.Brotli;

    static readonly string Plain = string.Concat(Enumerable.Repeat("nbomber load test ", 200));

    // --- compressed bodies ---

    [Theory]
    [InlineData("gzip", false)]
    [InlineData("gzip", true)]
    [InlineData("br", false)]
    [InlineData("br", true)]
    public async Task Compressed_body_matches_native_and_size_counts_compressed_bytes(string codec, bool chunked)
    {
        var compressed = Compress(codec, Encoding.UTF8.GetBytes(Plain));
        var (head, raw) = BuildResponse("200 OK", codec, compressed, chunked);

        var native = await SendNativeClient(Supported, raw);
        var ours = await SendNBomberClient(Supported, raw);

        AssertSameForCaller(native, ours);
        Assert.Equal(Plain, Encoding.UTF8.GetString(ours.Body!));

        // the size is the request and response as they were sent; the chunked framing isn't counted
        Assert.Equal(ours.RawRequest.Length + head.Length + compressed.Length, ours.SizeBytes);
    }

    /// A chunked body with no bytes at all. An empty body with "Content-Length: 0" is covered by Response_without_body_matches_native.
    [Theory]
    [InlineData("gzip", true)]
    [InlineData("br", true)]
    public async Task Empty_raw_body_matches_native(string contentEncoding, bool chunked)
    {
        var (head, raw) = BuildResponse("200 OK", contentEncoding, [], chunked);

        var native = await SendNativeClient(Supported, raw);
        var ours = await SendNBomberClient(Supported, raw);

        AssertSameForCaller(native, ours);
        Assert.Equal(ours.RawRequest.Length + head.Length, ours.SizeBytes);
    }

    [Fact]
    public async Task Unbuffered_body_with_content_length_is_counted_and_stays_readable()
    {
        var compressed = Compress("gzip", Encoding.UTF8.GetBytes(Plain));
        var (head, raw) = BuildResponse("200 OK", "gzip", compressed, chunked: false);

        var ours = await SendNBomberClient(Supported, raw, HttpCompletionOption.ResponseHeadersRead);

        // the original Content-Length is known before the body is read
        Assert.Equal(ours.RawRequest.Length + head.Length + compressed.Length, ours.SizeBytes);
        Assert.Equal(Plain, Encoding.UTF8.GetString(ours.Body!));
    }

    /// ReadAsStreamAsync on an unbuffered body must return the decoding stream, not load the body into a buffer first.
    [Fact]
    public async Task Unbuffered_body_is_streamed_by_ReadAsStreamAsync()
    {
        var compressed = Compress("gzip", Encoding.UTF8.GetBytes(Plain));
        var (_, raw) = BuildResponse("200 OK", "gzip", compressed, chunked: true);

        using var server = new RawServer();
        var serverTask = server.HandleOneRequest(raw);

        using var client = NBomberHttp.CreateDefaultClient(httpVersion: "1.1", automaticDecompression: DecompressionMethods.GZip);
        var result = await client.SendRequest(NBomberHttp.CreateRequest("GET", $"{server.Url}/"), httpCompletion: HttpCompletionOption.ResponseHeadersRead);

        await using var stream = await result.Payload.Value.Content.ReadAsStreamAsync();
        Assert.False(stream.CanSeek); // a buffered body would be a seekable MemoryStream
        Assert.Equal(Plain, await new StreamReader(stream).ReadToEndAsync());
        await serverTask;
    }

    [Fact]
    public async Task Unbuffered_chunked_body_is_not_counted_and_stays_readable()
    {
        var compressed = Compress("gzip", Encoding.UTF8.GetBytes(Plain));
        var (head, raw) = BuildResponse("200 OK", "gzip", compressed, chunked: true);

        var ours = await SendNBomberClient(Supported, raw, HttpCompletionOption.ResponseHeadersRead);

        // the body had not been received when the step finished
        Assert.Equal(ours.RawRequest.Length + head.Length, ours.SizeBytes);
        Assert.Equal(Plain, Encoding.UTF8.GetString(ours.Body!));
    }

    // --- responses without a body ---

    [Theory]
    [InlineData("HEAD", "200 OK", "Content-Length: 25\r\n")]
    [InlineData("GET", "204 No Content", "")]
    [InlineData("GET", "304 Not Modified", "")]
    [InlineData("GET", "200 OK", "Content-Length: 0\r\n")]
    public async Task Response_without_body_matches_native(string method, string status, string lengthHeader)
    {
        var head =
            $"HTTP/1.1 {status}\r\n" +
            "Content-Encoding: gzip\r\n" +
            lengthHeader +
            "\r\n";
        var raw = Encoding.ASCII.GetBytes(head);

        var native = await SendNativeClient(Supported, raw, method: new HttpMethod(method));
        var ours = await SendNBomberClient(Supported, raw, method: new HttpMethod(method));

        AssertSameForCaller(native, ours);
        Assert.Equal(ours.RawRequest.Length + head.Length, ours.SizeBytes);
    }

    // --- encodings that are not decoded ---

    [Theory]
    [InlineData("identity", Supported)]
    [InlineData("x-gzip", Supported)]
    [InlineData("gzip", DecompressionMethods.Brotli)] // not enabled
    public async Task Encoding_that_is_not_decoded_is_left_unchanged(string contentEncoding, DecompressionMethods methods)
    {
        var body = contentEncoding == "identity"
            ? Encoding.UTF8.GetBytes(Plain)
            : Compress("gzip", Encoding.UTF8.GetBytes(Plain));

        var (head, raw) = BuildResponse("200 OK", contentEncoding, body, chunked: false);

        var native = await SendNativeClient(methods, raw);
        var ours = await SendNBomberClient(methods, raw);

        AssertSameForCaller(native, ours);
        Assert.Equal(body, ours.Body);
        Assert.Equal(ours.RawRequest.Length + head.Length + body.Length, ours.SizeBytes);
    }

    [Fact]
    public async Task Only_the_last_of_several_encodings_is_decoded()
    {
        var gzipped = Compress("gzip", Encoding.UTF8.GetBytes(Plain));
        var compressed = Compress("br", gzipped);
        var (head, raw) = BuildResponse("200 OK", "gzip, br", compressed, chunked: false);

        var native = await SendNativeClient(Supported, raw);
        var ours = await SendNBomberClient(Supported, raw);

        AssertSameForCaller(native, ours);

        // br is decoded, gzip stays: the caller gets the gzip bytes and "Content-Encoding: gzip"
        Assert.Equal(gzipped, ours.Body);
        Assert.Equal(["gzip"], ours.Response.Content.Headers.ContentEncoding);
        Assert.Equal(ours.RawRequest.Length + head.Length + compressed.Length, ours.SizeBytes);
    }

    /// Deflate isn't supported, so these tests don't compare with the native client: .NET decodes "deflate".
    [Fact]
    public async Task Deflate_flag_is_ignored()
    {
        var raw = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\n\r\n");

        var ours = await SendNBomberClient(DecompressionMethods.Deflate, raw);

        Assert.DoesNotContain("Accept-Encoding", Encoding.ASCII.GetString(ours.RawRequest), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ours.RawRequest.Length + raw.Length, ours.SizeBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deflate_response_is_returned_compressed(bool chunked)
    {
        var compressed = Compress("deflate", Encoding.UTF8.GetBytes(Plain));
        var (head, raw) = BuildResponse("200 OK", "deflate", compressed, chunked);

        var ours = await SendNBomberClient(DecompressionMethods.All, raw);

        // the body and its headers are returned as they were received
        Assert.Equal(compressed, ours.Body);
        Assert.Equal(["deflate"], ours.Response.Content.Headers.ContentEncoding);
        Assert.Equal(ours.RawRequest.Length + head.Length + compressed.Length, ours.SizeBytes);
    }

    // --- Accept-Encoding ---

    [Theory]
    [InlineData(Supported, null)]
    [InlineData(DecompressionMethods.GZip, null)]
    [InlineData(DecompressionMethods.Brotli, null)]
    [InlineData(Supported, "br")]
    [InlineData(Supported, "identity;q=0.5")]
    [InlineData(Supported, "gzip;q=0.5")] // .NET compares only the name: "gzip" isn't added again
    [InlineData(Supported, "GZIP")]
    public async Task Accept_encoding_matches_native(DecompressionMethods methods, string? userAcceptEncoding)
    {
        var raw = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\n\r\n");

        void Configure(HttpRequestMessage request)
        {
            if (userAcceptEncoding is not null)
                request.Headers.TryAddWithoutValidation("Accept-Encoding", userAcceptEncoding);
        }

        var native = await SendNativeClient(methods, raw, configure: Configure);
        var ours = await SendNBomberClient(methods, raw, configure: Configure);

        // the request on the wire is byte-for-byte the same
        Assert.Equal(RequestText(native), RequestText(ours));
        Assert.Equal(ours.RawRequest.Length + raw.Length, ours.SizeBytes);
    }

    [Fact]
    public async Task All_methods_enable_gzip_and_brotli()
    {
        var compressed = Compress("br", Encoding.UTF8.GetBytes(Plain));
        var (_, raw) = BuildResponse("200 OK", "br", compressed, chunked: false);

        var ours = await SendNBomberClient(DecompressionMethods.All, raw);

        Assert.Contains("Accept-Encoding: gzip, br\r\n", Encoding.ASCII.GetString(ours.RawRequest));
        Assert.Equal(Plain, Encoding.UTF8.GetString(ours.Body!));
    }

    // --- typed send ---

    record Payload(int Id, string Name);

    [Fact]
    public async Task Typed_send_deserializes_gzipped_json_and_counts_compressed_bytes()
    {
        var compressed = Compress("gzip", """{"id":42,"name":"nbomber"}"""u8.ToArray());
        var (head, raw) = BuildResponse("200 OK", "gzip", compressed, chunked: false, contentType: "application/json");

        using var server = new RawServer();
        var serverTask = server.HandleOneRequest(raw);

        using var client = NBomberHttp.CreateDefaultClient(httpVersion: "1.1", automaticDecompression: DecompressionMethods.GZip);
        var response = await client.SendRequest<Payload>(NBomberHttp.CreateRequest("GET", $"{server.Url}/"));
        var rawRequest = await serverTask;

        Assert.False(response.IsError, response.Message);
        Assert.Equal(new Payload(42, "nbomber"), response.Payload.Value.Data);
        Assert.Equal(rawRequest.Length + head.Length + compressed.Length, response.SizeBytes);
    }

    /// An empty gzip body is about 20 bytes, so the compressed size is not 0. The body is still empty
    /// for the caller, so there is nothing to deserialize and the response is not an error.
    [Fact]
    public async Task Typed_send_treats_gzipped_empty_body_as_empty()
    {
        var compressed = Compress("gzip", []);
        var (head, raw) = BuildResponse("200 OK", "gzip", compressed, chunked: false, contentType: "application/json");

        using var server = new RawServer();
        var serverTask = server.HandleOneRequest(raw);

        using var client = NBomberHttp.CreateDefaultClient(httpVersion: "1.1", automaticDecompression: DecompressionMethods.GZip);
        var response = await client.SendRequest<Payload>(NBomberHttp.CreateRequest("GET", $"{server.Url}/"));
        var rawRequest = await serverTask;

        Assert.False(response.IsError, response.Message);
        Assert.Null(response.Payload.Value.Data);
        Assert.Equal(rawRequest.Length + head.Length + compressed.Length, response.SizeBytes);
    }

    // --- HTTP/2 ---

    /// HTTP/2 sizes are calculated as HTTP/1.1 messages, so they aren't compared with the bytes on the wire.
    /// Compared with .NET decompression, the size must differ by exactly the compressed vs decompressed body
    /// and the Content-Length and Content-Encoding lines that are counted again.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Http2_body_matches_native_and_size_counts_compressed_bytes(bool withContentLength)
    {
        var plain = Encoding.UTF8.GetBytes(Plain);
        var compressed = Compress("gzip", plain);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0, l => l.Protocols = HttpProtocols.Http2));
        await using var app = builder.Build();
        app.MapGet("/", async (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "text/plain";
            ctx.Response.Headers.ContentEncoding = "gzip";
            if (withContentLength) ctx.Response.ContentLength = compressed.Length;
            await ctx.Response.Body.WriteAsync(compressed);
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        using var nativeClient = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.GZip });
        var nativeRequest = NBomberHttp.CreateRequest("GET", address);
        nativeRequest.Version = System.Net.HttpVersion.Version20;
        nativeRequest.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        var native = await nativeClient.SendRequest(nativeRequest);

        using var ourClient = NBomberHttp.CreateDefaultClient(
            httpVersion: "2.0", versionPolicy: HttpVersionPolicy.RequestVersionExact,
            automaticDecompression: DecompressionMethods.GZip);
        var ours = await ourClient.SendRequest(NBomberHttp.CreateRequest("GET", address));

        Assert.Equal(System.Net.HttpVersion.Version20, ours.Payload.Value.Version);
        Assert.Equal(plain, await native.Payload.Value.Content.ReadAsByteArrayAsync());
        Assert.Equal(plain, await ours.Payload.Value.Content.ReadAsByteArrayAsync());

        var countedAgain =
            "Content-Encoding: gzip\r\n".Length +
            (withContentLength ? $"Content-Length: {compressed.Length}\r\n".Length : 0);

        Assert.Equal(native.SizeBytes - plain.Length + compressed.Length + countedAgain, ours.SizeBytes);
    }

    // --- helpers ---

    record Sent(Response<HttpResponseMessage> Result, HttpResponseMessage Response, byte[]? Body,
                string? Error, string ContentHeaders, byte[] RawRequest)
    {
        public long SizeBytes => Result.SizeBytes;
    }

    /// What the caller sees must be the same: status, content headers, and the body (or the error reading it).
    static void AssertSameForCaller(Sent native, Sent ours)
    {
        Assert.Equal(native.Response.StatusCode, ours.Response.StatusCode);
        Assert.Equal(native.ContentHeaders, ours.ContentHeaders);
        Assert.Equal(native.Error, ours.Error);
        Assert.Equal(native.Body, ours.Body);
        Assert.Equal(RequestText(native), RequestText(ours));
    }

    /// The raw request without the server port: native and ours are sent to two servers with different ports.
    static string RequestText(Sent sent) =>
        Regex.Replace(Encoding.ASCII.GetString(sent.RawRequest), @"Host: 127\.0\.0\.1:\d+", "Host: 127.0.0.1");

    static Task<Sent> SendNativeClient(DecompressionMethods methods, byte[] rawResponse,
                                 HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead,
                                 HttpMethod? method = null, Action<HttpRequestMessage>? configure = null) =>
        Send(() => new HttpClient(new SocketsHttpHandler { AutomaticDecompression = methods }),
             rawResponse, completion, method, configure);

    static Task<Sent> SendNBomberClient(DecompressionMethods methods, byte[] rawResponse,
                               HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead,
                               HttpMethod? method = null, Action<HttpRequestMessage>? configure = null) =>
        Send(() => NBomberHttp.CreateDefaultClient(httpVersion: "1.1", automaticDecompression: methods),
             rawResponse, completion, method, configure);

    static async Task<Sent> Send(Func<HttpClient> createClient, byte[] rawResponse, HttpCompletionOption completion,
                                 HttpMethod? method, Action<HttpRequestMessage>? configure)
    {
        using var server = new RawServer();
        var serverTask = server.HandleOneRequest(rawResponse);

        using var client = createClient();
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, $"{server.Url}/");
        configure?.Invoke(request);

        var result = await client.SendRequest(request, httpCompletion: completion);
        var response = result.Payload.Value;

        // read AFTER the size was calculated, the way a scenario would
        byte[]? body = null;
        string? error = null;
        try
        {
            body = await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
        }

        var contentHeaders = string.Join("\n",
            response.Content.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}").Order());

        return new Sent(result, response, body, error, contentHeaders, await serverTask);
    }

    static (string Head, byte[] Raw) BuildResponse(string status, string contentEncoding, byte[] body, bool chunked,
                                                   string contentType = "text/plain")
    {
        var head =
            $"HTTP/1.1 {status}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Encoding: {contentEncoding}\r\n" +
            (chunked ? "Transfer-Encoding: chunked\r\n" : $"Content-Length: {body.Length}\r\n") +
            "\r\n";

        var raw = new MemoryStream();
        raw.Write(Encoding.ASCII.GetBytes(head));

        if (!chunked)
            raw.Write(body);
        else
        {
            // several chunks, so the body is read in more than one piece
            foreach (var chunk in body.Chunk(100))
            {
                raw.Write(Encoding.ASCII.GetBytes($"{chunk.Length:X}\r\n"));
                raw.Write(chunk);
                raw.Write("\r\n"u8);
            }
            raw.Write("0\r\n\r\n"u8);
        }

        return (head, raw.ToArray());
    }

    static byte[] Compress(string codec, byte[] data)
    {
        using var output = new MemoryStream();
        using (Stream encoder = codec switch
        {
            "gzip" => new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true),
            "deflate" => new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true), // only to test that it isn't decoded
            "br" => new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true),
            _ => throw new ArgumentException(codec)
        })
        {
            encoder.Write(data);
        }
        return output.ToArray();
    }

    /// Receives one request, sends the raw response and returns the raw request bytes.
    sealed class RawServer : IDisposable
    {
        readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public RawServer() => _listener.Start();

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        public async Task<byte[]> HandleOneRequest(byte[] rawResponse)
        {
            using var tcp = await _listener.AcceptTcpClientAsync();
            var stream = tcp.GetStream();
            var received = new List<byte>();
            var one = new byte[1];

            while (true)
            {
                var n = await stream.ReadAsync(one);
                if (n == 0)
                    break;

                received.Add(one[0]);

                if (IsEndOfHeaders(received))
                    break;
            }

            await stream.WriteAsync(rawResponse);
            await Task.Delay(200);

            return received.ToArray();

            // Returns true when the request head (request line + headers) is fully received.
            // The head always ends with an empty line, so the last 4 bytes are CR LF CR LF.
            // Note: a request body (if any) is not read.
            static bool IsEndOfHeaders(List<byte> data) =>
                data is [.., (byte)'\r', _, _, _] && data[^3] == (byte)'\n' &&
                data[^2] == (byte)'\r' && data[^1] == (byte)'\n';
        }

        public void Dispose() => _listener.Stop();
    }
}
