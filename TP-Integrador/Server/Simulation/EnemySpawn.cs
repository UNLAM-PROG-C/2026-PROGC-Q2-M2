using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

internal sealed record EnemySpawn(
    EnemyKind Kind,
    Vector2D Position,
    int Health,
    float Speed);
