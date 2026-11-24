using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Represents one temporary modifier applied to a player.
/// </summary>
internal abstract class PowerUpEffect
{
  protected PowerUpEffect(PowerUpType type, int remainingTicks)
  {
    Type = type;
    RemainingTicks = remainingTicks;
  }

  public int RemainingTicks { get; private set; }

  public PowerUpType Type { get; }

  public abstract void Apply(Player player);

  public abstract void Remove(Player player);

  public void Refresh(int remainingTicks)
  {
    RemainingTicks = remainingTicks;
  }

  public bool Tick()
  {
    RemainingTicks--;
    return RemainingTicks <= 0;
  }
}
