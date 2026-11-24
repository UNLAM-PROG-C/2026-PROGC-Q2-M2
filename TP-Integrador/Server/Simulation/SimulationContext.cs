namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Groups the state and deterministic services owned by one simulation.
/// </summary>
internal sealed record SimulationContext(
    SimulationConfig Config,
    EntityFactory Factory,
    GameState State)
{
  public int DurationInTicks(TimeSpan duration)
  {
    return Math.Max(1, (int)Math.Ceiling(duration / Config.FixedStep));
  }
}
