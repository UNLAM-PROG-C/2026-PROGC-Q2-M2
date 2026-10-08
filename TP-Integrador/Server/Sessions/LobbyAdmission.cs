using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Sessions;

public sealed class LobbyAdmission
{
    private readonly object _sync = new();
    private readonly HashSet<PlayerId> _players = [];
    private int? _targetPlayers;
    private bool _matchStarted;

    public int TargetPlayers
    {
        get
        {
            lock (_sync)
            {
                return _targetPlayers ?? 1;
            }
        }
    }

    public bool TryAdmit(
        PlayerId playerId,
        int requestedPlayers,
        out string errorCode,
        out string errorDescription)
    {
        lock (_sync)
        {
            if (requestedPlayers is < 1 or > 4)
            {
                errorCode = "invalidMatchSize";
                errorDescription = "The requested player count must be from 1 to 4.";
                return false;
            }

            if (_matchStarted)
            {
                errorCode = "matchInProgress";
                errorDescription = "A match is already in progress.";
                return false;
            }

            _targetPlayers ??= requestedPlayers;
            if (_targetPlayers != requestedPlayers)
            {
                errorCode = "matchSizeMismatch";
                errorDescription = $"This lobby is configured for {_targetPlayers} players.";
                return false;
            }

            if (_players.Count >= _targetPlayers)
            {
                errorCode = "lobbyFull";
                errorDescription = $"This lobby already has {_targetPlayers} players.";
                return false;
            }

            _players.Add(playerId);
            errorCode = string.Empty;
            errorDescription = string.Empty;
            return true;
        }
    }

    public void MarkMatchStarted()
    {
        lock (_sync)
        {
            _matchStarted = true;
        }
    }

    public void Release(PlayerId playerId)
    {
        lock (_sync)
        {
            _players.Remove(playerId);
            if (_players.Count == 0)
            {
                _targetPlayers = null;
                _matchStarted = false;
            }
        }
    }
}
