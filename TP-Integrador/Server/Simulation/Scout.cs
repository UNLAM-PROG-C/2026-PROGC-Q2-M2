using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents a fast baseline enemy.
/// </summary>
internal sealed class Scout(EnemyId id, Vector2D position, float speed, Hitbox hitbox, int health)
    : Enemy(id, EnemyKind.Scout, position, speed, hitbox, health);
