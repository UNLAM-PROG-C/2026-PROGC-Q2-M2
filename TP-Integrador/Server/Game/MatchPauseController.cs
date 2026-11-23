using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Sessions;

namespace SpaceShooter.Server.Game;

/// <summary>Owns the authoritative pause state on the game-loop thread.</summary>
public sealed class MatchPauseController(
    SessionRegistry sessions,
    Action<ProtocolMessage> broadcast)
{
  private long _outgoingSequence;

  public PlayerId? PausedBy { get; private set; }

  public bool IsPaused => PausedBy is not null;

  public void Apply(SessionCommand command, long serverTick)
  {
    switch (command)
    {
      case ClientPayloadCommand { Payload: SetPausePayload pause } client:
        ApplyRequest(client.PlayerId, pause, serverTick);
        break;
      case DisconnectSessionCommand disconnect when PausedBy == disconnect.PlayerId:
        Resume(serverTick);
        break;
    }
  }

  private void ApplyRequest(PlayerId playerId, SetPausePayload request, long serverTick)
  {
    if (!sessions.TryGet(playerId, out ClientSession? session) ||
        session?.Status != SessionStatus.Playing)
    {
      return;
    }

    if (request.Paused)
    {
      Pause(playerId, serverTick);
    }
    else if (PausedBy == playerId)
    {
      Resume(serverTick);
    }
    else if (IsPaused)
    {
      Publish(serverTick);
    }
  }

  private void Pause(PlayerId playerId, long serverTick)
  {
    if (IsPaused)
    {
      return;
    }

    PausedBy = playerId;
    ClearPlayerInputs();
    Publish(serverTick);
  }

  private void ClearPlayerInputs()
  {
    foreach (ClientSession session in sessions.Sessions)
    {
      session.LastInput = null;
    }
  }

  private void Resume(long serverTick)
  {
    PausedBy = null;
    Publish(serverTick);
  }

  private void Publish(long serverTick)
  {
    PauseStatePayload payload = new(IsPaused, PausedBy, serverTick);
    broadcast(new ProtocolMessage(Interlocked.Increment(ref _outgoingSequence), payload));
  }
}
