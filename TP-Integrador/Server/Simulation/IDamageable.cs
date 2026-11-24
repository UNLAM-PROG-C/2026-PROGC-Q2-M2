namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Marks an entity with health that can receive authoritative damage.
/// </summary>
internal interface IDamageable
{
  int Health { get; set; }
}
