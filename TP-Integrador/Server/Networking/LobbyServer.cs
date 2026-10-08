using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Game;
using SpaceShooter.Server.Persistence;
using SpaceShooter.Server.Sessions;
using SpaceShooter.Server.Simulation;
using SpaceShooter.Transport;

namespace SpaceShooter.Server.Networking;

public sealed class LobbyServer(
    IPAddress address,
    int port,
    TextWriter output,
    TimeSpan? countdownDuration = null,
    SimulationConfig? simulationConfig = null,
    IHighScoreStore? highScores = null) : IDisposable
{
    private const int MaximumConnections = 4;
    private readonly TcpListener _listener = new(address, port);
    private readonly TextWriter _output = output;
    private readonly SemaphoreSlim _connectionSlots = new(MaximumConnections, MaximumConnections);
    private readonly ConcurrentDictionary<PlayerId, SessionConnection> _connections = new();
    private readonly SessionCommandChannel _commands = new();
    private readonly TaskCompletionSource<int> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextPlayerId;

    public Task<int> Started => _started.Task;

    public void Dispose()
    {
        _listener.Stop();
        foreach (var connection in _connections.Values)
        {
            connection.Disconnect();
        }

        _connectionSlots.Dispose();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var connectionTasks = new List<Task>();
        var registry = new SessionRegistry();
        var admission = new LobbyAdmission();
        var coordinator = new LobbyCoordinator(
            registry,
            Broadcast,
            countdownDuration,
            () => admission.TargetPlayers,
            admission.MarkMatchStarted);
        var config = simulationConfig ?? new SimulationConfig { CampaignEnabled = true };
        var scoreStore = highScores ?? new InMemoryHighScoreStore();
        var gameLoop = new AuthoritativeGameLoop(
            _commands,
            coordinator,
            new AuthoritativeMatch(registry, config, new SeededRandomSource(1)),
            config,
            new StopwatchGameClock(),
            Broadcast,
            scoreStore);
        var gameLoopTask = gameLoop.RunAsync(cancellationToken);

        try
        {
            _listener.Start();
            var boundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _started.TrySetResult(boundPort);
            await _output.WriteLineAsync($"Lobby server listening on 0.0.0.0:{boundPort}.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                client.NoDelay = true;

                if (!_connectionSlots.Wait(0, cancellationToken))
                {
                    await RejectFullServerAsync(client, cancellationToken);
                    continue;
                }

                var playerId = new PlayerId(Interlocked.Increment(ref _nextPlayerId));
                var connection = new SessionConnection(client, playerId, _commands, admission);
                if (!_connections.TryAdd(playerId, connection))
                {
                    connection.Dispose();
                    _connectionSlots.Release();
                    continue;
                }

                connectionTasks.Add(RunConnectionSafelyAsync(connection, cancellationToken));
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
            foreach (var connection in _connections.Values)
            {
                connection.Disconnect();
            }

            await Task.WhenAll(connectionTasks);
            _commands.Complete();
            await gameLoopTask;
        }
    }

    private void Broadcast(ProtocolMessage message)
    {
        foreach (var connection in _connections.Values)
        {
            if (!connection.CanReceiveBroadcasts)
            {
                continue;
            }

            if (!connection.TrySend(message))
            {
                connection.Disconnect();
            }
        }
    }

    private async Task RunConnectionSafelyAsync(
        SessionConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The server owns shutdown cancellation.
        }
        catch (ProtocolException exception)
        {
            await _output.WriteLineAsync($"Protocol error for player {connection.PlayerId.Value}: {exception.Error}.");
        }
        catch (IOException exception)
        {
            await _output.WriteLineAsync($"Connection closed for player {connection.PlayerId.Value}: {exception.Message}");
        }
        catch (SocketException exception)
        {
            await _output.WriteLineAsync($"Socket closed for player {connection.PlayerId.Value}: {exception.SocketErrorCode}.");
        }
        finally
        {
            _connections.TryRemove(connection.PlayerId, out _);
            connection.Dispose();
            _connectionSlots.Release();
        }
    }

    private static async Task RejectFullServerAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var error = new ProtocolMessage(
                1,
                new ErrorPayload("serverFull", "The server already has four connected players."));
            await LengthPrefixedFrame.WriteAsync(
                client.GetStream(),
                ProtocolSerializer.Serialize(error),
                cancellationToken);
        }
    }
}
