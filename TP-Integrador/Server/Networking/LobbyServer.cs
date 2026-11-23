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

/// <summary>
/// Accepts bounded client connections and owns the authoritative server lifetime.
/// </summary>
public sealed class LobbyServer(
    IPAddress address,
    int port,
    TextWriter output,
    TimeSpan? countdownDuration = null,
    SimulationConfig? simulationConfig = null,
    IHighScoreStore? highScores = null) : IDisposable
{
  private const int MaximumConnections = 4;
  private readonly ConcurrentDictionary<PlayerId, SessionConnection> _connections = new();
  private readonly SemaphoreSlim _connectionSlots = new(MaximumConnections, MaximumConnections);
  private readonly LobbyAdmission _admission = new();
  private readonly TcpListener _listener = new(address, port);
  private readonly SessionCommandChannel _commands = new();
  private readonly TextWriter _output = output;
  private readonly TaskCompletionSource<int> _started = new(
      TaskCreationOptions.RunContinuationsAsynchronously);
  private int _nextPlayerId;

  public Task<int> Started => _started.Task;

  public void Dispose()
  {
    StopListenerAndConnections();
    _connectionSlots.Dispose();
  }

  /// <summary>Runs connection acceptance and the authoritative game loop.</summary>
  /// <param name="cancellationToken">Stops the listener and all owned connections.</param>
  /// <returns>A task representing the server lifetime.</returns>
  public async Task RunAsync(CancellationToken cancellationToken)
  {
    List<Task> connectionTasks = [];
    Task gameLoopTask = CreateGameLoop().RunAsync(cancellationToken);
    try
    {
      await ListenAsync(connectionTasks, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // Server cancellation is the expected shutdown path.
    }
    catch (Exception exception)
    {
      _started.TrySetException(exception);
      throw;
    }
    finally
    {
      await ShutdownAsync(connectionTasks, gameLoopTask);
    }
  }

  private AuthoritativeGameLoop CreateGameLoop()
  {
    SessionRegistry registry = new();
    LobbyCoordinator coordinator = CreateCoordinator(registry, _admission);
    SimulationConfig config = simulationConfig ?? new SimulationConfig { CampaignEnabled = true };
    IHighScoreStore scoreStore = highScores ?? new InMemoryHighScoreStore();
    AuthoritativeMatch match = new(registry, config, new SeededRandomSource(1));
    MatchPauseController pause = new(registry, Broadcast);
    return new AuthoritativeGameLoop(
        _commands,
        coordinator,
        match,
        pause,
        config,
        new StopwatchGameClock(),
        Broadcast,
        scoreStore);
  }

  private LobbyCoordinator CreateCoordinator(
      SessionRegistry registry,
      LobbyAdmission admission)
  {
    return new LobbyCoordinator(
        registry,
        Broadcast,
        countdownDuration,
        () => admission.TargetPlayers,
        admission.MarkMatchStarted,
        admission.ReopenLobby);
  }

  private async Task ListenAsync(
      List<Task> connectionTasks,
      CancellationToken cancellationToken)
  {
    _listener.Start();
    int boundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
    _started.TrySetResult(boundPort);
    await _output.WriteLineAsync($"Lobby server listening on 0.0.0.0:{boundPort}.");
    while (!cancellationToken.IsCancellationRequested)
    {
      RemoveCompletedTasks(connectionTasks);
      await AcceptClientAsync(connectionTasks, cancellationToken);
    }
  }

  private async Task AcceptClientAsync(
      List<Task> connectionTasks,
      CancellationToken cancellationToken)
  {
    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
    client.NoDelay = true;
    if (!_connectionSlots.Wait(0, cancellationToken))
    {
      await RejectFullServerAsync(client, cancellationToken);
      return;
    }

    RegisterConnection(client, connectionTasks, cancellationToken);
  }

  private void RegisterConnection(
      TcpClient client,
      List<Task> connectionTasks,
      CancellationToken cancellationToken)
  {
    PlayerId playerId = new(Interlocked.Increment(ref _nextPlayerId));
    SessionConnection connection = new(client, playerId, _commands, _admission);
    if (!_connections.TryAdd(playerId, connection))
    {
      connection.Dispose();
      _connectionSlots.Release();
      return;
    }

    connectionTasks.Add(RunConnectionSafelyAsync(connection, cancellationToken));
  }

  private void Broadcast(ProtocolMessage message)
  {
    foreach (SessionConnection connection in _connections.Values)
    {
      SendOrDisconnect(connection, message);
    }
  }

  private static void SendOrDisconnect(SessionConnection connection, ProtocolMessage message)
  {
    if (connection.CanReceiveBroadcasts && !connection.TrySend(message))
    {
      connection.Disconnect();
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
      // Server cancellation owns active connection shutdown.
    }
    catch (Exception exception)
    {
      await LogConnectionErrorAsync(connection, exception);
    }
    finally
    {
      ReleaseConnection(connection);
    }
  }

  private async Task LogConnectionErrorAsync(
      SessionConnection connection,
      Exception exception)
  {
    string detail = exception switch
    {
      ProtocolException protocol => $"protocol error {protocol.Error}",
      SocketException socket => $"socket error {socket.SocketErrorCode}",
      IOException io => $"connection closed: {io.Message}",
      _ => $"unexpected connection error: {exception}",
    };
    await _output.WriteLineAsync($"Player {connection.PlayerId.Value}: {detail}");
  }

  private void ReleaseConnection(SessionConnection connection)
  {
    _connections.TryRemove(connection.PlayerId, out _);
    connection.Dispose();
    _connectionSlots.Release();
  }

  private async Task ShutdownAsync(List<Task> connectionTasks, Task gameLoopTask)
  {
    StopListenerAndConnections();
    await Task.WhenAll(connectionTasks);
    _commands.Complete();
    await gameLoopTask;
  }

  private void StopListenerAndConnections()
  {
    _listener.Stop();
    foreach (SessionConnection connection in _connections.Values)
    {
      connection.Disconnect();
    }
  }

  private static void RemoveCompletedTasks(List<Task> connectionTasks)
  {
    connectionTasks.RemoveAll(task => task.IsCompleted);
  }

  private static async Task RejectFullServerAsync(
      TcpClient client,
      CancellationToken cancellationToken)
  {
    using (client)
    {
      ErrorPayload payload = new("serverFull", "El server ya tiene conectados 4 jugadores.");
      ProtocolMessage error = new(1, payload);
      byte[] bytes = ProtocolSerializer.Serialize(error);
      await LengthPrefixedFrame.WriteAsync(client.GetStream(), bytes, cancellationToken);
    }
  }
}
