using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Selects deterministic power-up drops using the match random source.
/// </summary>
internal sealed class PowerUpDropPolicy(
    SimulationConfig config,
    IRandomSource random,
    LevelManager levels)
{
  private const int ProbabilityScale = 10_000;

  public bool TryChoose(int wave, out PowerUpType type)
  {
    IReadOnlyList<PowerUpType> available = AvailablePowerUps(wave);
    bool shouldDrop = random.NextInt(0, ProbabilityScale) < DropThreshold();
    if (!shouldDrop)
    {
      type = default;
      return false;
    }

    type = available[random.NextInt(0, available.Count)];
    return true;
  }

  private int DropThreshold()
  {
    return (int)MathF.Round(config.PowerUpDropChance * ProbabilityScale);
  }

  private IReadOnlyList<PowerUpType> AvailablePowerUps(int wave)
  {
    return wave <= 0 ? Enum.GetValues<PowerUpType>() : levels.GetLevel(wave).AvailablePowerUps;
  }
}
