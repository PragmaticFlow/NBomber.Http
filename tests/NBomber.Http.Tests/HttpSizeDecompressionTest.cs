using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NBomber.Contracts;
using NBomberHttp = NBomber.Http.CSharp.Http;

namespace NBomber.Http.Tests;

/// <summary>
/// Covers responses the handler decompresses. .NET replaces the content with a decompressed one that
/// drops Content-Length and clears Content-Encoding, and whose ContentLength stays null even after the
/// body is read - so the body length can only come from the buffered content.
/// </summary>
public class HttpSizeDecompressionTest
{
    const int PlainSize = 2000;

    [Fact]
    public async Task Decompressed_body_is_counted_when_buffered()
    {
        var x = await Exchange(DecompressionMethods.GZip, HttpCompletionOption.ResponseContentRead);

        // the body still arrives decompressed for the caller
        Assert.Equal(PlainSize, x.Body.Length);

        // the request is a plain GET, so its size is exactly the bytes the server received
        Assert.Equal(x.RawRequest.Length + CountedHead(x) + PlainSize, x.Response.SizeBytes);
    }

    /// Without buffering the body isn't read yet, so its length can't be known. The important part is
    /// that probing for it must not consume the stream: the caller still has to be able to read the body.
    [Fact]
    public async Task Unbuffered_decompressed_body_is_not_counted_but_stays_readable()
    {
        var x = await Exchange(DecompressionMethods.GZip, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(PlainSize, x.Body.Length);

        // the body is not part of the size: it had not been transferred when the step finished
        Assert.Equal(x.RawRequest.Length + CountedHead(x), x.Response.SizeBytes);
    }

    /// The default: no decompression, so the headers carry the real length and the size stays byte-exact.
    [Fact]
    public async Task Compressed_body_without_decompression_is_byte_exact()
    {
        var x = await Exchange(DecompressionMethods.None, HttpCompletionOption.ResponseContentRead);

        // the caller gets the compressed bytes, because nothing decompressed them
        Assert.Equal(x.Gzipped.Length, x.Body.Length);

        Assert.Equal(x.RawRequest.Length + x.Head.Length + x.Gzipped.Length, x.Response.SizeBytes);
    }

    /// The head that can still be measured after decompression: .NET dropped Content-Length and
    /// cleared Content-Encoding, so those lines are gone from the response object.
    static int CountedHead(Exchanged x) =>
        x.Head.Length
        - "Content-Encoding: gzip\r\n".Length
        - $"Content-Length: {x.Gzipped.Length}\r\n".Length;

    record Exchanged(Response<HttpResponseMessage> Response, byte[] Body, string Head, byte[] Gzipped, byte[] RawRequest);

    static async Task<Exchanged> Exchange(
        DecompressionMethods decompression, HttpCompletionOption completion)
    {
        var gzipped = Gzip(new string('a', PlainSize));

        var head =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/plain\r\n" +
            "Content-Encoding: gzip\r\n" +
            $"Content-Length: {gzipped.Length}\r\n" +
            "\r\n";

        var rawResponse = Encoding.ASCII.GetBytes(head).Concat(gzipped).ToArray();

        using var server = new BinaryHttpServer();
        var serverTask = server.HandleOneRequest(rawResponse);

        using var handler = new SocketsHttpHandler { AutomaticDecompression = decompression };
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, $"{server.Url}/");
        if (decompression == DecompressionMethods.None)
            request.Headers.Add("Accept-Encoding", "gzip");

        var clientArgs = HttpClientArgs.Create(httpCompletion: completion);
        var response = await NBomberHttp.Send(client, clientArgs, request);

        // read AFTER the size was calculated, the way a scenario would
        var body = await response.Payload.Value.Content.ReadAsByteArrayAsync();
        var rawRequest = await serverTask;

        return new Exchanged(response, body, head, gzipped, rawRequest);
    }

    static byte[] Gzip(string text)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(text));
        return output.ToArray();
    }

    sealed class BinaryHttpServer : IDisposable
    {
        readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public BinaryHttpServer() => _listener.Start();

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        /// Receives one request, sends the raw response and returns the raw request bytes.
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
                data.Count >= 4 &&
                data[^4] == (byte)'\r' && data[^3] == (byte)'\n' &&
                data[^2] == (byte)'\r' && data[^1] == (byte)'\n';
        }

        public void Dispose() => _listener.Stop();
    }
}
