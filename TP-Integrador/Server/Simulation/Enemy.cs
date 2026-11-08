using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents an enemy with health and a behavior state.
/// </summary>
internal abstract class Enemy : Entity, IDamageable
{
  protected Enemy(
      EnemyId id,
      EnemyKind kind,
      Vector2D position,
      float speed,
      Hitbox hitbox,
      int health)
      : base(new EntityKey(EntityType.Enemy, id.Value), position, speed, hitbox)
  {
    Id = id;
    Kind = kind;
    Health = health;
  }

  public int Health { get; set; }

  public EnemyId Id { get; }

  public EnemyBehaviorState BehaviorState { get; set; } = EnemyBehaviorState.Entering;

  public int AttackCooldownTicks { get; set; }

  public int BehaviorTicks { get; set; }

  public EnemyKind Kind { get; }

  public float MovementPhase { get; set; }

  public float MovementSpeed => Speed;

  public EnemyStatus Status { get; set; } = EnemyStatus.Active;
}
