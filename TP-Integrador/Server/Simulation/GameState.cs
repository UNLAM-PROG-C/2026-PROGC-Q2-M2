using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Owns every mutable entity collection for one authoritative match.
/// </summary>
internal sealed class GameState
{
  private readonly Dictionary<EnemyId, Enemy> _enemies = [];

  private readonly Dictionary<PlayerId, Player> _players = [];

  private readonly Dictionary<PowerUpId, PowerUp> _powerUps = [];

  private readonly Dictionary<ProjectileId, Projectile> _projectiles = [];

  public int BossPhase { get; internal set; }

  public IReadOnlyDictionary<EnemyId, Enemy> Enemies => _enemies;

  public MatchStatus MatchStatus { get; internal set; } = MatchStatus.Waiting;

  public IReadOnlyDictionary<PlayerId, Player> Players => _players;

  public IReadOnlyDictionary<PowerUpId, PowerUp> PowerUps => _powerUps;

  public IReadOnlyDictionary<ProjectileId, Projectile> Projectiles => _projectiles;

  public long Tick { get; internal set; }

  public int Wave { get; internal set; }

  internal void AddEnemy(Enemy enemy)
  {
    _enemies.Add(enemy.Id, enemy);
  }

  internal void AddPlayer(Player player)
  {
    _players.Add(player.Id, player);
  }

  internal void AddPowerUp(PowerUp powerUp)
  {
    _powerUps.Add(powerUp.Id, powerUp);
  }

  internal void AddProjectile(Projectile projectile)
  {
    _projectiles.Add(projectile.Id, projectile);
  }

  public IEnumerable<Entity> GetEntities()
  {
    return _players.Values.Cast<Entity>()
        .Concat(_enemies.Values)
        .Concat(_projectiles.Values)
        .Concat(_powerUps.Values)
        .OrderBy(entity => entity.Key);
  }

  internal bool RemoveEnemy(EnemyId id)
  {
    return _enemies.Remove(id);
  }

  internal bool RemovePlayer(PlayerId id)
  {
    return _players.Remove(id);
  }

  internal bool RemovePowerUp(PowerUpId id)
  {
    return _powerUps.Remove(id);
  }

  internal bool RemoveProjectile(ProjectileId id)
  {
    return _projectiles.Remove(id);
  }
}
