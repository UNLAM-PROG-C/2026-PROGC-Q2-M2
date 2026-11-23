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
      AdmissionError? error = ValidateAdmission(requestedPlayers);
      errorCode = error?.Code ?? string.Empty;
      errorDescription = error?.Description ?? string.Empty;
      if (error is not null)
      {
        return false;
      }

      _players.Add(playerId);
      return true;
    }
  }

  private AdmissionError? ValidateAdmission(int requestedPlayers)
  {
    if (requestedPlayers is < 1 or > 4)
    {
      return new AdmissionError(
          "invalidMatchSize",
          "La cantidad de jugadores debe ser de 1 a 4.");
    }

    if (_matchStarted)
    {
      return new AdmissionError("matchInProgress", "Ya hay una partida en progreso.");
    }

    _targetPlayers ??= requestedPlayers;
    return ValidateCapacity(requestedPlayers);
  }

  private AdmissionError? ValidateCapacity(int requestedPlayers)
  {
    if (_targetPlayers != requestedPlayers)
    {
      return new AdmissionError(
          "matchSizeMismatch",
          $"Este lobby esta configurado para {_targetPlayers} jugadores.");
    }

    return _players.Count >= _targetPlayers
        ? new AdmissionError("lobbyFull", $"Este lobby ya tiene {_targetPlayers} jugadores.")
        : null;
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

  private sealed record AdmissionError(string Code, string Description);
}
