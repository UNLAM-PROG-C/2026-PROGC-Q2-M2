using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

public readonly record struct PlayerInput(Vector2D Movement, bool Fire);
