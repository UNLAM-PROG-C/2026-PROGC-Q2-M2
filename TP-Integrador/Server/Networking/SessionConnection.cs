using System.Net.Sockets;
using System.Threading.Channels;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Sessions;
using SpaceShooter.Transport;

namespace SpaceShooter.Server.Networking;

public sealed class SessionConnection(
    TcpClient client,
    PlayerId playerId,
    SessionCommandChannel commands,
    LobbyAdmission admission) : IDisposable
{
    private const long PingId = 1;
    private readonly object _lifecycleLock = new();
    private readonly CancellationTokenSource _disconnect = new();
    private readonly Channel<ProtocolMessage> _outgoing = Channel.CreateBounded<ProtocolMessage>(
        new BoundedChannelOptions(16)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
        });

    public PlayerId PlayerId { get; } = playerId;

    private long _outgoingSequence;
    private int _handshakeComplete;

    public bool CanReceiveBroadcasts => Volatile.Read(ref _handshakeComplete) == 1;

    public bool TrySend(ProtocolMessage message) => _outgoing.Writer.TryWrite(
        new ProtocolMessage(Interlocked.Increment(ref _outgoingSequence), message.Payload));

    private bool _disposed;

    public void Disconnect()
    {
        lock (_lifecycleLock)
        {
            if (!_disposed)
            {
                _disconnect.Cancel();
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disconnect.Cancel();
            client.Dispose();
            _disconnect.Dispose();
        }
    }

    public async Task RunAsync(CancellationToken serverCancellation)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            serverCancellation,
            _disconnect.Token);
        var cancellationToken = cancellation.Token;
        var stream = client.GetStream();
        var sendTask = SendLoopAsync(stream, cancellationToken);
        var admitted = false;

        try
        {
            var helloMessage = await ReadMessageAsync(stream, cancellationToken);
            if (helloMessage?.Payload is not HelloPayload hello)
            {
                throw new ProtocolException(ProtocolError.UnexpectedMessage, "Hello must be the first message of a connection.");
            }

            if (!admission.TryAdmit(
                    PlayerId,
                    hello.RequestedPlayers,
                    out var errorCode,
                    out var errorDescription))
            {
                TrySend(new ProtocolMessage(1, new ErrorPayload(errorCode, errorDescription)));
                return;
            }

            admitted = true;

            if (!TrySend(new ProtocolMessage(
                    1,
                    new WelcomePayload(PlayerId, MinimumPlayers: 1, MaximumPlayers: 4, ProtocolVersion.Current))) ||
                !TrySend(new ProtocolMessage(
                    2,
                    new PingPayload(PingId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))))
            {
                throw new ProtocolException(ProtocolError.UnexpectedMessage, "A bounded connection queue is full.");
            }

            Volatile.Write(ref _handshakeComplete, 1);
            if (!commands.TryEnqueue(new JoinSessionCommand(PlayerId, helloMessage.Sequence, hello)))
            {
                throw new ProtocolException(ProtocolError.UnexpectedMessage, "The bounded input channel is full.");
            }

            await ReceiveLoopAsync(stream, helloMessage.Sequence, cancellationToken);
        }
        finally
        {
            if (admitted)
            {
                commands.TryEnqueue(new DisconnectSessionCommand(PlayerId));
                admission.Release(PlayerId);
            }

            _outgoing.Writer.TryComplete();

            try
            {
                await sendTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is owned by the connection or server.
            }
        }
    }

    private async Task ReceiveLoopAsync(Stream stream, long lastSequence, CancellationToken cancellationToken)
    {
        var incomingSequence = new IncomingSequence(lastSequence);
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReadMessageAsync(stream, cancellationToken);
            if (message is null)
            {
                return;
            }

            if (!incomingSequence.TryAccept(message.Sequence))
            {
                continue;
            }
            switch (message.Payload)
            {
                case PingResponsePayload response when response.PingId == PingId:
                    continue;
                case LeavePayload:
                    return;
                case SelectShipPayload or SetReadyPayload or InputStatePayload:
                    if (!commands.TryEnqueue(new ClientPayloadCommand(PlayerId, message.Sequence, message.Payload)))
                    {
                        return;
                    }

                    break;
                default:
                    throw new ProtocolException(
                        ProtocolError.UnexpectedMessage,
                        $"Message '{message.Type}' is not valid from a connected client.");
            }
        }
    }

    private async Task SendLoopAsync(Stream stream, CancellationToken cancellationToken)
    {
        await foreach (var message in _outgoing.Reader.ReadAllAsync(cancellationToken))
        {
            await LengthPrefixedFrame.WriteAsync(
                stream,
                ProtocolSerializer.Serialize(message),
                cancellationToken);
        }
    }

    private static async ValueTask<ProtocolMessage?> ReadMessageAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var payload = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken);
        return payload is null ? null : ProtocolSerializer.Deserialize(payload);
    }
}
