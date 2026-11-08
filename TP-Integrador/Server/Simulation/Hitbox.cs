using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Defines one immutable collision area relative to an entity position.
/// </summary>
internal sealed class Hitbox
{
  public Hitbox(Vector2D offset, CollisionShape shape)
  {
    Offset = offset;
    Shape = shape;
  }

  public Vector2D Offset { get; }

  public CollisionShape Shape { get; }

  public float BoundingRadius => Shape.BoundingRadius;

  public Vector2D GetCenter(Vector2D entityPosition)
  {
    return entityPosition + Offset;
  }
}
