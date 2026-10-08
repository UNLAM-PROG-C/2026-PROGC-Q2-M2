using System.Net;
using System.Net.Sockets;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Server.Networking;

public sealed class BasicProtocolServer(IPAddress address, int port, TextWriter output) : IDisposable
{
    private readonly TcpListener _listener = new(address, port);
    private readonly TextWriter _output = output;
    private readonly TaskCompletionSource<int> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextPlayerId;

    public Task<int> Started => _started.Task;

    public void Dispose() => _listener.Stop();

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var connections = new List<Task>();

        try
        {
            _listener.Start();
            var boundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _started.TrySetResult(boundPort);
            await _output.WriteLineAsync($"Protocol server listening on 0.0.0.0:{boundPort}.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                client.NoDelay = true;
                var playerId = new PlayerId(Interlocked.Increment(ref _nextPlayerId));
                connections.Add(HandleClientSafelyAsync(client, playerId, cancellationToken));
                connections.RemoveAll(task => task.IsCompleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is the expected shutdown path.
        }
        catch (Exception exception)
        {
            _started.TrySetException(exception);
            throw;
        }
        finally
        {
            _listener.Stop();
            await Task.WhenAll(connections);
        }
    }

    private async Task HandleClientSafelyAsync(
        TcpClient client,
        PlayerId playerId,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                await BasicProtocolConnection.HandleAsync(client.GetStream(), playerId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The server owns cancellation for active connections.
            }
            catch (ProtocolException exception)
            {
                await _output.WriteLineAsync($"Protocol error for player {playerId.Value}: {exception.Error}.");
            }
            catch (IOException exception)
            {
                await _output.WriteLineAsync($"Connection closed for player {playerId.Value}: {exception.Message}");
            }
            catch (SocketException exception)
            {
                await _output.WriteLineAsync($"Socket closed for player {playerId.Value}: {exception.SocketErrorCode}.");
            }
        }
    }
}
