namespace SpaceShooter.GameDomain;

public readonly record struct PlayerId(int Value);

public readonly record struct EnemyId(int Value);

public readonly record struct ProjectileId(int Value);

public readonly record struct PowerUpId(int Value);

/// <summary>
/// Identifies any world entity while preserving its category for deterministic ordering.
/// </summary>
public readonly record struct EntityKey(EntityType EntityType, int Value) : IComparable<EntityKey>
{
  public int CompareTo(EntityKey other)
  {
    int typeComparison = EntityType.CompareTo(other.EntityType);
    return typeComparison != 0 ? typeComparison : Value.CompareTo(other.Value);
  }

  public static bool operator <(EntityKey left, EntityKey right) => left.CompareTo(right) < 0;

  public static bool operator <=(EntityKey left, EntityKey right) => left.CompareTo(right) <= 0;

  public static bool operator >(EntityKey left, EntityKey right) => left.CompareTo(right) > 0;

  public static bool operator >=(EntityKey left, EntityKey right) => left.CompareTo(right) >= 0;
}
