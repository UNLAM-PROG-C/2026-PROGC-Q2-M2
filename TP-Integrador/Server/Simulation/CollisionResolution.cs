namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Describes the gameplay rule selected for an intersecting pair.
/// </summary>
internal sealed record CollisionResolution(
    CollisionType Type,
    Entity Source,
    Entity Target);
