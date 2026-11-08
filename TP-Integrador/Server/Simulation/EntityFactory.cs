using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Creates authoritative entities and owns monotonic identifiers for a match.
/// </summary>
internal sealed class EntityFactory
{
  private int _nextEnemyId = 1;
  private int _nextPlayerId = 1;
  private int _nextPowerUpId = 1;
  private int _nextProjectileId = 1;

  public static EntityKey CreateKey(PlayerId id)
  {
    return new EntityKey(EntityType.Player, id.Value);
  }

  public Player CreatePlayer(
      Vector2D position,
      float speed,
      Hitbox hitbox,
      int lives,
      int health)
  {
    PlayerId id = new(_nextPlayerId++);
    return new Player(id, position, speed, hitbox, lives, health);
  }

  public Player CreatePlayer(
      PlayerId id,
      Vector2D position,
      float speed,
      Hitbox hitbox,
      int lives,
      int health)
  {
    ReservePlayerId(id);
    return new Player(id, position, speed, hitbox, lives, health);
  }

  public PlayerId CreatePlayerId()
  {
    return new PlayerId(_nextPlayerId++);
  }

  public void ReservePlayerId(PlayerId id)
  {
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(id.Value, 0);
    _nextPlayerId = Math.Max(_nextPlayerId, id.Value + 1);
  }

  public Enemy CreateEnemy(
      EnemyKind kind,
      Vector2D position,
      float speed,
      Hitbox hitbox,
      int health)
  {
    EnemyId id = CreateEnemyId();
    return kind switch
    {
      EnemyKind.Scout => new Scout(id, position, speed, hitbox, health),
      EnemyKind.Diver => new Diver(id, position, speed, hitbox, health),
      EnemyKind.Tank => new Tank(id, position, speed, hitbox, health),
      EnemyKind.Boss => new Boss(id, position, speed, hitbox, health),
      _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported enemy kind."),
    };
  }

  public PowerUp CreatePowerUp(
      PowerUpType type,
      Vector2D position,
      float speed,
      Hitbox hitbox)
  {
    return new PowerUp(CreatePowerUpId(), type, position, speed, hitbox);
  }

  public Projectile CreateProjectile(
      EntityKey ownerKey,
      EntityType type,
      Vector2D position,
      Vector2D velocity,
      Hitbox hitbox,
      int damage)
  {
    return new Projectile(
        CreateProjectileId(),
        ownerKey,
        type,
        position,
        velocity,
        hitbox,
        damage);
  }

  public EnemyId CreateEnemyId()
  {
    return new EnemyId(_nextEnemyId++);
  }

  public PowerUpId CreatePowerUpId()
  {
    return new PowerUpId(_nextPowerUpId++);
  }

  public ProjectileId CreateProjectileId()
  {
    return new ProjectileId(_nextProjectileId++);
  }
}
