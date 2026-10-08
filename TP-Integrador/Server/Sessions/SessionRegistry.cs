using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Sessions;

public sealed class SessionRegistry
{
    private readonly Dictionary<PlayerId, ClientSession> _sessions = [];

    public int Count => _sessions.Count;

    public IReadOnlyCollection<ClientSession> Sessions => _sessions.Values;

    public bool TryGet(PlayerId playerId, out ClientSession? session) =>
        _sessions.TryGetValue(playerId, out session);

    internal ClientSession Add(PlayerId playerId, string playerName)
    {
        var session = new ClientSession(playerId, playerName);
        _sessions.Add(playerId, session);
        return session;
    }

    internal ClientSession? Remove(PlayerId playerId)
    {
        if (!_sessions.Remove(playerId, out var session))
        {
            return null;
        }

        session.Status = SessionStatus.Disconnected;
        return session;
    }
}
