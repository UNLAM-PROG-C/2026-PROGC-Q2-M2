using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

internal sealed class RapidFireEffect(int ticks) : PowerUpEffect(PowerUpType.RapidFire, ticks)
{
  private const float ActiveCooldownScale = 0.5f;

  public override void Apply(Player player)
  {
    player.FireCooldownScale = ActiveCooldownScale;
  }

  public override void Remove(Player player)
  {
    player.FireCooldownScale = 1f;
  }
}

internal sealed class MultiShotEffect(int ticks) : PowerUpEffect(PowerUpType.MultiShot, ticks)
{
  private const int ActiveProjectileCount = 3;

  public override void Apply(Player player)
  {
    player.ProjectileCount = ActiveProjectileCount;
  }

  public override void Remove(Player player)
  {
    player.ProjectileCount = 1;
  }
}

internal sealed class ShieldEffect(int ticks) : PowerUpEffect(PowerUpType.Shield, ticks)
{
  public override void Apply(Player player)
  {
    player.ShieldActive = true;
  }

  public override void Remove(Player player)
  {
    player.ShieldActive = false;
  }
}

internal sealed class SpeedBoostEffect(int ticks, float multiplier)
    : PowerUpEffect(PowerUpType.SpeedBoost, ticks)
{
  private float _originalSpeed;

  public override void Apply(Player player)
  {
    _originalSpeed = player.Speed;
    player.SetSpeed(_originalSpeed * multiplier);
  }

  public override void Remove(Player player)
  {
    player.SetSpeed(_originalSpeed);
  }
}
