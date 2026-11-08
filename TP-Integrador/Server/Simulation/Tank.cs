using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents a durable enemy that can shoot.
/// </summary>
internal sealed class Tank(EnemyId id, Vector2D position, float speed, Hitbox hitbox, int health)
    : Enemy(id, EnemyKind.Tank, position, speed, hitbox, health), IShooter
{
  public int FireCooldownTicks { get; set; }
}
