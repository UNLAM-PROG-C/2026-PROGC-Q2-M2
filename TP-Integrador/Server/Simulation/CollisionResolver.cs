using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Maps geometric intersections to gameplay interactions.
/// </summary>
internal static class CollisionResolver
{
  public static CollisionResolution? Resolve(CollisionEvent collision)
  {
    Entity first = collision.First;
    Entity second = collision.Second;
    return ResolveOrdered(first, second) ?? ResolveOrdered(second, first);
  }

  private static CollisionResolution? ResolveOrdered(Entity source, Entity target)
  {
    return (source.Key.EntityType, target.Key.EntityType) switch
    {
      (EntityType.AlliedProjectile, EntityType.Enemy) =>
          new CollisionResolution(CollisionType.Damage, source, target),
      (EntityType.EnemyProjectile, EntityType.Player) =>
          new CollisionResolution(CollisionType.Damage, source, target),
      (EntityType.Enemy, EntityType.Player) =>
          new CollisionResolution(CollisionType.Damage, source, target),
      (EntityType.PowerUp, EntityType.Player) =>
          new CollisionResolution(CollisionType.Pickup, source, target),
      _ => null,
    };
  }
}
