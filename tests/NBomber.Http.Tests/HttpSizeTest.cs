using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace NBomber.Http.Tests;

/// <summary>
/// Compares calcRequestSize/calcResponseSize with the real HTTP/1.1 bytes:
/// a raw TCP server records the request bytes that HttpClient sends and returns a raw response.
/// </summary>
public class HttpSizeTest
{
    [Fact]
    public async Task Get_without_body()
    {
        const string rawResponse = "HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n";

        var (request, response, rawRequest) = await Exchange(
            url => new HttpRequestMessage(HttpMethod.Get, $"{url}/api/ping?id=1"),
            rawResponse);

        Assert.Equal(rawRequest.Length, HttpImpl.calcRequestSize(request));
        Assert.Equal(rawResponse.Length, HttpImpl.calcResponseSize(response));
    }

    [Fact]
    public async Task Post_with_body_and_multi_value_headers()
    {
        const string rawResponse =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: application/json\r\n" +
            "Content-Length: 13\r\n" +
            "Set-Cookie: a=1\r\n" +
            "Set-Cookie: b=2\r\n" +
            "\r\n" +
            "{\"id\":\"1234\"}";

        var (request, response, rawRequest) = await Exchange(url =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/users");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("a", "1"));
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("b", "2"));
            req.Content = new StringContent("hello", Encoding.UTF8, "text/plain");
            return req;
        }, rawResponse);

        Assert.Equal(rawRequest.Length, HttpImpl.calcRequestSize(request));
        Assert.Equal(rawResponse.Length, HttpImpl.calcResponseSize(response));
    }

    [Fact]
    public async Task Host_header_set_by_user_is_counted_once()
    {
        const string rawResponse = "HTTP/1.1 204 No Content\r\n\r\n";

        var (request, response, rawRequest) = await Exchange(url =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/");
            req.Headers.Host = "example.com";
            return req;
        }, rawResponse);

        Assert.Equal(rawRequest.Length, HttpImpl.calcRequestSize(request));
        Assert.Equal(rawResponse.Length, HttpImpl.calcResponseSize(response));
    }

    [Fact]
    public async Task Chunked_response_body_is_counted_and_headers_are_not_changed()
    {
        const string rawResponse =
            "HTTP/1.1 200 OK\r\n" +
            "Transfer-Encoding: chunked\r\n" +
            "\r\n" +
            "5\r\nhello\r\n0\r\n\r\n";

        // the chunked framing is not counted: "5\r\n", "\r\n" after the data and "0\r\n\r\n"
        const int framingSize = 3 + 2 + 5;

        var (_, response, _) = await Exchange(
            url => new HttpRequestMessage(HttpMethod.Get, $"{url}/"),
            rawResponse);

        Assert.Equal(rawResponse.Length - framingSize, HttpImpl.calcResponseSize(response));

        // the server did not send Content-Length, so the calculation must not add it
        Assert.False(response.Content.Headers.Contains("Content-Length"));
    }

    [Fact]
    public async Task Head_response_body_is_not_counted()
    {
        const string rawResponse = "HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n";

        var (_, response, _) = await Exchange(
            url => new HttpRequestMessage(HttpMethod.Head, $"{url}/"),
            rawResponse);

        Assert.Equal(rawResponse.Length, HttpImpl.calcResponseSize(response));
    }

    [Fact]
    public async Task Request_body_with_unknown_length_is_not_counted()
    {
        const string rawResponse = "HTTP/1.1 204 No Content\r\n\r\n";

        // JsonContent can't calculate its length, so HttpClient sends it with Transfer-Encoding: chunked
        var (request, _, rawRequest) = await Exchange(url =>
            new HttpRequestMessage(HttpMethod.Post, $"{url}/") { Content = JsonContent.Create(new { id = 1 }) },
            rawResponse);

        Assert.Equal(HeadLength(rawRequest), HttpImpl.calcRequestSize(request));
    }

    static async Task<(HttpRequestMessage, HttpResponseMessage, byte[])> Exchange(
        Func<string, HttpRequestMessage> createRequest, string rawResponse)
    {
        using var server = new RawHttpServer();
        using var client = new HttpClient();

        var serverTask = server.HandleOneRequest(rawResponse);
        var request = createRequest(server.Url);
        var response = await client.SendAsync(request);
        var rawRequest = await serverTask;

        return (request, response, rawRequest);
    }

    static int HeadLength(byte[] raw) =>
        Encoding.Latin1.GetString(raw).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;

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

            var head = text[..headEnd];
            var contentLength = Regex.Match(head, @"Content-Length: (\d+)", RegexOptions.IgnoreCase);

            if (contentLength.Success)
                return data.Count >= headEnd + 4 + int.Parse(contentLength.Groups[1].Value);

            if (head.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
                return text.EndsWith("0\r\n\r\n", StringComparison.Ordinal);

            return true;
        }

        public void Dispose() => _listener.Stop();
    }
}
