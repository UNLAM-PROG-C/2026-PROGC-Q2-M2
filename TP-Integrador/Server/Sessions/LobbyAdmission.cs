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
        errorDescription = "La cantidad de jugadores debe ser de 1 a 4.";
        return false;
      }

      if (_matchStarted)
      {
        errorCode = "matchInProgress";
        errorDescription = "Ya hay una partida en progreso.";
        return false;
      }

      _targetPlayers ??= requestedPlayers;
      if (_targetPlayers != requestedPlayers)
      {
        errorCode = "matchSizeMismatch";
        errorDescription = $"Este lobby esta configurado para {_targetPlayers} jugadores.";
        return false;
      }

      if (_players.Count >= _targetPlayers)
      {
        errorCode = "lobbyFull";
        errorDescription = $"Este lobby ya tiene {_targetPlayers} jugadores.";
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

  public void ReopenLobby()
  {
    lock (_sync)
    {
      _matchStarted = false;
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
