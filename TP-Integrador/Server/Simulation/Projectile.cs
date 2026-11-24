using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents a projectile and the stable identity of its owner.
/// </summary>
internal sealed class Projectile : Entity
{
  public Projectile(
      ProjectileId id,
      EntityKey ownerKey,
      EntityType entityType,
      Vector2D position,
      Vector2D velocity,
      Hitbox hitbox,
      int damage)
      : base(
          new EntityKey(entityType, id.Value),
          position,
          MathF.Sqrt(velocity.LengthSquared),
          hitbox)
  {
    if (entityType is not EntityType.AlliedProjectile and not EntityType.EnemyProjectile)
    {
      throw new ArgumentException("A projectile must use a projectile entity type.", nameof(entityType));
    }

    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(damage, 0);
    Id = id;
    OwnerKey = ownerKey;
    Damage = damage;
    SetVelocity(velocity);
  }

  public int Damage { get; }

  public ProjectileId Id { get; }

  public EntityKey OwnerKey { get; }

  public PlayerId OwnerId
  {
    get
    {
      if (OwnerKey.EntityType != EntityType.Player)
      {
        throw new InvalidOperationException("Only a player-owned projectile has a player owner identifier.");
      }

      return new PlayerId(OwnerKey.Value);
    }
  }

  public ProjectileStatus Status { get; set; } = ProjectileStatus.Active;
}
