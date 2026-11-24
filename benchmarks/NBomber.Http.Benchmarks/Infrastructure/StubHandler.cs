using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace NBomber.Http.Benchmarks.Infrastructure;

public class User
{
    public int UserId { get; set; }
    public string? Title { get; set; }
}

/// An in-memory handler that returns a new canned response for every request, without network I/O.
/// Benchmarks that use it measure only the overhead of HttpClient and NBomber.Http.
/// The response is selected by the request path:
/// - /text: a text body with Content-Length;
/// - /user: a JSON body with Content-Length;
/// - /chunked: a text body without a known length (as a chunked HTTP/1.1 response).
/// A request body is read to the end, as a real handler sends it.
public sealed class StubHandler : HttpMessageHandler
{
    public const string BaseUrl = "http://localhost:5000";

    static readonly byte[] TextBody = Encoding.UTF8.GetBytes(new string('a', 1024));
    static readonly byte[] JsonBody = Encoding.UTF8.GetBytes("""{"userId":7,"title":"anton"}""");
    static readonly MediaTypeHeaderValue TextType = new("text/plain") { CharSet = "utf-8" };
    static readonly MediaTypeHeaderValue JsonType = new("application/json") { CharSet = "utf-8" };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
            await request.Content.CopyToAsync(Stream.Null, cancellationToken);

        return CreateResponse(request);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Content?.CopyTo(Stream.Null, null, cancellationToken);
        return CreateResponse(request);
    }

    static HttpResponseMessage CreateResponse(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;

        HttpContent content = path switch
        {
            "/user"    => new ByteArrayContent(JsonBody),
            "/chunked" => new StreamContent(new NonSeekableStream(TextBody)),
            _          => new ByteArrayContent(TextBody)
        };

        content.Headers.ContentType = path == "/user" ? JsonType : TextType;

        // SocketsHttpHandler puts the received Content-Length header into the collection.
        // ByteArrayContent only calculates it when the ContentLength property is read, so it is set here explicitly,
        // otherwise the response would not look like a real one.
        if (content is ByteArrayContent)
            content.Headers.ContentLength = path == "/user" ? JsonBody.Length : TextBody.Length;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
            RequestMessage = request,
            Version = System.Net.HttpVersion.Version11
        };

        response.Headers.TryAddWithoutValidation("Server", "Kestrel");
        response.Headers.TryAddWithoutValidation("Date", "Tue, 24 Nov 2026 10:00:00 GMT");

        if (path == "/chunked")
            response.Headers.TransferEncodingChunked = true;

        return response;
    }
}

/// A read-only stream that can't seek, so StreamContent can't compute its length.
public sealed class NonSeekableStream(byte[] data) : Stream
{
    int _position;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, data.Length - _position);
        data.AsSpan(_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer, offset, count));

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
