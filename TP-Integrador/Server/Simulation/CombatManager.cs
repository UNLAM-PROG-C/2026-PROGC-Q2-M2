namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Applies damage and guarantees that destruction wins over later collisions.
/// </summary>
internal sealed class CombatManager
{
  private readonly HashSet<Entity> _destroyedEntities = [];

  public bool IsDestroyed(Entity entity)
  {
    return _destroyedEntities.Contains(entity);
  }

  public bool ApplyDamage(Entity target, int damage)
  {
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(damage, 0);
    if (IsDestroyed(target) || target is not IDamageable damageable)
    {
      return false;
    }

    if (ConsumeShield(target))
    {
      return false;
    }

    damageable.Health = Math.Max(0, damageable.Health - damage);
    return MarkIfDestroyed(target, damageable.Health);
  }

  public void BeginTick()
  {
    _destroyedEntities.Clear();
  }

  private static bool ConsumeShield(Entity target)
  {
    if (target is not Player { ShieldActive: true } player)
    {
      return false;
    }

    player.ShieldActive = false;
    player.Effects.Remove(SpaceShooter.GameDomain.PowerUpType.Shield);
    return true;
  }

  private bool MarkIfDestroyed(Entity target, int health)
  {
    if (health > 0)
    {
      return false;
    }

    _destroyedEntities.Add(target);
    return true;
  }
}
