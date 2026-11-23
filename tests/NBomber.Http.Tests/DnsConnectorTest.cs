using System.Net;
using System.Net.Sockets;

namespace NBomber.Http.Tests;

public class DnsConnectorTest
{
    static readonly IPAddress Ip1 = IPAddress.Parse("127.0.0.1");
    static readonly IPAddress Ip2 = IPAddress.Parse("127.0.0.2"); // all of 127.0.0.0/8 is loopback on Windows and Linux

    static DnsConnector CreateConnector(DnsSelection selection, params IPAddress[] addresses) =>
        new(selection, (_, _) => Task.FromResult(addresses));

    static TcpListener StartListener(IPAddress address, int port)
    {
        var listener = new TcpListener(address, port);
        listener.Start();
        return listener;
    }

    static IPAddress RemoteAddress(Stream stream) =>
        ((IPEndPoint)((NetworkStream)stream).Socket.RemoteEndPoint!).Address;

    [Fact]
    public void PickStartIndex_Default_AlwaysReturnsZero()
    {
        var connector = CreateConnector(DnsSelection.Default);
        var indexes = Enumerable.Range(0, 5).Select(_ => connector.PickStartIndex(3));
        Assert.All(indexes, i => Assert.Equal(0, i));
    }

    [Fact]
    public void PickStartIndex_RoundRobin_IteratesInSequence()
    {
        var connector = CreateConnector(DnsSelection.RoundRobin);
        var indexes = Enumerable.Range(0, 7).Select(_ => connector.PickStartIndex(3)).ToArray();
        Assert.Equal([0, 1, 2, 0, 1, 2, 0], indexes);
    }

    [Fact]
    public void PickStartIndex_Random_UsesAllAddresses()
    {
        var connector = CreateConnector(DnsSelection.Random);
        var indexes = Enumerable.Range(0, 1000).Select(_ => connector.PickStartIndex(3)).ToHashSet();
        Assert.Equal([0, 1, 2], indexes.Order());
    }

    [Fact]
    public async Task ConnectAsync_RoundRobin_SpreadsConnectionsOverAddresses()
    {
        using var listener1 = new DisposableListener(StartListener(Ip1, 0));
        var port = ((IPEndPoint)listener1.Listener.LocalEndpoint).Port;
        using var listener2 = new DisposableListener(StartListener(Ip2, port));

        var connector = CreateConnector(DnsSelection.RoundRobin, Ip1, Ip2);
        var remotes = new List<IPAddress>();

        for (var i = 0; i < 4; i++)
        {
            await using var stream = await connector.ConnectAsync("test-host", port, CancellationToken.None);
            remotes.Add(RemoteAddress(stream));
        }

        Assert.Equal([Ip1, Ip2, Ip1, Ip2], remotes);
    }

    [Fact]
    public async Task ConnectAsync_WhenSelectedAddressFails_TriesNextAddress()
    {
        // nothing listens on Ip2, so the connection to it is refused
        using var listener = new DisposableListener(StartListener(Ip1, 0));
        var port = ((IPEndPoint)listener.Listener.LocalEndpoint).Port;

        var connector = CreateConnector(DnsSelection.Default, Ip2, Ip1);
        await using var stream = await connector.ConnectAsync("test-host", port, CancellationToken.None);

        Assert.Equal(Ip1, RemoteAddress(stream));
    }

    [Fact]
    public async Task ConnectAsync_WhenAllAddressesFail_ThrowsSocketException()
    {
        int port;
        using (var temp = new DisposableListener(StartListener(Ip1, 0)))
            port = ((IPEndPoint)temp.Listener.LocalEndpoint).Port;

        var connector = CreateConnector(DnsSelection.RoundRobin, Ip1);
        await Assert.ThrowsAsync<SocketException>(() => connector.ConnectAsync("test-host", port, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAsync_WhenNoAddressesResolved_ThrowsSocketException()
    {
        var connector = CreateConnector(DnsSelection.Random);
        var ex = await Assert.ThrowsAsync<SocketException>(() => connector.ConnectAsync("test-host", 80, CancellationToken.None));
        Assert.Equal(SocketError.HostNotFound, ex.SocketErrorCode);
    }

    sealed class DisposableListener(TcpListener listener) : IDisposable
    {
        public TcpListener Listener { get; } = listener;
        public void Dispose() => Listener.Stop();
    }
}
