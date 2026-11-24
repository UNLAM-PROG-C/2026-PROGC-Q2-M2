using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents a movable server-authoritative entity in the game world.
/// </summary>
internal abstract class Entity
{
  protected Entity(EntityKey key, Vector2D position, float speed, Hitbox hitbox)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(speed);
    Key = key;
    Position = position;
    Speed = speed;
    Hitbox = hitbox;
  }

  public Hitbox Hitbox { get; }

  public EntityKey Key { get; }

  public Vector2D Position { get; internal set; }

  public float Speed { get; private set; }

  public Vector2D Velocity { get; private set; }

  public void Move(float elapsedSeconds, WorldBounds bounds)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(elapsedSeconds);
    Position = bounds.Clamp(Position + (Velocity * elapsedSeconds), Hitbox.BoundingRadius);
  }

  public void SetPosition(Vector2D position)
  {
    Position = position;
  }

  public void SetVelocity(Vector2D velocity)
  {
    Velocity = velocity;
  }

  public void SetSpeed(float speed)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(speed);
    Speed = speed;
  }
}
