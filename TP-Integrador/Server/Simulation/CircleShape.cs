namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents a circular collision shape.
/// </summary>
internal sealed class CircleShape : CollisionShape
{
  public CircleShape(float radius)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
    Radius = radius;
  }

  public float Radius { get; }

  public override float BoundingRadius => Radius;
}
