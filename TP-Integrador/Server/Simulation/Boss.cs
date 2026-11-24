using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents the configurable final-level boss.
/// </summary>
internal sealed class Boss(EnemyId id, Vector2D position, float speed, Hitbox hitbox, int health)
    : Enemy(id, EnemyKind.Boss, position, speed, hitbox, health), IShooter
{
  public int FireCooldownTicks { get; set; }
}
