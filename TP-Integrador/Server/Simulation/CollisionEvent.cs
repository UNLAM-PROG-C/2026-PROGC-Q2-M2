namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Describes a deterministic collision detected during one simulation tick.
/// </summary>
internal sealed record CollisionEvent(Entity First, Entity Second);
