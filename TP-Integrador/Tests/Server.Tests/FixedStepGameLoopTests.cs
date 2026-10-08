using SpaceShooter.Server.Simulation;
using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Tests;

public sealed class FixedStepGameLoopTests
{
    [Fact]
    public void PumpRunsOnlyCompleteFixedStepsFromInjectedClock()
    {
        var config = new SimulationConfig
        {
            FixedStep = TimeSpan.FromMilliseconds(100),
            MaxCatchUpSteps = 5,
        };
        var simulation = new WorldSimulation(config, new SeededRandomSource(1));
        var clock = new ManualGameClock();
        var gameLoop = new FixedStepGameLoop(simulation, config, clock);

        clock.Advance(TimeSpan.FromMilliseconds(350));
        var completedSteps = gameLoop.Pump(new Dictionary<PlayerId, PlayerInput>());

        Assert.Equal(3, completedSteps);
        Assert.Equal(3, simulation.State.Tick);
    }

    [Fact]
    public void PumpCapsCatchUpToAvoidAnUnlimitedSpiral()
    {
        var config = new SimulationConfig
        {
            FixedStep = TimeSpan.FromMilliseconds(10),
            MaxCatchUpSteps = 3,
        };
        var simulation = new WorldSimulation(config, new SeededRandomSource(1));
        var clock = new ManualGameClock();
        var gameLoop = new FixedStepGameLoop(simulation, config, clock);

        clock.Advance(TimeSpan.FromSeconds(1));
        var completedSteps = gameLoop.Pump(new Dictionary<PlayerId, PlayerInput>());

        Assert.Equal(3, completedSteps);
        Assert.Equal(3, simulation.State.Tick);
    }
}
