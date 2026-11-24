using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Creates, registers, and updates enemies without owning a second collection.
/// </summary>
internal sealed class EnemyManager(
    SimulationConfig config,
    EntityFactory factory,
    GameState state)
{
  public Enemy Create(
      EnemyKind kind,
      Vector2D position,
      int health,
      float speed,
      TimeSpan? attackInterval)
  {
    Hitbox hitbox = CreateHitbox(kind);
    Enemy enemy = factory.CreateEnemy(kind, position, speed, hitbox, health);
    if (enemy is IShooter && attackInterval is not null)
    {
      enemy.AttackCooldownTicks = DurationInTicks(attackInterval.Value);
    }

    state.AddEnemy(enemy);
    return enemy;
  }

  public bool UpdateMovement(Enemy enemy, float elapsedSeconds, float speedMultiplier)
  {
    EnemyStateMachine.Update(enemy);
    enemy.MovementPhase += elapsedSeconds;
    float direction = (enemy.Id.Value + (int)enemy.MovementPhase) % 2 == 0 ? 1f : -1f;
    Vector2D velocity = VelocityFor(enemy, direction, speedMultiplier);
    enemy.SetVelocity(velocity);
    enemy.Position += velocity * elapsedSeconds;
    ClampHorizontalPosition(enemy);
    return enemy.Position.Y > config.WorldBounds.MaxY - VerticalMargin(enemy.Kind);
  }

  public Vector2D ClampSpawn(EnemyKind kind, Vector2D position)
  {
    if (kind != EnemyKind.Boss)
    {
      return config.WorldBounds.Clamp(position, config.EnemyRadius);
    }

    float x = Math.Clamp(
        position.X,
        config.WorldBounds.MinX + HorizontalMargin(kind),
        config.WorldBounds.MaxX - HorizontalMargin(kind));
    float y = Math.Clamp(
        position.Y,
        config.WorldBounds.MinY + VerticalMargin(kind),
        config.WorldBounds.MaxY - VerticalMargin(kind));
    return new Vector2D(x, y);
  }

  public Vector2D CreateTopSpawn(EnemyKind kind, float x)
  {
    return new Vector2D(x, config.WorldBounds.MinY + VerticalMargin(kind));
  }

  public void ResetAtTop(Enemy enemy)
  {
    enemy.Position = CreateTopSpawn(enemy.Kind, enemy.Position.X);
  }

  private void ClampHorizontalPosition(Enemy enemy)
  {
    float margin = HorizontalMargin(enemy.Kind);
    float minimum = config.WorldBounds.MinX + margin;
    float maximum = config.WorldBounds.MaxX - margin;
    enemy.Position = new Vector2D(Math.Clamp(enemy.Position.X, minimum, maximum), enemy.Position.Y);
  }

  private int DurationInTicks(TimeSpan duration)
  {
    return Math.Max(1, (int)Math.Ceiling(duration / config.FixedStep));
  }

  private Hitbox CreateHitbox(EnemyKind kind)
  {
    CollisionShape shape = kind == EnemyKind.Boss
        ? new RectangleShape(config.BossHitboxWidth, config.BossHitboxHeight)
        : new CircleShape(config.EnemyRadius);
    return new Hitbox(Vector2D.Zero, shape);
  }

  private float HorizontalMargin(EnemyKind kind)
  {
    return kind == EnemyKind.Boss ? config.BossHitboxWidth / 2f : config.EnemyRadius;
  }

  private float VerticalMargin(EnemyKind kind)
  {
    return kind == EnemyKind.Boss ? config.BossHitboxHeight / 2f : config.EnemyRadius;
  }

  private static Vector2D VelocityFor(Enemy enemy, float direction, float bossMultiplier)
  {
    float speed = enemy.MovementSpeed;
    return enemy.Kind switch
    {
      EnemyKind.Scout => new Vector2D(direction * speed, speed * 0.6f),
      EnemyKind.Diver => new Vector2D(direction * speed * 0.25f, speed),
      EnemyKind.Tank => new Vector2D(direction * speed * 0.15f, speed),
      EnemyKind.Boss => new Vector2D(direction * speed * bossMultiplier, 0f),
      _ => Vector2D.Zero,
    };
  }
}
