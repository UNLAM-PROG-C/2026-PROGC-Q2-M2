using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Server.Sessions;

public sealed class ClientSession(PlayerId playerId, string playerName)
{
    public PlayerId PlayerId { get; } = playerId;

    public string PlayerName { get; } = playerName;

    public string ShipId { get; internal set; } = "default";

    public SessionStatus Status { get; internal set; } = SessionStatus.Connected;

    public InputStatePayload? LastInput { get; internal set; }
}
