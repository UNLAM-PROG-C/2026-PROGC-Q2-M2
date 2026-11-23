using System.Collections.Concurrent;
using System.Net.Sockets;
using SpaceShooter.Protocol;
using SpaceShooter.Transport;

namespace SpaceShooter.Client.Networking;

/// <summary>
/// Owns client transport and transfers received payloads to the Godot thread.
/// </summary>
public sealed class LanClient : IAsyncDisposable
{
  private readonly ConcurrentQueue<IProtocolPayload> _incoming = new();
  private readonly SemaphoreSlim _sendLock = new(1, 1);
  private readonly CancellationTokenSource _shutdown = new();
  private long _outgoingSequence;
  private Task? _receiveTask;
  private NetworkStream? _stream;
  private TcpClient? _tcpClient;

  public bool IsConnected => _tcpClient?.Connected == true && !_shutdown.IsCancellationRequested;

  public bool TryDequeue(out IProtocolPayload? payload)
  {
    return _incoming.TryDequeue(out payload);
  }

  /// <summary>Connects once and starts the background receive loop.</summary>
  /// <param name="host">The IPv4 server host.</param>
  /// <param name="port">The TCP server port.</param>
  /// <param name="playerName">The display name sent during handshake.</param>
  /// <param name="requestedPlayers">The desired lobby size.</param>
  /// <returns>A task that completes after the hello message is sent.</returns>
  public async Task ConnectAsync(string host, int port, string playerName, int requestedPlayers)
  {
    EnsureUnused();
    _tcpClient = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
    await _tcpClient.ConnectAsync(host, port, _shutdown.Token);
    _stream = _tcpClient.GetStream();
    _receiveTask = ReceiveLoopAsync(_shutdown.Token);
    HelloPayload hello = new("vertical-slice-1", playerName, RequestedPlayers: requestedPlayers);
    await SendAsync(hello);
  }

  /// <summary>Serializes one payload while preserving the outgoing sequence order.</summary>
  /// <param name="payload">The protocol payload to send.</param>
  /// <returns>A task that completes after the frame is written.</returns>
  public async Task SendAsync(IProtocolPayload payload)
  {
    NetworkStream stream = _stream ?? throw new InvalidOperationException("The client is not connected.");
    await _sendLock.WaitAsync(_shutdown.Token);
    try
    {
      await WritePayloadAsync(stream, payload);
    }
    finally
    {
      _sendLock.Release();
    }
  }

  public async ValueTask DisposeAsync()
  {
    _shutdown.Cancel();
    _tcpClient?.Dispose();
    await AwaitReceiveLoopAsync();
    _sendLock.Dispose();
    _shutdown.Dispose();
  }

  private void EnsureUnused()
  {
    if (_tcpClient is not null)
    {
      throw new InvalidOperationException("The client has already been used.");
    }
  }

  private async Task WritePayloadAsync(NetworkStream stream, IProtocolPayload payload)
  {
    long sequence = Interlocked.Increment(ref _outgoingSequence);
    ProtocolMessage message = new(sequence, payload);
    byte[] bytes = ProtocolSerializer.Serialize(message);
    await LengthPrefixedFrame.WriteAsync(stream, bytes, _shutdown.Token);
  }

  private async Task AwaitReceiveLoopAsync()
  {
    if (_receiveTask is null)
    {
      return;
    }

    try
    {
      await _receiveTask;
    }
    catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
    {
      // Client disposal owns receive-loop cancellation.
    }
  }

  private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
  {
    try
    {
      while (!cancellationToken.IsCancellationRequested && await ReceiveOneAsync(cancellationToken))
      {
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // Client disposal is the expected shutdown path.
    }
    catch (Exception exception) when (IsTransportFailure(exception))
    {
      _incoming.Enqueue(new ErrorPayload("connection", "The connection failed."));
    }
  }

  private async Task<bool> ReceiveOneAsync(CancellationToken cancellationToken)
  {
    byte[]? frame = await LengthPrefixedFrame.ReadAsync(_stream!, cancellationToken);
    if (frame is null)
    {
      _incoming.Enqueue(new ErrorPayload("disconnected", "El servidor cerró la conexión."));
      return false;
    }

    ProtocolMessage message = ProtocolSerializer.Deserialize(frame);
    return await HandlePayloadAsync(message.Payload);
  }

  private async Task<bool> HandlePayloadAsync(IProtocolPayload payload)
  {
    if (payload is PingPayload ping)
    {
      await SendAsync(new PingResponsePayload(ping.PingId));
      return true;
    }

    _incoming.Enqueue(payload);
    return payload is not ErrorPayload;
  }

  private static bool IsTransportFailure(Exception exception)
  {
    return exception is IOException or SocketException or ProtocolException;
  }
}
