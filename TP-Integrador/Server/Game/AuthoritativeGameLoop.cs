using SpaceShooter.Protocol;
using SpaceShooter.Server.Persistence;
using SpaceShooter.Server.Sessions;
using SpaceShooter.Server.Simulation;

namespace SpaceShooter.Server.Game;

public sealed class AuthoritativeGameLoop(
    SessionCommandChannel commands,
    LobbyCoordinator lobby,
    AuthoritativeMatch match,
    SimulationConfig config,
    IGameClock clock,
    Action<ProtocolMessage> broadcast,
    IHighScoreStore highScores)
{
    private long _outgoingSequence = 10_000;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(config.FixedStep);
        long lastSnapshotTick = -1;
        var resultPublished = false;
        var previousElapsed = clock.Elapsed;
        var accumulator = TimeSpan.Zero;

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                while (commands.Reader.TryRead(out var command))
                {
                    lobby.Apply(command, clock.Elapsed);
                }

                lobby.Update(clock.Elapsed);
                if (match.TryReset())
                {
                    resultPublished = false;
                    lastSnapshotTick = -1;
                    accumulator = TimeSpan.Zero;
                    previousElapsed = clock.Elapsed;
                }

                var startedNow = match.TryStart();
                if (!match.IsStarted || match.IsEnded)
                {
                    if (match.IsEnded && !resultPublished)
                    {
                        broadcast(CreateMessage(match.CreateSnapshot()));
                        var result = match.CreateResult();
                        var records = await highScores.UpdateAsync(result.Ranking, cancellationToken);
                        broadcast(CreateMessage(result with { Records = records }));
                        resultPublished = true;
                    }

                    continue;
                }

                var currentElapsed = clock.Elapsed;
                if (startedNow)
                {
                    previousElapsed = currentElapsed;
                    accumulator = config.FixedStep;
                }
                else
                {
                    accumulator += currentElapsed - previousElapsed;
                    previousElapsed = currentElapsed;
                }

                var completedSteps = 0;
                while (accumulator >= config.FixedStep && completedSteps < config.MaxCatchUpSteps)
                {
                    match.Step();
                    accumulator -= config.FixedStep;
                    completedSteps++;
                }

                if (completedSteps == config.MaxCatchUpSteps && accumulator >= config.FixedStep)
                {
                    accumulator = TimeSpan.Zero;
                }

                var currentTick = match.Simulation!.State.Tick;
                if (currentTick - lastSnapshotTick >= 3)
                {
                    broadcast(CreateMessage(match.CreateSnapshot()));
                    lastSnapshotTick = currentTick;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is the expected shutdown path.
        }
    }

    private ProtocolMessage CreateMessage(IProtocolPayload payload) =>
        new(Interlocked.Increment(ref _outgoingSequence), payload);
}
