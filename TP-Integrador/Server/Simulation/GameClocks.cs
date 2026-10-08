using System.Diagnostics;

namespace SpaceShooter.Server.Simulation;

public interface IGameClock
{
    TimeSpan Elapsed { get; }
}

public sealed class StopwatchGameClock : IGameClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public TimeSpan Elapsed => _stopwatch.Elapsed;
}

public sealed class ManualGameClock : IGameClock
{
    public TimeSpan Elapsed { get; private set; }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        Elapsed += elapsed;
    }
}
