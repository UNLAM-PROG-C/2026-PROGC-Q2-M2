using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

internal sealed class FixedStepGameLoop
{
  private readonly WorldSimulation _simulation;
  private readonly SimulationConfig _config;
  private readonly IGameClock _clock;
  private TimeSpan _lastElapsed;
  private TimeSpan _accumulator;

  public FixedStepGameLoop(WorldSimulation simulation, SimulationConfig config, IGameClock clock)
  {
    config.Validate();
    _simulation = simulation;
    _config = config;
    _clock = clock;
    _lastElapsed = clock.Elapsed;
  }

  public int Pump(IReadOnlyDictionary<PlayerId, PlayerInput> inputs)
  {
    TimeSpan now = _clock.Elapsed;
    TimeSpan elapsed = now - _lastElapsed;
    if (elapsed < TimeSpan.Zero)
    {
      throw new InvalidOperationException("The game clock cannot move backwards.");
    }

    _lastElapsed = now;
    _accumulator += elapsed;

    int completedSteps = 0;
    while (_accumulator >= _config.FixedStep && completedSteps < _config.MaxCatchUpSteps)
    {
      _simulation.Step(inputs);
      _accumulator -= _config.FixedStep;
      completedSteps++;
    }

    if (completedSteps == _config.MaxCatchUpSteps && _accumulator >= _config.FixedStep)
    {
      _accumulator = TimeSpan.Zero;
    }

    return completedSteps;
  }
}
