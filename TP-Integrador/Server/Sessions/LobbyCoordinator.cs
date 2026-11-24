using SpaceShooter.Protocol;

namespace SpaceShooter.Server.Sessions;

public sealed class LobbyCoordinator
{
  public static readonly TimeSpan DefaultCountdownDuration = TimeSpan.FromSeconds(3);

  private readonly SessionRegistry _registry;
  private readonly Action<ProtocolMessage> _broadcast;
  private readonly TimeSpan _countdownDuration;
  private readonly Func<int>? _requiredPlayers;
  private readonly Action? _matchStarted;
  private readonly Action? _matchEnded;
  private long _outgoingSequence;
  private long _nextMatchId;
  private TimeSpan? _countdownDeadline;
  private int? _lastPublishedCountdown;

  public LobbyCoordinator(
      SessionRegistry registry,
      Action<ProtocolMessage> broadcast,
      TimeSpan? countdownDuration = null,
      Func<int>? requiredPlayers = null,
      Action? matchStarted = null,
      Action? matchEnded = null)
  {
    _registry = registry;
    _broadcast = broadcast;
    _countdownDuration = countdownDuration ?? DefaultCountdownDuration;
    _requiredPlayers = requiredPlayers;
    _matchStarted = matchStarted;
    _matchEnded = matchEnded;
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_countdownDuration, TimeSpan.Zero);
  }

  public TimeSpan? CountdownDeadline => _countdownDeadline;

  public void Apply(SessionCommand command, TimeSpan now)
  {
    bool lobbyChanged = command switch
    {
      JoinSessionCommand join => ApplyJoin(join),
      ClientPayloadCommand clientCommand => ApplyClientPayload(clientCommand),
      DisconnectSessionCommand disconnect => ApplyDisconnect(disconnect),
      _ => false,
    };

    UpdateCountdown(now, lobbyChanged);
  }

  public void Update(TimeSpan now) => UpdateCountdown(now, lobbyChanged: false);

  public void OpenRematchLobby()
  {
    ClientSession[] sessions = _registry.Sessions.OrderBy(session => session.PlayerId.Value).ToArray();
    foreach (ClientSession session in sessions)
    {
      session.Status = SessionStatus.InLobby;
      session.LastInput = null;
    }

    _countdownDeadline = null;
    _lastPublishedCountdown = null;
    _matchEnded?.Invoke();
    BroadcastLobby(sessions, countdownSeconds: null);
  }

  private bool ApplyJoin(JoinSessionCommand command)
  {
    if (_registry.TryGet(command.PlayerId, out _))
    {
      return false;
    }

    ClientSession session = _registry.Add(command.PlayerId, command.Hello.PlayerName);
    session.Status = SessionStatus.InLobby;
    return true;
  }

  private bool ApplyClientPayload(ClientPayloadCommand command)
  {
    if (!_registry.TryGet(command.PlayerId, out ClientSession? session) || session is null)
    {
      return false;
    }

    switch (command.Payload)
    {
      case SelectShipPayload selectShip when session.Status is SessionStatus.InLobby or SessionStatus.Ready:
        session.ShipId = selectShip.ShipId;
        return true;
      case SetReadyPayload setReady when session.Status is SessionStatus.InLobby or SessionStatus.Ready:
        session.Status = setReady.Ready ? SessionStatus.Ready : SessionStatus.InLobby;
        return true;
      case InputStatePayload input when session.Status == SessionStatus.Playing:
        session.LastInput = input;
        return false;
      default:
        return false;
    }
  }

  private bool ApplyDisconnect(DisconnectSessionCommand command) =>
      _registry.Remove(command.PlayerId) is not null;

  private void UpdateCountdown(TimeSpan now, bool lobbyChanged)
  {
    ClientSession[] sessions = _registry.Sessions.OrderBy(session => session.PlayerId.Value).ToArray();
    if (sessions.Any(session => session.Status == SessionStatus.Playing))
    {
      return;
    }

    if (!AreAllPlayersReady(sessions))
    {
      CancelCountdown(sessions, lobbyChanged);
      return;
    }

    bool countdownStarted = EnsureCountdownStarted(now);
    if (now >= _countdownDeadline!.Value)
    {
      StartMatch(sessions);
      return;
    }

    PublishCountdown(sessions, now, lobbyChanged || countdownStarted);
  }

  private bool AreAllPlayersReady(ClientSession[] sessions)
  {
    int? requiredPlayers = _requiredPlayers?.Invoke();
    bool hasRequiredPlayers = requiredPlayers is null
        ? sessions.Length is >= 1 and <= 4
        : sessions.Length == requiredPlayers;
    return hasRequiredPlayers && sessions.All(session => session.Status == SessionStatus.Ready);
  }

  private void CancelCountdown(ClientSession[] sessions, bool lobbyChanged)
  {
    bool wasCountingDown = _countdownDeadline is not null;
    _countdownDeadline = null;
    _lastPublishedCountdown = null;
    if (lobbyChanged || wasCountingDown)
    {
      BroadcastLobby(sessions, countdownSeconds: null);
    }
  }

  private bool EnsureCountdownStarted(TimeSpan now)
  {
    if (_countdownDeadline is not null)
    {
      return false;
    }

    _countdownDeadline = now + _countdownDuration;
    _lastPublishedCountdown = null;
    return true;
  }

  private void StartMatch(ClientSession[] sessions)
  {
    foreach (ClientSession session in sessions)
    {
      session.Status = SessionStatus.Playing;
    }

    ResetCountdown();
    _matchStarted?.Invoke();
    MatchStartedPayload payload = new(Interlocked.Increment(ref _nextMatchId), 1, 0);
    _broadcast(new ProtocolMessage(NextSequence(), payload));
  }

  private void ResetCountdown()
  {
    _countdownDeadline = null;
    _lastPublishedCountdown = null;
  }

  private void PublishCountdown(
      ClientSession[] sessions,
      TimeSpan now,
      bool lobbyChanged)
  {
    double remaining = (_countdownDeadline!.Value - now).TotalSeconds;
    int secondsRemaining = Math.Max(1, (int)Math.Ceiling(remaining));
    if (lobbyChanged || secondsRemaining != _lastPublishedCountdown)
    {
      _lastPublishedCountdown = secondsRemaining;
      BroadcastLobby(sessions, secondsRemaining);
    }
  }

  private void BroadcastLobby(ClientSession[] sessions, int? countdownSeconds)
  {
    LobbyParticipant[] participants = sessions
        .Select(session => new LobbyParticipant(
            session.PlayerId,
            session.PlayerName,
            session.ShipId,
            session.Status == SessionStatus.Ready))
        .ToArray();

    _broadcast(new ProtocolMessage(
        NextSequence(),
        new LobbyStatePayload(
            participants,
            countdownSeconds,
            _requiredPlayers?.Invoke() ?? Math.Clamp(sessions.Length, 1, 4))));
  }

  private long NextSequence() => Interlocked.Increment(ref _outgoingSequence);
}
