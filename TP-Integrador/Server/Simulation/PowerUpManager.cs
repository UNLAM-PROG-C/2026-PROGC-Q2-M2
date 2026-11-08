using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Applies collectible effects and advances their deterministic tick durations.
/// </summary>
internal static class PowerUpManager
{
  private const float DefaultSpeedBoostMultiplier = 1.5f;

  public static void Collect(
      Player player,
      PowerUpType type,
      int durationTicks,
      float speedBoostMultiplier = DefaultSpeedBoostMultiplier)
  {
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(durationTicks, 0);
    if (type == PowerUpType.ExtraLife)
    {
      player.Lives++;
      return;
    }

    ApplyOrRefresh(player, type, durationTicks, speedBoostMultiplier);
  }

  public static void Update(Player player)
  {
    PowerUpEffect[] expiredEffects = player.Effects.Values
        .Where(effect => effect.Tick())
        .OrderBy(effect => effect.Type)
        .ToArray();
    RemoveExpired(player, expiredEffects);
  }

  private static PowerUpEffect CreateEffect(
      PowerUpType type,
      int durationTicks,
      float speedBoostMultiplier)
  {
    return type switch
    {
      PowerUpType.RapidFire => new RapidFireEffect(durationTicks),
      PowerUpType.MultiShot => new MultiShotEffect(durationTicks),
      PowerUpType.Shield => new ShieldEffect(durationTicks),
      PowerUpType.SpeedBoost => new SpeedBoostEffect(durationTicks, speedBoostMultiplier),
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported timed effect."),
    };
  }

  private static void RemoveExpired(Player player, IEnumerable<PowerUpEffect> effects)
  {
    foreach (PowerUpEffect effect in effects)
    {
      effect.Remove(player);
      player.Effects.Remove(effect.Type);
    }
  }

  private static void ApplyOrRefresh(
      Player player,
      PowerUpType type,
      int durationTicks,
      float speedBoostMultiplier)
  {
    if (player.Effects.TryGetValue(type, out PowerUpEffect? effect))
    {
      effect.Refresh(durationTicks);
      return;
    }

    PowerUpEffect newEffect = CreateEffect(type, durationTicks, speedBoostMultiplier);
    player.Effects.Add(type, newEffect);
    newEffect.Apply(player);
  }
}
