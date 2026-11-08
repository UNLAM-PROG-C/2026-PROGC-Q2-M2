using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents a collectible power-up moving through the game world.
/// </summary>
internal sealed class PowerUp : Entity
{
  public PowerUp(
      PowerUpId id,
      PowerUpType type,
      Vector2D position,
      float speed,
      Hitbox hitbox)
      : base(new EntityKey(EntityType.PowerUp, id.Value), position, speed, hitbox)
  {
    Id = id;
    Type = type;
    SetVelocity(new Vector2D(0f, speed));
  }

  public PowerUpId Id { get; }

  public PowerUpType Type { get; }

  public PowerUpType Kind => Type;

  public PowerUpStatus Status { get; set; } = PowerUpStatus.Available;
}
