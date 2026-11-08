namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Marks an entity that can create projectiles during a simulation tick.
/// </summary>
internal interface IShooter
{
  int FireCooldownTicks { get; }
}
