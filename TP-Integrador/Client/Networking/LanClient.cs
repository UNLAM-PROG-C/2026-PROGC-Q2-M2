using System.Collections.Concurrent;
using System.Net.Sockets;
using SpaceShooter.Protocol;
using SpaceShooter.Transport;

namespace SpaceShooter.Client.Networking;

public sealed class LanClient : IAsyncDisposable
{
    private readonly ConcurrentQueue<IProtocolPayload> _incoming = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private Task? _receiveTask;
    private long _outgoingSequence;

    public bool IsConnected => _tcpClient?.Connected == true && !_shutdown.IsCancellationRequested;

    public bool TryDequeue(out IProtocolPayload? payload) => _incoming.TryDequeue(out payload);

    public async Task ConnectAsync(string host, int port, string playerName, int requestedPlayers)
    {
        if (_tcpClient is not null)
        {
            throw new InvalidOperationException("The client has already been used.");
        }

        _tcpClient = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
        await _tcpClient.ConnectAsync(host, port, _shutdown.Token);
        _stream = _tcpClient.GetStream();
        await SendAsync(new HelloPayload("vertical-slice-1", playerName, RequestedPlayers: requestedPlayers));
        _receiveTask = ReceiveLoopAsync(_shutdown.Token);
    }

    public async Task SendAsync(IProtocolPayload payload)
    {
        var stream = _stream ?? throw new InvalidOperationException("The client is not connected.");
        await _sendLock.WaitAsync(_shutdown.Token);
        try
        {
            var sequence = Interlocked.Increment(ref _outgoingSequence);
            await LengthPrefixedFrame.WriteAsync(
                stream,
                ProtocolSerializer.Serialize(new ProtocolMessage(sequence, payload)),
                _shutdown.Token);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_shutdown.IsCancellationRequested)
        {
            _shutdown.Cancel();
        }

        _tcpClient?.Dispose();
        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is the expected disposal path.
            }
        }

        _sendLock.Dispose();
        _shutdown.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await LengthPrefixedFrame.ReadAsync(_stream!, cancellationToken);
                if (frame is null)
                {
                    _incoming.Enqueue(new ErrorPayload("disconnected", "The server closed the connection."));
                    return;
                }

                var message = ProtocolSerializer.Deserialize(frame);
                if (message.Payload is ErrorPayload error)
                {
                    _incoming.Enqueue(error);
                    return;
                }

                if (message.Payload is PingPayload ping)
                {
                    await SendAsync(new PingResponsePayload(ping.PingId));
                    continue;
                }

                _incoming.Enqueue(message.Payload);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is the expected disposal path.
        }
        catch (Exception exception) when (exception is IOException or SocketException or ProtocolException)
        {
            _incoming.Enqueue(new ErrorPayload("connection", exception.Message));
        }
    }
}
