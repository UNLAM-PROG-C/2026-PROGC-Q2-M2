using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents an enemy that dives toward the player area.
/// </summary>
internal sealed class Diver(EnemyId id, Vector2D position, float speed, Hitbox hitbox, int health)
    : Enemy(id, EnemyKind.Diver, position, speed, hitbox, health);
