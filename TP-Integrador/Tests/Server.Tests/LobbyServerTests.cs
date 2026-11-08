using System.Net;
using System.Net.Sockets;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Networking;
using SpaceShooter.Server.Sessions;
using SpaceShooter.Server.Simulation;
using SpaceShooter.Transport;

namespace SpaceShooter.Server.Tests;

public sealed class LobbyServerTests
{
  [Fact]
  public async Task FourClientsConnectAndFifthIsRejected()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using LobbyServer server = new LobbyServer(IPAddress.Loopback, 0, TextWriter.Null);
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    IEnumerable<Task<TestLobbyClient>> connectionTasks = Enumerable.Range(1, 4)
        .Select(number => TestLobbyClient.ConnectAsync(port, $"Pilot {number}", shutdown.Token, requestedPlayers: 4));
    TestLobbyClient[] clients = await Task.WhenAll(connectionTasks);

    try
    {
      Assert.Equal(4, clients.Select(client => client.PlayerId).Distinct().Count());

      using TcpClient fifth = new TcpClient(AddressFamily.InterNetwork);
      await fifth.ConnectAsync(IPAddress.Loopback, port, shutdown.Token);
      ProtocolMessage rejection = await ReadMessageAsync(fifth.GetStream(), shutdown.Token);
      ErrorPayload error = Assert.IsType<ErrorPayload>(rejection.Payload);
      Assert.Equal("serverFull", error.Code);
    }
    finally
    {
      foreach (TestLobbyClient? client in clients)
      {
        client.Dispose();
      }

      shutdown.Cancel();
      await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
    }
  }

  [Fact]
  public async Task DisconnectedSlotCanBeFilledBeforeFourPlayerMatch()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using LobbyServer server = new LobbyServer(
        IPAddress.Loopback,
        0,
        TextWriter.Null,
        countdownDuration: TimeSpan.FromMilliseconds(100));
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    using TestLobbyClient first = await TestLobbyClient.ConnectAsync(port, "First", shutdown.Token, 4);
    using TestLobbyClient second = await TestLobbyClient.ConnectAsync(port, "Second", shutdown.Token, 4);
    TestLobbyClient disconnected = await TestLobbyClient.ConnectAsync(port, "Disconnected", shutdown.Token, 4);

    await first.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await second.SendAsync(new SetReadyPayload(true), shutdown.Token);
    disconnected.Dispose();

    await Task.Delay(50, shutdown.Token);
    using TestLobbyClient third = await TestLobbyClient.ConnectAsync(port, "Third", shutdown.Token, 4);
    using TestLobbyClient fourth = await TestLobbyClient.ConnectAsync(port, "Fourth", shutdown.Token, 4);
    await third.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await fourth.SendAsync(new SetReadyPayload(true), shutdown.Token);

    MatchStartedPayload firstStarted = await first.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    MatchStartedPayload secondStarted = await second.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    MatchStartedPayload thirdStarted = await third.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    MatchStartedPayload fourthStarted = await fourth.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);

    Assert.Equal(firstStarted.MatchId, secondStarted.MatchId);
    Assert.Equal(firstStarted.MatchId, thirdStarted.MatchId);
    Assert.Equal(firstStarted.MatchId, fourthStarted.MatchId);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task RepeatedServerLifecycleDoesNotDeadlock()
  {
    for (int iteration = 0; iteration < 10; iteration++)
    {
      using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(2));
      using LobbyServer server = new LobbyServer(IPAddress.Loopback, 0, TextWriter.Null);
      Task serverTask = server.RunAsync(shutdown.Token);
      int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
      using TestLobbyClient client = await TestLobbyClient.ConnectAsync(port, $"Pilot {iteration}", shutdown.Token);

      shutdown.Cancel();
      await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
    }
  }

  [Fact]
  public void SlowClientCannotGrowOutgoingQueueWithoutLimit()
  {
    using TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
    SessionCommandChannel commands = new SessionCommandChannel();
    using SessionConnection connection = new SessionConnection(tcpClient, new(1), commands, new LobbyAdmission());
    ProtocolMessage message = new ProtocolMessage(1, new PingPayload(1, 1));

    for (int item = 0; item < 16; item++)
    {
      Assert.True(connection.TrySend(message));
    }

    Assert.False(connection.TrySend(message));
  }

  [Fact]
  public async Task TwoClientsReceiveAuthoritativeMovementAtTwentySnapshotsPerSecond()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using LobbyServer server = new LobbyServer(
        IPAddress.Loopback,
        0,
        TextWriter.Null,
        countdownDuration: TimeSpan.FromMilliseconds(20),
        simulationConfig: new SimulationConfig());
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    using TestLobbyClient first = await TestLobbyClient.ConnectAsync(port, "First", shutdown.Token);
    using TestLobbyClient second = await TestLobbyClient.ConnectAsync(port, "Second", shutdown.Token);
    await first.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await second.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await first.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    await second.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    WorldSnapshotPayload initial = await first.ReadUntilAsync<WorldSnapshotPayload>(shutdown.Token);
    PlayerSnapshot initialPlayer = initial.Players.Single(player => player.Id.Value == first.PlayerId);

    await first.SendAsync(new InputStatePayload(1, 0, false, 1), shutdown.Token);
    WorldSnapshotPayload moved = await first.ReadUntilAsync<WorldSnapshotPayload>(
        snapshot => snapshot.Players.Any(player =>
            player.Id.Value == first.PlayerId && player.Position.X > initialPlayer.Position.X),
        shutdown.Token);
    WorldSnapshotPayload next = await first.ReadUntilAsync<WorldSnapshotPayload>(shutdown.Token);

    Assert.Equal(2, moved.Players.Length);
    Assert.InRange(next.ServerTick - moved.ServerTick, 3, 4);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task FourPlayingClientsReceiveWorldContainingAllPlayers()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using LobbyServer server = new LobbyServer(
        IPAddress.Loopback,
        0,
        TextWriter.Null,
        countdownDuration: TimeSpan.FromMilliseconds(20),
        simulationConfig: new SimulationConfig());
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    TestLobbyClient[] clients = await Task.WhenAll(Enumerable.Range(1, 4)
        .Select(number => TestLobbyClient.ConnectAsync(port, $"Pilot {number}", shutdown.Token, requestedPlayers: 4)));

    try
    {
      await Task.WhenAll(clients.Select(client =>
          client.SendAsync(new SetReadyPayload(true), shutdown.Token)));
      foreach (TestLobbyClient? client in clients)
      {
        await client.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
      }

      foreach (TestLobbyClient? client in clients)
      {
        WorldSnapshotPayload snapshot = await client.ReadUntilAsync<WorldSnapshotPayload>(shutdown.Token);
        Assert.Equal(4, snapshot.Players.Length);
      }
    }
    finally
    {
      foreach (TestLobbyClient? client in clients)
      {
        client.Dispose();
      }

      shutdown.Cancel();
      await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
    }
  }

  [Fact]
  public async Task ServerResolvesShotScoreAndVictoryForBothClients()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using LobbyServer server = new LobbyServer(
        IPAddress.Loopback,
        0,
        TextWriter.Null,
        countdownDuration: TimeSpan.FromMilliseconds(20),
        simulationConfig: new SimulationConfig());
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    using TestLobbyClient first = await TestLobbyClient.ConnectAsync(port, "First", shutdown.Token);
    using TestLobbyClient second = await TestLobbyClient.ConnectAsync(port, "Second", shutdown.Token);
    await first.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await second.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await first.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    await second.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);

    await first.SendAsync(new InputStatePayload(0, 0, true, 1), shutdown.Token);
    MatchEndedPayload firstResult = await first.ReadUntilAsync<MatchEndedPayload>(shutdown.Token);
    MatchEndedPayload secondResult = await second.ReadUntilAsync<MatchEndedPayload>(shutdown.Token);

    Assert.True(firstResult.Victory);
    Assert.True(secondResult.Victory);
    Assert.Equal(100, firstResult.Ranking.Single(entry => entry.PlayerId.Value == first.PlayerId).Score);
    Assert.Equal(firstResult.Ranking, secondResult.Ranking);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task TwoPlayersCanCompleteTwoConsecutiveMatchesWithoutRestartingServer()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    using LobbyServer server = new LobbyServer(
        IPAddress.Loopback,
        0,
        TextWriter.Null,
        countdownDuration: TimeSpan.FromMilliseconds(20),
        simulationConfig: new SimulationConfig());
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));

    TestLobbyClient first = await TestLobbyClient.ConnectAsync(port, "First", shutdown.Token);
    TestLobbyClient second = await TestLobbyClient.ConnectAsync(port, "Second", shutdown.Token);
    await first.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await second.SendAsync(new SetReadyPayload(true), shutdown.Token);
    MatchStartedPayload firstMatch = await first.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    await second.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    await first.SendAsync(new InputStatePayload(0, 0, true, 1), shutdown.Token);
    await first.ReadUntilAsync<MatchEndedPayload>(shutdown.Token);
    await second.ReadUntilAsync<MatchEndedPayload>(shutdown.Token);
    first.Dispose();
    second.Dispose();

    await Task.Delay(200, shutdown.Token);

    using TestLobbyClient third = await TestLobbyClient.ConnectAsync(port, "Third", shutdown.Token);
    using TestLobbyClient fourth = await TestLobbyClient.ConnectAsync(port, "Fourth", shutdown.Token);
    await third.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await fourth.SendAsync(new SetReadyPayload(true), shutdown.Token);
    MatchStartedPayload secondMatch = await third.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    await fourth.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    await third.SendAsync(new InputStatePayload(0, 0, true, 1), shutdown.Token);
    MatchEndedPayload thirdResult = await third.ReadUntilAsync<MatchEndedPayload>(shutdown.Token);
    MatchEndedPayload fourthResult = await fourth.ReadUntilAsync<MatchEndedPayload>(shutdown.Token);

    Assert.True(secondMatch.MatchId > firstMatch.MatchId);
    Assert.True(thirdResult.Victory);
    Assert.True(fourthResult.Victory);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task ClientWithDifferentMatchSizeIsRejected()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using LobbyServer server = new LobbyServer(IPAddress.Loopback, 0, TextWriter.Null);
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    using TestLobbyClient first = await TestLobbyClient.ConnectAsync(port, "First", shutdown.Token, 4);

    ErrorPayload error = await ConnectExpectErrorAsync(port, "Second", requestedPlayers: 2, shutdown.Token);

    Assert.Equal("matchSizeMismatch", error.Code);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task LateJoinIsRejectedAfterMatchStarts()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using LobbyServer server = new LobbyServer(
        IPAddress.Loopback,
        0,
        TextWriter.Null,
        countdownDuration: TimeSpan.FromMilliseconds(20));
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    using TestLobbyClient first = await TestLobbyClient.ConnectAsync(port, "First", shutdown.Token);
    using TestLobbyClient second = await TestLobbyClient.ConnectAsync(port, "Second", shutdown.Token);
    await first.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await second.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await first.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);

    ErrorPayload error = await ConnectExpectErrorAsync(port, "Late", requestedPlayers: 2, shutdown.Token);

    Assert.Equal("matchInProgress", error.Code);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task TwoClientsObserveSameCompleteCampaignAndRecords()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    SimulationConfig config = new SimulationConfig
    {
      CampaignEnabled = true,
      Levels = CreateFastCampaign(),
      Wave1EnemyCount = 1,
      Wave2EnemyCount = 1,
      Wave3EnemyCount = 1,
      EnemyHealth = 10,
      DiverHealth = 10,
      TankHealth = 10,
      BossHealth = 60,
      ScoutSpeed = 0,
      DiverSpeed = 0,
      TankSpeed = 0,
      BossSpeed = 0,
      PlayerSpeed = 900,
      ProjectileSpeed = 2_000,
      FireCooldown = TimeSpan.FromMilliseconds(20),
      PowerUpFallSpeed = 0,
    };
    using LobbyServer server = new LobbyServer(
        IPAddress.Loopback,
        0,
        TextWriter.Null,
        countdownDuration: TimeSpan.FromMilliseconds(20),
        simulationConfig: config);
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    using TestLobbyClient first = await TestLobbyClient.ConnectAsync(port, "First", shutdown.Token);
    using TestLobbyClient second = await TestLobbyClient.ConnectAsync(port, "Second", shutdown.Token);
    await first.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await second.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await first.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    await second.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    await first.SendAsync(new InputStatePayload(1, 0, true, 1), shutdown.Token);
    await Task.Delay(250, shutdown.Token);
    await first.SendAsync(new InputStatePayload(0, 0, true, 2), shutdown.Token);

    CampaignObservation[] observations = await Task.WhenAll(
        first.ReadCampaignAsync(shutdown.Token),
        second.ReadCampaignAsync(shutdown.Token));

    Assert.All(observations, observation =>
    {
      Assert.Equal(10, observation.MaximumWave);
      Assert.Equal(2, observation.MaximumBossPhase);
      Assert.True(observation.Result.Victory);
      Assert.NotEmpty(observation.Result.Records!);
    });
    Assert.Equal(observations[0].Result.Ranking, observations[1].Result.Ranking);
    Assert.Equal(observations[0].Result.Records, observations[1].Result.Records);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  private static LevelConfig[] CreateFastCampaign()
  {
    return CampaignLevels.CreateDefault()
        .Select(level => level with
        {
          EnemyGroups =
            [
                level.EnemyGroups[0] with
                    {
                        Count = 1,
                        Health = level.EnemyGroups[0].Kind == EnemyKind.Boss ? 20 : 10,
                        Speed = 0,
                    },
            ],
        })
        .ToArray();
  }

  [Fact]
  public async Task OnePlayerCanStartAnAuthoritativeMatch()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using LobbyServer server = new LobbyServer(
        IPAddress.Loopback,
        0,
        TextWriter.Null,
        countdownDuration: TimeSpan.FromMilliseconds(20),
        simulationConfig: new SimulationConfig());
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));
    using TestLobbyClient client = await TestLobbyClient.ConnectAsync(
        port,
        "Solo",
        shutdown.Token,
        requestedPlayers: 1);

    await client.SendAsync(new SetReadyPayload(true), shutdown.Token);
    await client.ReadUntilAsync<MatchStartedPayload>(shutdown.Token);
    WorldSnapshotPayload snapshot = await client.ReadUntilAsync<WorldSnapshotPayload>(shutdown.Token);

    Assert.Single(snapshot.Players);
    Assert.Equal(client.PlayerId, snapshot.Players[0].Id.Value);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  private static async Task<ErrorPayload> ConnectExpectErrorAsync(
      int port,
      string playerName,
      int requestedPlayers,
      CancellationToken cancellationToken)
  {
    using TcpClient client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
    await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
    await TestLobbyClient.WriteMessageAsync(
        client.GetStream(),
        new ProtocolMessage(1, new HelloPayload("test", playerName, RequestedPlayers: requestedPlayers)),
        cancellationToken);
    return Assert.IsType<ErrorPayload>((await ReadMessageAsync(client.GetStream(), cancellationToken)).Payload);
  }

  private static async Task<ProtocolMessage> ReadMessageAsync(
      Stream stream,
      CancellationToken cancellationToken)
  {
    byte[]? frame = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken);
    Assert.NotNull(frame);
    return ProtocolSerializer.Deserialize(frame);
  }

  private sealed class TestLobbyClient : IDisposable
  {
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private long _sequence = 2;

    private TestLobbyClient(TcpClient client, WelcomePayload welcome)
    {
      _client = client;
      _stream = client.GetStream();
      PlayerId = welcome.PlayerId.Value;
    }

    public int PlayerId { get; }

    public static async Task<TestLobbyClient> ConnectAsync(
        int port,
        string playerName,
        CancellationToken cancellationToken,
        int requestedPlayers = 2)
    {
      TcpClient client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
      try
      {
        await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
        NetworkStream stream = client.GetStream();
        await WriteMessageAsync(
            stream,
            new ProtocolMessage(1, new HelloPayload("test", playerName, RequestedPlayers: requestedPlayers)),
            cancellationToken);
        WelcomePayload welcome = Assert.IsType<WelcomePayload>(
            (await ReadMessageAsync(stream, cancellationToken)).Payload);
        PingPayload ping = Assert.IsType<PingPayload>(
            (await ReadMessageAsync(stream, cancellationToken)).Payload);
        await WriteMessageAsync(
            stream,
            new ProtocolMessage(2, new PingResponsePayload(ping.PingId)),
            cancellationToken);
        return new TestLobbyClient(client, welcome);
      }
      catch
      {
        client.Dispose();
        throw;
      }
    }

    public async Task SendAsync(IProtocolPayload payload, CancellationToken cancellationToken)
    {
      long sequence = Interlocked.Increment(ref _sequence);
      await WriteMessageAsync(_stream, new ProtocolMessage(sequence, payload), cancellationToken);
    }

    public async Task<TPayload> ReadUntilAsync<TPayload>(CancellationToken cancellationToken)
        where TPayload : class, IProtocolPayload
        => await ReadUntilAsync<TPayload>(_ => true, cancellationToken);

    public async Task<TPayload> ReadUntilAsync<TPayload>(
        Func<TPayload, bool> predicate,
        CancellationToken cancellationToken)
        where TPayload : class, IProtocolPayload
    {
      while (true)
      {
        ProtocolMessage message = await ReadMessageAsync(_stream, cancellationToken);
        if (message.Payload is TPayload payload && predicate(payload))
        {
          return payload;
        }
      }
    }

    public async Task<CampaignObservation> ReadCampaignAsync(CancellationToken cancellationToken)
    {
      int maximumWave = 0;
      int maximumBossPhase = 0;
      while (true)
      {
        ProtocolMessage message = await ReadMessageAsync(_stream, cancellationToken);
        if (message.Payload is WorldSnapshotPayload snapshot)
        {
          maximumWave = Math.Max(maximumWave, snapshot.Wave);
          maximumBossPhase = Math.Max(maximumBossPhase, snapshot.BossPhase);
        }
        else if (message.Payload is MatchEndedPayload result)
        {
          return new CampaignObservation(maximumWave, maximumBossPhase, result);
        }
      }
    }

    public void Dispose() => _client.Dispose();

    public static ValueTask WriteMessageAsync(
        Stream stream,
        ProtocolMessage message,
        CancellationToken cancellationToken) =>
        LengthPrefixedFrame.WriteAsync(
            stream,
            ProtocolSerializer.Serialize(message),
            cancellationToken);
  }

  private sealed record CampaignObservation(
      int MaximumWave,
      int MaximumBossPhase,
      MatchEndedPayload Result);
}
