using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NBomber.Http.CSharp;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Tests;

/// <summary>
/// Covers the automaticDecompression parameter of CreateDefaultClient. Send&lt;T&gt; deserializes the body
/// as it arrives, so without decompression a gzipped JSON body can't be deserialized.
/// </summary>
public class HttpDecompressionTest
{
    const string Json = """{"id":42,"name":"nbomber"}""";

    record Payload(int Id, string Name);

    /// The bug the parameter fixes: the deserializer is handed raw gzip bytes.
    [Fact]
    public async Task Gzipped_json_cannot_be_deserialized_without_decompression()
    {
        using var server = new GzipJsonServer(Json);
        var serverTask = server.HandleOneRequest();

        using var client = NBomberHttp.CreateDefaultClient(httpVersion: "1.1");
        var request = NBomberHttp.CreateRequest("GET", $"{server.Url}/api/data");
        var response = await client.SendRequest<Payload>(request);

        var raw = Encoding.Latin1.GetString(await serverTask);

        // the client never asked for compression, so it can't be expected to undo it
        Assert.DoesNotContain("Accept-Encoding", raw, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.IsError);
        Assert.Contains("JSON deserialization failed", response.Message);
    }

    [Fact]
    public async Task Gzipped_json_is_deserialized_with_decompression_enabled()
    {
        using var server = new GzipJsonServer(Json);
        var serverTask = server.HandleOneRequest();

        using var client = NBomberHttp.CreateDefaultClient(httpVersion: "1.1", automaticDecompression: DecompressionMethods.GZip);

        var request = NBomberHttp.CreateRequest("GET", $"{server.Url}/api/data");
        var response = await client.SendRequest<Payload>(request);

        var raw = Encoding.Latin1.GetString(await serverTask);

        // .NET adds the header itself once AutomaticDecompression is set
        Assert.Contains("Accept-Encoding", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gzip", raw, StringComparison.OrdinalIgnoreCase);

        Assert.False(response.IsError);
        Assert.Equal(new Payload(42, "nbomber"), response.Payload.Value.Data);
    }

    [Fact]
    public async Task Gzipped_body_reads_back_decompressed_with_the_untyped_send()
    {
        using var server = new GzipJsonServer(Json);
        var serverTask = server.HandleOneRequest();

        using var client = NBomberHttp.CreateDefaultClient(
            httpVersion: "1.1", automaticDecompression: DecompressionMethods.GZip);

        var request = NBomberHttp.CreateRequest("GET", $"{server.Url}/api/data");
        var response = await client.SendRequest(request);
        var body = await response.Payload.Value.Content.ReadAsStringAsync();

        await serverTask;

        Assert.Equal(Json, body);

        // the caller sees the headers .NET would leave after decompressing; the size still counts the original ones
        // (see HttpCountingDecompressionTest)
        Assert.False(response.Payload.Value.Content.Headers.Contains("Content-Length"));
        Assert.Empty(response.Payload.Value.Content.Headers.ContentEncoding);
    }

    /// The default must stay exactly as it was: no Accept-Encoding, and a byte-exact size.
    [Fact]
    public async Task Default_client_requests_no_compression_and_stays_byte_exact()
    {
        const string rawResponse = "HTTP/1.1 204 No Content\r\n\r\n";

        using var server = new GzipJsonServer(Json);
        var serverTask = server.HandleOneRequest(Encoding.ASCII.GetBytes(rawResponse));

        using var client = NBomberHttp.CreateDefaultClient(httpVersion: "1.1");
        var request = NBomberHttp.CreateRequest("GET", $"{server.Url}/");
        var response = await client.SendRequest(request);

        var rawRequest = await serverTask;

        Assert.DoesNotContain("Accept-Encoding", Encoding.Latin1.GetString(rawRequest),
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(rawRequest.Length + rawResponse.Length, response.SizeBytes);
    }

    /// Serves one gzipped JSON response over a raw socket and returns the request bytes it received.
    sealed class GzipJsonServer(string json) : IDisposable
    {
        readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        bool _started;

        public string Url
        {
            get
            {
                if (!_started) { _listener.Start(); _started = true; }
                return $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            }
        }

        public Task<byte[]> HandleOneRequest() => HandleOneRequest(BuildGzipResponse(json));

        public async Task<byte[]> HandleOneRequest(byte[] rawResponse)
        {
            if (!_started)
            {
                _listener.Start();
                _started = true;
            }

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
                data.Count >= 4 &&
                data[^4] == (byte)'\r' && data[^3] == (byte)'\n' &&
                data[^2] == (byte)'\r' && data[^1] == (byte)'\n';
        }

        static byte[] BuildGzipResponse(string json)
        {
            var gzipped = Gzip(json);

            var head =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json\r\n" +
                "Content-Encoding: gzip\r\n" +
                $"Content-Length: {gzipped.Length}\r\n" +
                "\r\n";

            return Encoding.ASCII.GetBytes(head).Concat(gzipped).ToArray();
        }

        static byte[] Gzip(string text)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(Encoding.UTF8.GetBytes(text));
            return output.ToArray();
        }

        public void Dispose() => _listener.Stop();
    }
}
