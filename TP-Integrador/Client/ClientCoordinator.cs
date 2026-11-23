using System.Net.Sockets;
using SpaceShooter.Client.Networking;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

/// <summary>
/// Coordinates client use cases while preserving LanClient as the transport boundary.
/// </summary>
internal sealed class ClientCoordinator : IAsyncDisposable
{
  private const int ServerPort = 7777;
  private LanClient? _client;

  public bool IsConnected => _client?.IsConnected == true;

  public async Task ConnectAsync(string host, string playerName, int requestedPlayers)
  {
    await DisposeClientAsync();
    _client = new LanClient();
    await _client.ConnectAsync(host, ServerPort, playerName, requestedPlayers);
  }

  public async ValueTask DisposeAsync()
  {
    await DisposeClientAsync();
  }

  public async Task DisconnectAsync(string reason)
  {
    await TrySendLeaveAsync(reason);
    await DisposeClientAsync();
  }

  public Task SendAsync(IProtocolPayload payload)
  {
    return _client?.SendAsync(payload) ?? Task.CompletedTask;
  }

  public bool TryDequeue(out IProtocolPayload? payload)
  {
    if (_client?.TryDequeue(out IProtocolPayload? received) == true)
    {
      payload = received;
      return true;
    }

    payload = null;
    return false;
  }

  private async Task TrySendLeaveAsync(string reason)
  {
    if (_client is null)
    {
      return;
    }

    try
    {
      await _client.SendAsync(new LeavePayload(reason));
    }
    catch (Exception exception) when (IsExpectedDisconnect(exception))
    {
      // The requested outcome is already a disconnected client.
    }
  }

  private static bool IsExpectedDisconnect(Exception exception)
  {
    return exception is IOException or OperationCanceledException or SocketException or
        ObjectDisposedException or InvalidOperationException;
  }

  private async Task DisposeClientAsync()
  {
    if (_client is not null)
    {
      await _client.DisposeAsync();
      _client = null;
    }
  }
}
