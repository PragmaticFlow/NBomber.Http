using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NBomber.Contracts;
using NBomberHttp = NBomber.Http.CSharp.Http;

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
        Assert.Equal(rawResponse.Length, await HttpImpl.calcResponseSize(true, response));
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
        Assert.Equal(rawResponse.Length, await HttpImpl.calcResponseSize(true, response));
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
        Assert.Equal(rawResponse.Length, await HttpImpl.calcResponseSize(true, response));
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

        Assert.Equal(rawResponse.Length - framingSize, await HttpImpl.calcResponseSize(true, response));

        // the server did not send Content-Length, so the calculation must not add it
        Assert.False(response.Content.Headers.Contains("Content-Length"));
    }

    /// Pins the property D4 was about: the calculation reads headers, it never writes them.
    /// The HttpContentHeaders.ContentLength property would add a Content-Length header to a chunked
    /// response while computing its length, which makes the result depend on the order of the calls.
    /// This passes both before and after that change - it exists to stop the property being lost again.
    [Fact]
    public async Task Calculating_the_size_does_not_change_the_response_headers()
    {
        const string rawResponse =
            "HTTP/1.1 200 OK\r\n" +
            "Transfer-Encoding: chunked\r\n" +
            "\r\n" +
            "5\r\nhello\r\n0\r\n\r\n";

        var (_, response, _) = await Exchange(
            url => new HttpRequestMessage(HttpMethod.Get, $"{url}/"),
            rawResponse);

        var before = response.Content.Headers.ToString();

        var first = await HttpImpl.calcResponseSize(true, response);
        var second = await HttpImpl.calcResponseSize(true, response);

        // repeating the calculation must not drift, and must leave the headers as they arrived
        Assert.Equal(first, second);
        Assert.Equal(before, response.Content.Headers.ToString());
        Assert.False(response.Content.Headers.Contains("Content-Length"));
    }

    [Fact]
    public async Task Head_response_body_is_not_counted()
    {
        const string rawResponse = "HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n";

        var (_, response, _) = await Exchange(
            url => new HttpRequestMessage(HttpMethod.Head, $"{url}/"),
            rawResponse);

        Assert.Equal(rawResponse.Length, await HttpImpl.calcResponseSize(true, response));
    }

    /// A body with an unknown length is sent chunked. Http.Send wraps it to count the payload,
    /// so the size is the head plus the payload (the chunked framing stays uncounted).
    [Fact]
    public async Task Request_body_with_unknown_length_is_counted()
    {
        const string rawResponse = "HTTP/1.1 204 No Content\r\n\r\n";

        // JsonContent can't calculate its length, so HttpClient sends it with Transfer-Encoding: chunked
        var (response, rawRequest, _) = await ExchangeViaSend(url =>
            new HttpRequestMessage(HttpMethod.Post, $"{url}/") { Content = JsonContent.Create(new { id = 1 }) },
            rawResponse);

        var head = Encoding.Latin1.GetString(rawRequest)[..HeadLength(rawRequest)];

        // the wire format must not change: still chunked, still no Content-Length
        Assert.Contains("Transfer-Encoding: chunked", head, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content-Length", head, StringComparison.OrdinalIgnoreCase);

        var payloadSize = DecodedChunkedBodyLength(rawRequest);
        Assert.True(payloadSize > 0, "the probe needs a non-empty chunked body");

        Assert.Equal(HeadLength(rawRequest) + payloadSize + rawResponse.Length, response.SizeBytes);
    }

    /// The wrapper is swapped in only for the send, so the caller keeps the content it created.
    [Fact]
    public async Task Request_body_with_unknown_length_is_restored_after_the_send()
    {
        const string rawResponse = "HTTP/1.1 204 No Content\r\n\r\n";
        var content = JsonContent.Create(new { id = 1 });

        var (_, _, request) = await ExchangeViaSend(
            url => new HttpRequestMessage(HttpMethod.Post, $"{url}/") { Content = content },
            rawResponse);

        Assert.Same(content, request.Content);
    }

    /// A body with a known length must keep going through the Content-Length path, unwrapped.
    [Fact]
    public async Task Request_body_with_known_length_is_unchanged()
    {
        const string rawResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

        var (response, rawRequest, request) = await ExchangeViaSend(url =>
            new HttpRequestMessage(HttpMethod.Post, $"{url}/api/users")
            {
                Content = new StringContent("hello", Encoding.UTF8, "text/plain")
            },
            rawResponse);

        var head = Encoding.Latin1.GetString(rawRequest)[..HeadLength(rawRequest)];
        Assert.Contains("Content-Length: 5", head, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Transfer-Encoding", head, StringComparison.OrdinalIgnoreCase);

        // known length: the calculation stays byte-exact against the real bytes
        Assert.Equal(rawRequest.Length + rawResponse.Length, response.SizeBytes);
        Assert.IsType<StringContent>(request.Content);
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

    /// Sends through Http.Send, so the size is the one NBomber reports for the step.
    static async Task<(Response<HttpResponseMessage>, byte[], HttpRequestMessage)> ExchangeViaSend(
        Func<string, HttpRequestMessage> createRequest, string rawResponse)
    {
        using var server = new RawHttpServer();
        using var client = new HttpClient();

        var serverTask = server.HandleOneRequest(rawResponse);
        var request = createRequest(server.Url);
        var response = await NBomberHttp.Send(client, request);
        var rawRequest = await serverTask;

        return (response, rawRequest, request);
    }

    static int HeadLength(byte[] raw) =>
        Encoding.Latin1.GetString(raw).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;

    /// Sums the chunk data of a chunked request body, excluding the framing.
    static int DecodedChunkedBodyLength(byte[] raw)
    {
        var text = Encoding.Latin1.GetString(raw);
        var position = HeadLength(raw);
        var total = 0;

        while (true)
        {
            var lineEnd = text.IndexOf("\r\n", position, StringComparison.Ordinal);
            if (lineEnd < 0)
                break;

            // a chunk size line may carry extensions after a ';'
            var sizeText = text[position..lineEnd].Split(';')[0].Trim();
            if (!int.TryParse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var size) || size == 0)
                break;

            total += size;
            position = lineEnd + 2 + size + 2; // data + the CRLF that closes the chunk
        }

        return total;
    }

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
                if (count == 0)
                    break;

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
