namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents immutable geometry used by a hitbox.
/// </summary>
internal abstract class CollisionShape
{
  public abstract float BoundingRadius { get; }
}
