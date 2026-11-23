using System.Net.Sockets;
using System.Threading.Channels;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Sessions;
using SpaceShooter.Transport;

namespace SpaceShooter.Server.Networking;

/// <summary>
/// Owns one client socket and bridges validated messages to the command channel.
/// </summary>
public sealed class SessionConnection(
    TcpClient client,
    PlayerId playerId,
    SessionCommandChannel commands,
    LobbyAdmission admission) : IDisposable
{
  private const int OutgoingCapacity = 16;
  private const long PingId = 1;
  private readonly CancellationTokenSource _disconnect = new();
  private readonly object _lifecycleLock = new();
  private readonly Channel<ProtocolMessage> _outgoing = CreateOutgoingChannel();
  private bool _disposed;
  private int _handshakeComplete;
  private long _outgoingSequence;

  public PlayerId PlayerId { get; } = playerId;

  public bool CanReceiveBroadcasts => Volatile.Read(ref _handshakeComplete) == 1;

  public bool TrySend(ProtocolMessage message)
  {
    long sequence = Interlocked.Increment(ref _outgoingSequence);
    return _outgoing.Writer.TryWrite(new ProtocolMessage(sequence, message.Payload));
  }

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

      DisposeResources();
    }
  }

  /// <summary>Runs handshake, command production, and outgoing delivery for this socket.</summary>
  /// <param name="serverCancellation">Stops the connection during server shutdown.</param>
  /// <returns>A task representing the complete socket lifetime.</returns>
  public async Task RunAsync(CancellationToken serverCancellation)
  {
    using CancellationTokenSource linked = CreateLinkedCancellation(serverCancellation);
    CancellationToken cancellationToken = linked.Token;
    NetworkStream stream = client.GetStream();
    Task sendTask = SendLoopAsync(stream, cancellationToken);
    bool admitted = false;
    try
    {
      HandshakeResult? handshake = await PerformHandshakeAsync(stream, cancellationToken);
      admitted = handshake is not null;
      if (handshake is not null)
      {
        await RegisterAndReceiveAsync(stream, handshake, cancellationToken);
      }
    }
    finally
    {
      await CompleteAsync(admitted, sendTask, serverCancellation, cancellationToken);
    }
  }

  private static Channel<ProtocolMessage> CreateOutgoingChannel()
  {
    BoundedChannelOptions options = new(OutgoingCapacity)
    {
      SingleReader = true,
      SingleWriter = false,
      FullMode = BoundedChannelFullMode.Wait,
      AllowSynchronousContinuations = false,
    };
    return Channel.CreateBounded<ProtocolMessage>(options);
  }

  private CancellationTokenSource CreateLinkedCancellation(CancellationToken serverCancellation)
  {
    return CancellationTokenSource.CreateLinkedTokenSource(serverCancellation, _disconnect.Token);
  }

  private async Task<HandshakeResult?> PerformHandshakeAsync(
      Stream stream,
      CancellationToken cancellationToken)
  {
    ProtocolMessage? message = await ReadMessageAsync(stream, cancellationToken);
    HelloPayload hello = ValidateHello(message);
    if (!TryAdmit(hello, out ErrorPayload? error))
    {
      QueueError(error!);
      return null;
    }

    QueueGreeting();
    Volatile.Write(ref _handshakeComplete, 1);
    return new HandshakeResult(message!.Sequence, hello);
  }

  private static HelloPayload ValidateHello(ProtocolMessage? message)
  {
    if (message?.Payload is HelloPayload hello)
    {
      return hello;
    }

    throw new ProtocolException(
        ProtocolError.UnexpectedMessage,
        "Hello must be the first message of a connection.");
  }

  private bool TryAdmit(HelloPayload hello, out ErrorPayload? error)
  {
    bool accepted = admission.TryAdmit(
        PlayerId,
        hello.RequestedPlayers,
        out string errorCode,
        out string errorDescription);
    error = accepted ? null : new ErrorPayload(errorCode, errorDescription);
    return accepted;
  }

  private void QueueError(ErrorPayload error)
  {
    if (!TrySend(new ProtocolMessage(1, error)))
    {
      throw QueueFullException("connection output");
    }
  }

  private void QueueGreeting()
  {
    WelcomePayload welcome = new(PlayerId, 1, 4, ProtocolVersion.Current);
    PingPayload ping = new(PingId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    if (!TrySend(new ProtocolMessage(1, welcome)) || !TrySend(new ProtocolMessage(2, ping)))
    {
      throw QueueFullException("connection output");
    }
  }

  private async Task RegisterAndReceiveAsync(
      Stream stream,
      HandshakeResult handshake,
      CancellationToken cancellationToken)
  {
    JoinSessionCommand join = new(PlayerId, handshake.Sequence, handshake.Hello);
    await commands.EnqueueAsync(join, cancellationToken);
    await ReceiveLoopAsync(stream, handshake.Sequence, cancellationToken);
  }

  private async Task ReceiveLoopAsync(
      Stream stream,
      long lastSequence,
      CancellationToken cancellationToken)
  {
    IncomingSequence sequence = new(lastSequence);
    while (!cancellationToken.IsCancellationRequested)
    {
      ProtocolMessage? message = await ReadMessageAsync(stream, cancellationToken);
      if (message is null || !await HandleMessageAsync(message, sequence, cancellationToken))
      {
        return;
      }
    }
  }

  private async Task<bool> HandleMessageAsync(
      ProtocolMessage message,
      IncomingSequence sequence,
      CancellationToken cancellationToken)
  {
    if (!sequence.TryAccept(message.Sequence))
    {
      return true;
    }

    return await DispatchPayloadAsync(message, cancellationToken);
  }

  private async Task<bool> DispatchPayloadAsync(
      ProtocolMessage message,
      CancellationToken cancellationToken)
  {
    if (message.Payload is PingResponsePayload response && response.PingId == PingId)
    {
      return true;
    }

    if (message.Payload is LeavePayload)
    {
      return false;
    }

    await EnqueueClientPayloadAsync(message, cancellationToken);
    return true;
  }

  private async Task EnqueueClientPayloadAsync(
      ProtocolMessage message,
      CancellationToken cancellationToken)
  {
    if (message.Payload is not (SelectShipPayload or SetReadyPayload or SetPausePayload or InputStatePayload))
    {
      throw new ProtocolException(
          ProtocolError.UnexpectedMessage,
          $"Message '{message.Type}' is not valid from a connected client.");
    }

    ClientPayloadCommand command = new(PlayerId, message.Sequence, message.Payload);
    await commands.EnqueueAsync(command, cancellationToken);
  }

  private async Task CompleteAsync(
      bool admitted,
      Task sendTask,
      CancellationToken serverCancellation,
      CancellationToken connectionCancellation)
  {
    if (admitted)
    {
      await QueueDisconnectAsync(serverCancellation);
      admission.Release(PlayerId);
    }

    _outgoing.Writer.TryComplete();
    await AwaitSendLoopAsync(sendTask, connectionCancellation);
  }

  private async Task QueueDisconnectAsync(CancellationToken serverCancellation)
  {
    if (!serverCancellation.IsCancellationRequested)
    {
      await commands.EnqueueAsync(new DisconnectSessionCommand(PlayerId), serverCancellation);
    }
  }

  private static async Task AwaitSendLoopAsync(Task sendTask, CancellationToken cancellationToken)
  {
    try
    {
      await sendTask;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // Connection cancellation owns the send loop shutdown.
    }
  }

  private async Task SendLoopAsync(Stream stream, CancellationToken cancellationToken)
  {
    await foreach (ProtocolMessage message in _outgoing.Reader.ReadAllAsync(cancellationToken))
    {
      byte[] payload = ProtocolSerializer.Serialize(message);
      await LengthPrefixedFrame.WriteAsync(stream, payload, cancellationToken);
    }
  }

  private static async ValueTask<ProtocolMessage?> ReadMessageAsync(
      Stream stream,
      CancellationToken cancellationToken)
  {
    byte[]? payload = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken);
    return payload is null ? null : ProtocolSerializer.Deserialize(payload);
  }

  private static ProtocolException QueueFullException(string queueName)
  {
    return new ProtocolException(
        ProtocolError.UnexpectedMessage,
        $"The bounded {queueName} queue is full.");
  }

  private void DisposeResources()
  {
    _disposed = true;
    _disconnect.Cancel();
    client.Dispose();
    _disconnect.Dispose();
  }

  private sealed record HandshakeResult(long Sequence, HelloPayload Hello);
}
