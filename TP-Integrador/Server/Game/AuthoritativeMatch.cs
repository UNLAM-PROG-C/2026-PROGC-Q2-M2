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
    if (!IsEnded || sessions.Count != 0)
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
    for (int index = 0; index < playingSessions.Length; index++)
    {
      float x = config.WorldBounds.MaxX * (index + 1) / (playingSessions.Length + 1);
      float y = config.PlayerBounds.MaxY - config.PlayerRadius - 20;
      _simulation.AddPlayer(playingSessions[index].PlayerId, new Vector2D(x, y));
    }

    if (config.CampaignEnabled)
    {
      _simulation.StartCampaign();
    }
    else
    {
      Player firstPlayer = _simulation.State.Players.Values.OrderBy(player => player.Id.Value).First();
      _simulation.AddEnemy(EnemyKind.Scout, new Vector2D(firstPlayer.Position.X, 140));
    }

    return true;
  }

  public void Step()
  {
    if (_simulation is null || IsEnded)
    {
      return;
    }

    HashSet<PlayerId> activeSessionIds = sessions.Sessions
        .Where(session => session.Status == SessionStatus.Playing)
        .Select(session => session.PlayerId)
        .ToHashSet();
    foreach (PlayerId playerId in _simulation.State.Players.Keys.Where(id => !activeSessionIds.Contains(id)).ToArray())
    {
      _simulation.RemovePlayer(playerId);
    }

    Dictionary<PlayerId, PlayerInput> inputs = sessions.Sessions
        .Where(session => session.Status == SessionStatus.Playing && session.LastInput is not null)
        .ToDictionary(
            session => session.PlayerId,
            session => new PlayerInput(
                new Vector2D(session.LastInput!.Horizontal, session.LastInput.Vertical),
                session.LastInput.Fire));

    _simulation.Step(inputs);
  }

  public WorldSnapshotPayload CreateSnapshot()
  {
    GameState state = _simulation?.State ?? throw new InvalidOperationException("The match has not started.");
    return new WorldSnapshotPayload(
        state.Tick,
        state.Players.Values.OrderBy(player => player.Id.Value).Select(player => new PlayerSnapshot(
            player.Id,
            player.Position,
            player.Status,
            player.Lives,
            player.Shield,
            player.Score,
            ActivePowerUps(player))).ToArray(),
        state.Enemies.Values.OrderBy(enemy => enemy.Id.Value).Select(enemy => new EnemySnapshot(
            enemy.Id,
            enemy.Kind,
            enemy.Position,
            enemy.Status)).ToArray(),
        state.Projectiles.Values.OrderBy(projectile => projectile.Id.Value).Select(projectile => new ProjectileSnapshot(
            projectile.Id,
            projectile.Position,
            projectile.Status)).ToArray(),
        state.PowerUps.Values.OrderBy(powerUp => powerUp.Id.Value).Select(powerUp => new PowerUpSnapshot(
            powerUp.Id,
            powerUp.Kind,
            powerUp.Position,
            powerUp.Status)).ToArray(),
        state.Wave,
        state.Enemies.Values.FirstOrDefault(enemy => enemy.Kind == EnemyKind.Boss && enemy.Status == EnemyStatus.Active)?.Health,
        state.BossPhase);
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
}
