using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Sessions;
using SpaceShooter.Server.Simulation;

namespace SpaceShooter.Server.Game;

public sealed class AuthoritativeMatch(
    SessionRegistry sessions,
    SimulationConfig config,
    IRandomSource random)
{
  private WorldSimulation? _simulation;

  internal WorldSimulation? Simulation => _simulation;

  public bool IsStarted => _simulation is not null;

  public bool IsEnded => _simulation?.State.MatchStatus is MatchStatus.Won or MatchStatus.Lost;

  public bool TryReset()
  {
    if (!IsEnded || sessions.Sessions.Any(session => session.Status == SessionStatus.Playing))
    {
      return false;
    }

    _simulation = null;
    return true;
  }

  public bool TryStart()
  {
    if (_simulation is not null)
    {
      return false;
    }

    ClientSession[] playingSessions = sessions.Sessions
        .Where(session => session.Status == SessionStatus.Playing)
        .OrderBy(session => session.PlayerId.Value)
        .ToArray();
    if (playingSessions.Length < 1)
    {
      return false;
    }

    _simulation = new WorldSimulation(config, random);
    AddPlayers(playingSessions);
    StartScenario();
    return true;
  }

  public void Step()
  {
    if (_simulation is null || IsEnded)
    {
      return;
    }

    RemoveDisconnectedPlayers();
    _simulation.Step(CreateInputs());
  }

  public WorldSnapshotPayload CreateSnapshot()
  {
    GameState state = _simulation?.State ?? throw new InvalidOperationException("The match has not started.");
    return new WorldSnapshotPayload(
        state.Tick,
        CreatePlayerSnapshots(state),
        CreateEnemySnapshots(state),
        CreateProjectileSnapshots(state),
        CreatePowerUpSnapshots(state),
        state.Wave,
        ActiveBossHealth(state),
        state.BossPhase,
        WaveTransitionRemainingMilliseconds(state),
        state.WaveCompletionBonus);
  }

  private void AddPlayers(ClientSession[] playingSessions)
  {
    for (int index = 0; index < playingSessions.Length; index++)
    {
      float x = config.WorldBounds.MaxX * (index + 1) / (playingSessions.Length + 1);
      float y = config.PlayerBounds.MaxY - config.PlayerRadius - 20;
      _simulation!.AddPlayer(playingSessions[index].PlayerId, new Vector2D(x, y));
    }
  }

  private void StartScenario()
  {
    if (config.CampaignEnabled)
    {
      _simulation!.StartCampaign();
      return;
    }

    Player first = _simulation!.State.Players.Values.OrderBy(player => player.Id.Value).First();
    _simulation.AddEnemy(EnemyKind.Scout, new Vector2D(first.Position.X, 140));
  }

  private void RemoveDisconnectedPlayers()
  {
    HashSet<PlayerId> activeIds = sessions.Sessions
        .Where(session => session.Status == SessionStatus.Playing)
        .Select(session => session.PlayerId)
        .ToHashSet();
    PlayerId[] disconnected = _simulation!.State.Players.Keys
        .Where(id => !activeIds.Contains(id))
        .ToArray();
    foreach (PlayerId playerId in disconnected)
    {
      _simulation.RemovePlayer(playerId);
    }
  }

  private Dictionary<PlayerId, PlayerInput> CreateInputs()
  {
    return sessions.Sessions
        .Where(session => session.Status == SessionStatus.Playing && session.LastInput is not null)
        .ToDictionary(session => session.PlayerId, CreateInput);
  }

  private static PlayerInput CreateInput(ClientSession session)
  {
    InputStatePayload input = session.LastInput!;
    return new PlayerInput(new Vector2D(input.Horizontal, input.Vertical), input.Fire);
  }

  private PlayerSnapshot[] CreatePlayerSnapshots(GameState state)
  {
    return state.Players.Values
        .OrderBy(player => player.Id.Value)
        .Select(CreatePlayerSnapshot)
        .ToArray();
  }

  private static int? ActiveBossHealth(GameState state)
  {
    return state.Enemies.Values.FirstOrDefault(
        enemy => enemy.Kind == EnemyKind.Boss && enemy.Status == EnemyStatus.Active)?.Health;
  }

  private PlayerSnapshot CreatePlayerSnapshot(Player player)
  {
    string shipId = sessions.Sessions
        .FirstOrDefault(session => session.PlayerId == player.Id)?.ShipId ?? "blue";
    return new PlayerSnapshot(
        player.Id,
        player.Position,
        player.Status,
        player.Lives,
        player.Shield,
        player.Score,
        ActivePowerUps(player),
        shipId);
  }

  private static EnemySnapshot[] CreateEnemySnapshots(GameState state)
  {
    return state.Enemies.Values
        .OrderBy(enemy => enemy.Id.Value)
        .Select(enemy => new EnemySnapshot(enemy.Id, enemy.Kind, enemy.Position, enemy.Status))
        .ToArray();
  }

  private static ProjectileSnapshot[] CreateProjectileSnapshots(GameState state)
  {
    return state.Projectiles.Values
        .OrderBy(projectile => projectile.Id.Value)
        .Select(projectile => new ProjectileSnapshot(
            projectile.Id,
            projectile.Position,
            projectile.Status,
            projectile.OwnerKey))
        .ToArray();
  }

  private static PowerUpSnapshot[] CreatePowerUpSnapshots(GameState state)
  {
    return state.PowerUps.Values
        .OrderBy(powerUp => powerUp.Id.Value)
        .Select(powerUp => new PowerUpSnapshot(
            powerUp.Id,
            powerUp.Kind,
            powerUp.Position,
            powerUp.Status))
        .ToArray();
  }

  public MatchEndedPayload CreateResult()
  {
    GameState state = _simulation?.State ?? throw new InvalidOperationException("The match has not started.");
    Dictionary<PlayerId, string> names = sessions.Sessions
        .ToDictionary(session => session.PlayerId, session => session.PlayerName);
    ScoreEntry[] ranking = state.Players.Values
        .OrderByDescending(player => player.Score)
        .ThenBy(player => player.Id.Value)
        .Select(player => new ScoreEntry(
            player.Id,
            names.GetValueOrDefault(player.Id, $"Player {player.Id.Value}"),
            player.Score))
        .ToArray();
    return new MatchEndedPayload(state.MatchStatus == MatchStatus.Won, ranking);
  }

  private static PowerUpType[] ActivePowerUps(Player player)
  {
    return [.. player.Effects.Keys.OrderBy(type => type)];
  }

  private int WaveTransitionRemainingMilliseconds(GameState state)
  {
    return (int)Math.Ceiling(
        state.WaveTransitionTicksRemaining * config.FixedStep.TotalMilliseconds);
  }
}
