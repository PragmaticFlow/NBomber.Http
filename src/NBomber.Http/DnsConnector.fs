namespace NBomber.Http

#if NET8_0_OR_GREATER

open System
open System.IO
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Defines which IP address a new connection uses when the host name resolves to several addresses (for example, DNS round-robin).
/// </summary>
type DnsSelection =
    /// Always starts with the first resolved address. This is the default .NET behavior.
    /// With several addresses, all connections usually go to the same IP.
    | First = 0
    /// Starts with a random resolved address for each new connection.
    | Random = 1
    /// Starts with the next resolved address (in sequence) for each new connection.
    | RoundRobin = 2

/// Opens TCP connections and selects the resolved IP address with a <see cref="DnsSelection"/> strategy.
/// If the connection to the selected address fails, the next addresses are tried in order.
type internal DnsConnector(selection: DnsSelection, resolve: Func<string, CancellationToken, Task<IPAddress[]>>) =

    let mutable counter = -1

    new(selection) =
        DnsConnector(selection, Func<_,_,_>(fun host ct -> Dns.GetHostAddressesAsync(host, ct)))

    member this.PickStartIndex(count: int) =
        match selection with
        | DnsSelection.Random     -> Random.Shared.Next count
        | DnsSelection.RoundRobin -> (Interlocked.Increment(&counter) &&& Int32.MaxValue) % count
        | _                       -> 0

    member this.ConnectAsync(host: string, port: int, ct: CancellationToken) : Task<Stream> = backgroundTask {
        let! addresses = resolve.Invoke(host, ct)

        if addresses.Length = 0 then
            raise (SocketException(int SocketError.HostNotFound))

        let start = this.PickStartIndex addresses.Length
        let mutable stream: Stream = null
        let mutable lastError: exn = null
        let mutable i = 0

        while isNull stream && i < addresses.Length do
            let address = addresses[(start + i) % addresses.Length]
            let socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            socket.NoDelay <- true
            try
                do! socket.ConnectAsync(IPEndPoint(address, port), ct)
                stream <- new NetworkStream(socket, ownsSocket = true)
            with ex ->
                socket.Dispose()
                ct.ThrowIfCancellationRequested()
                lastError <- ex
            i <- i + 1

        if isNull stream then
            ExceptionDispatchInfo.Throw(lastError)

        return stream
    }

    /// The function for <see cref="SocketsHttpHandler.ConnectCallback"/>.
    member this.ConnectCallback(ctx: SocketsHttpConnectionContext, ct: CancellationToken) =
        ValueTask<Stream>(this.ConnectAsync(ctx.DnsEndPoint.Host, ctx.DnsEndPoint.Port, ct))

#endif
