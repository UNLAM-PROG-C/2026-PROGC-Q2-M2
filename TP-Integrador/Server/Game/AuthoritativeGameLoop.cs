using SpaceShooter.Protocol;
using SpaceShooter.Server.Persistence;
using SpaceShooter.Server.Sessions;
using SpaceShooter.Server.Simulation;

namespace SpaceShooter.Server.Game;

/// <summary>
/// Serializes lobby and world mutations on the authoritative fixed-step loop.
/// </summary>
public sealed class AuthoritativeGameLoop(
    SessionCommandChannel commands,
    LobbyCoordinator lobby,
    AuthoritativeMatch match,
    MatchPauseController pause,
    SimulationConfig config,
    IGameClock clock,
    Action<ProtocolMessage> broadcast,
    IHighScoreStore highScores)
{
  private long _outgoingSequence = 10_000;

  /// <summary>Runs the single-writer fixed-step loop until server cancellation.</summary>
  /// <param name="cancellationToken">Stops timers, persistence, and active processing.</param>
  /// <returns>A task representing the loop lifetime.</returns>
  public async Task RunAsync(CancellationToken cancellationToken)
  {
    using PeriodicTimer timer = new(config.FixedStep);
    GameLoopProgress progress = new(clock.Elapsed);
    try
    {
      while (await timer.WaitForNextTickAsync(cancellationToken))
      {
        await TickAsync(progress, cancellationToken);
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // Server cancellation is the expected loop shutdown path.
    }
  }

  private async Task TickAsync(
      GameLoopProgress progress,
      CancellationToken cancellationToken)
  {
    ProcessCommands();
    lobby.Update(clock.Elapsed);
    ResetMatchIfRequired(progress);
    bool startedNow = match.TryStart();
    if (!match.IsStarted || match.IsEnded)
    {
      await PublishResultIfRequiredAsync(progress, cancellationToken);
      return;
    }

    if (pause.IsPaused)
    {
      progress.Suspend(clock.Elapsed);
      return;
    }

    AdvanceSimulation(progress, startedNow);
    PublishSnapshotIfRequired(progress);
  }

  private void ProcessCommands()
  {
    while (commands.Reader.TryRead(out SessionCommand? command))
    {
      pause.Apply(command, match.Simulation?.State.Tick ?? 0);
      lobby.Apply(command, clock.Elapsed);
    }
  }

  private void ResetMatchIfRequired(GameLoopProgress progress)
  {
    if (!match.TryReset())
    {
      return;
    }

    progress.Reset(clock.Elapsed);
  }

  private async Task PublishResultIfRequiredAsync(
      GameLoopProgress progress,
      CancellationToken cancellationToken)
  {
    if (!match.IsEnded || progress.ResultPublished)
    {
      return;
    }

    broadcast(CreateMessage(match.CreateSnapshot()));
    MatchEndedPayload result = match.CreateResult();
    ScoreEntry[] records = await highScores.UpdateAsync(result.Ranking, cancellationToken);
    broadcast(CreateMessage(result with { Records = records }));
    progress.ResultPublished = true;
    lobby.OpenRematchLobby();
  }

  private void AdvanceSimulation(GameLoopProgress progress, bool startedNow)
  {
    TimeSpan currentElapsed = clock.Elapsed;
    progress.Accumulate(currentElapsed, config.FixedStep, startedNow);
    int completedSteps = RunAvailableSteps(progress);
    if (completedSteps == config.MaxCatchUpSteps && progress.Accumulator >= config.FixedStep)
    {
      progress.Accumulator = TimeSpan.Zero;
    }
  }

  private int RunAvailableSteps(GameLoopProgress progress)
  {
    int completedSteps = 0;
    while (progress.Accumulator >= config.FixedStep && completedSteps < config.MaxCatchUpSteps)
    {
      match.Step();
      progress.Accumulator -= config.FixedStep;
      completedSteps++;
    }

    return completedSteps;
  }

  private void PublishSnapshotIfRequired(GameLoopProgress progress)
  {
    const int SnapshotIntervalTicks = 3;
    long currentTick = match.Simulation!.State.Tick;
    if (currentTick - progress.LastSnapshotTick < SnapshotIntervalTicks)
    {
      return;
    }

    broadcast(CreateMessage(match.CreateSnapshot()));
    progress.LastSnapshotTick = currentTick;
  }

  private ProtocolMessage CreateMessage(IProtocolPayload payload)
  {
    return new ProtocolMessage(Interlocked.Increment(ref _outgoingSequence), payload);
  }

  private sealed class GameLoopProgress(TimeSpan elapsed)
  {
    public TimeSpan Accumulator { get; set; }

    public long LastSnapshotTick { get; set; } = -1;

    public TimeSpan PreviousElapsed { get; private set; } = elapsed;

    public bool ResultPublished { get; set; }

    public void Accumulate(TimeSpan currentElapsed, TimeSpan fixedStep, bool startedNow)
    {
      Accumulator += startedNow ? fixedStep : currentElapsed - PreviousElapsed;
      PreviousElapsed = currentElapsed;
    }

    public void Reset(TimeSpan currentElapsed)
    {
      Accumulator = TimeSpan.Zero;
      LastSnapshotTick = -1;
      PreviousElapsed = currentElapsed;
      ResultPublished = false;
    }

    public void Suspend(TimeSpan currentElapsed)
    {
      Accumulator = TimeSpan.Zero;
      PreviousElapsed = currentElapsed;
    }
  }
}
