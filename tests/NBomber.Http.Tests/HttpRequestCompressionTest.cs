using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NBomber.Contracts;
using NBomber.Http.CSharp;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Tests;

/// <summary>
/// Covers request body compression (WithCompression / WithJsonBody(compression)):
/// a raw TCP server records the request bytes that HttpClient sends, so the wire format and the reported size can be checked.
/// </summary>
public class HttpRequestCompressionTest
{
    const string RawResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    record User(string Name, string Bio);

    static readonly User Data = new("Anton", new string('a', 2000));

    [Theory]
    [InlineData(RequestCompression.GZip, "gzip")]
    [InlineData(RequestCompression.Brotli, "br")]
    public async Task Json_body_is_compressed_and_sent_with_content_length(RequestCompression compression, string token)
    {
        var (response, rawRequest) = await Exchange(url =>
            NBomberHttp.CreateRequest("POST", $"{url}/api/users")
                .WithJsonBody(Data, compression: compression));

        var head = Head(rawRequest);
        var body = Body(rawRequest);

        Assert.Contains($"Content-Encoding: {token}\r\n", head, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Content-Length: {body.Length}\r\n", head, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Transfer-Encoding", head, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Type: application/json; charset=utf-8\r\n", head, StringComparison.OrdinalIgnoreCase);

        // the body is really compressed, and it decompresses back to the original JSON
        var expectedJson = JsonSerializer.Serialize(Data, NBomberHttp.GlobalJsonSerializerOptions);
        Assert.True(body.Length < Encoding.UTF8.GetByteCount(expectedJson));
        Assert.Equal(expectedJson, Decompress(compression, body));

        // the compressed body has a known length, so the size stays byte-exact
        Assert.Equal(rawRequest.Length + RawResponse.Length, response.SizeBytes);
    }

    [Fact]
    public async Task Any_body_can_be_compressed()
    {
        const string text = "hello hello hello hello hello hello hello hello";

        var (response, rawRequest) = await Exchange(url =>
            NBomberHttp.CreateRequest("POST", $"{url}/api/text")
                .WithBody(new StringContent(text, Encoding.UTF8, "text/plain"))
                .WithCompression(RequestCompression.GZip));

        var head = Head(rawRequest);

        Assert.Contains("Content-Encoding: gzip\r\n", head, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Type: text/plain; charset=utf-8\r\n", head, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(text, Decompress(RequestCompression.GZip, Body(rawRequest)));
        Assert.Equal(rawRequest.Length + RawResponse.Length, response.SizeBytes);
    }

    [Fact]
    public void None_does_not_change_the_request()
    {
        var content = new StringContent("hello");
        var request = NBomberHttp.CreateRequest("POST", "http://localhost/")
            .WithBody(content)
            .WithCompression(RequestCompression.None);

        Assert.Same(content, request.Content);
        Assert.Empty(request.Content.Headers.ContentEncoding);
    }

    [Fact]
    public void Request_without_body_is_not_changed()
    {
        var request = NBomberHttp.CreateRequest("GET", "http://localhost/")
            .WithCompression(RequestCompression.GZip);

        Assert.Null(request.Content);
    }

    static string Decompress(RequestCompression compression, byte[] body)
    {
        using var input = new MemoryStream(body);
        using Stream decompressor = compression switch
        {
            RequestCompression.GZip => new GZipStream(input, CompressionMode.Decompress),
            RequestCompression.Brotli => new BrotliStream(input, CompressionMode.Decompress),
            _ => throw new ArgumentOutOfRangeException(nameof(compression))
        };
        using var reader = new StreamReader(decompressor, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    static async Task<(Response<HttpResponseMessage>, byte[])> Exchange(Func<string, HttpRequestMessage> createRequest)
    {
        using var server = new RawHttpServer();
        using var client = new HttpClient();

        var serverTask = server.HandleOneRequest(RawResponse);
        var request = createRequest(server.Url);
        var response = await NBomberHttp.Send(client, request);
        var rawRequest = await serverTask;

        return (response, rawRequest);
    }

    static int HeadLength(byte[] raw) =>
        Encoding.Latin1.GetString(raw).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;

    static string Head(byte[] raw) => Encoding.Latin1.GetString(raw)[..HeadLength(raw)];

    static byte[] Body(byte[] raw) => raw[HeadLength(raw)..];

    sealed class RawHttpServer : IDisposable
    {
        readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public RawHttpServer() => _listener.Start();

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        /// Receives one request, sends the raw response and returns the raw request bytes.
        public async Task<byte[]> HandleOneRequest(string rawResponse)
        {
            using var socket = await _listener.AcceptTcpClientAsync();
            var stream = socket.GetStream();
            var received = new List<byte>();
            var buffer = new byte[4096];

            while (!IsRequestComplete(received))
            {
                var count = await stream.ReadAsync(buffer);
                if (count == 0) break;
                received.AddRange(buffer.Take(count));
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes(rawResponse));
            return received.ToArray();
        }

        static bool IsRequestComplete(List<byte> data)
        {
            var text = Encoding.Latin1.GetString(data.ToArray());
            var headEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headEnd < 0) return false;

            var contentLength = Regex.Match(text[..headEnd], @"Content-Length: (\d+)", RegexOptions.IgnoreCase);

            return !contentLength.Success
                   || data.Count >= headEnd + 4 + int.Parse(contentLength.Groups[1].Value);
        }

        public void Dispose() => _listener.Stop();
    }
}
