namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents an axis-aligned rectangular collision shape.
/// </summary>
internal sealed class RectangleShape : CollisionShape
{
  public RectangleShape(float width, float height)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
    Width = width;
    Height = height;
  }

  public float Height { get; }

  public float Width { get; }

  public override float BoundingRadius => MathF.Sqrt((Width * Width) + (Height * Height)) / 2f;
}
