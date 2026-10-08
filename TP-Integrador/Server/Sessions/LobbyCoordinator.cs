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
    private long _outgoingSequence;
    private long _nextMatchId;
    private TimeSpan? _countdownDeadline;
    private int? _lastPublishedCountdown;

    public LobbyCoordinator(
        SessionRegistry registry,
        Action<ProtocolMessage> broadcast,
        TimeSpan? countdownDuration = null,
        Func<int>? requiredPlayers = null,
        Action? matchStarted = null)
    {
        _registry = registry;
        _broadcast = broadcast;
        _countdownDuration = countdownDuration ?? DefaultCountdownDuration;
        _requiredPlayers = requiredPlayers;
        _matchStarted = matchStarted;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_countdownDuration, TimeSpan.Zero);
    }

    public TimeSpan? CountdownDeadline => _countdownDeadline;

    public void Apply(SessionCommand command, TimeSpan now)
    {
        var lobbyChanged = command switch
        {
            JoinSessionCommand join => ApplyJoin(join),
            ClientPayloadCommand clientCommand => ApplyClientPayload(clientCommand),
            DisconnectSessionCommand disconnect => ApplyDisconnect(disconnect),
            _ => false,
        };

        UpdateCountdown(now, lobbyChanged);
    }

    public void Update(TimeSpan now) => UpdateCountdown(now, lobbyChanged: false);

    private bool ApplyJoin(JoinSessionCommand command)
    {
        if (_registry.TryGet(command.PlayerId, out _))
        {
            return false;
        }

        var session = _registry.Add(command.PlayerId, command.Hello.PlayerName);
        session.Status = SessionStatus.InLobby;
        return true;
    }

    private bool ApplyClientPayload(ClientPayloadCommand command)
    {
        if (!_registry.TryGet(command.PlayerId, out var session) || session is null)
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
        var sessions = _registry.Sessions.OrderBy(session => session.PlayerId.Value).ToArray();
        if (sessions.Any(session => session.Status == SessionStatus.Playing))
        {
            return;
        }

        var requiredPlayers = _requiredPlayers?.Invoke();
        var hasRequiredPlayers = requiredPlayers is null
            ? sessions.Length is >= 1 and <= 4
            : sessions.Length == requiredPlayers;
        var allReady = hasRequiredPlayers &&
                       sessions.All(session => session.Status == SessionStatus.Ready);

        if (!allReady)
        {
            if (_countdownDeadline is not null)
            {
                _countdownDeadline = null;
                _lastPublishedCountdown = null;
                lobbyChanged = true;
            }

            if (lobbyChanged)
            {
                BroadcastLobby(sessions, countdownSeconds: null);
            }

            return;
        }

        if (_countdownDeadline is null)
        {
            _countdownDeadline = now + _countdownDuration;
            _lastPublishedCountdown = null;
            lobbyChanged = true;
        }

        if (now >= _countdownDeadline.Value)
        {
            foreach (var session in sessions)
            {
                session.Status = SessionStatus.Playing;
            }

            _countdownDeadline = null;
            _lastPublishedCountdown = null;
            _matchStarted?.Invoke();
            _broadcast(new ProtocolMessage(
                NextSequence(),
                new MatchStartedPayload(
                    MatchId: Interlocked.Increment(ref _nextMatchId),
                    Seed: 1,
                    InitialTick: 0)));
            return;
        }

        var secondsRemaining = Math.Max(1, (int)Math.Ceiling((_countdownDeadline.Value - now).TotalSeconds));
        if (lobbyChanged || secondsRemaining != _lastPublishedCountdown)
        {
            _lastPublishedCountdown = secondsRemaining;
            BroadcastLobby(sessions, secondsRemaining);
        }
    }

    private void BroadcastLobby(ClientSession[] sessions, int? countdownSeconds)
    {
        var participants = sessions
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
