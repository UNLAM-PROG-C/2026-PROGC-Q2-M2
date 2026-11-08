using SpaceShooter.Client.Networking;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

/// <summary>
/// Coordinates client use cases while preserving LanClient as the transport boundary.
/// </summary>
internal sealed class ClientCoordinator : IAsyncDisposable
{
    private LanClient? _client;

    public bool IsConnected => _client?.IsConnected == true;

    public async Task ConnectAsync(string host, string playerName, int requestedPlayers)
    {
        await DisposeClientAsync();
        _client = new LanClient();
        await _client.ConnectAsync(host, 7777, playerName, requestedPlayers);
    }

    public ValueTask DisposeAsync()
    {
        return _client is null ? ValueTask.CompletedTask : _client.DisposeAsync();
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

    private async Task DisposeClientAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }
}
