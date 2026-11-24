namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Classifies the gameplay consequence of a collision.
/// </summary>
internal enum CollisionType
{
  None,
  Damage,
  Pickup,
}
