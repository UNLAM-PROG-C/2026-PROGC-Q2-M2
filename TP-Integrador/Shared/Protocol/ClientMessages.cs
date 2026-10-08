namespace SpaceShooter.Protocol;

public sealed record HelloPayload(
    string ClientVersion,
    string PlayerName,
    string[]? Capabilities = null,
    int RequestedPlayers = 2) : IProtocolPayload;

public sealed record SelectShipPayload(string ShipId) : IProtocolPayload;

public sealed record SetReadyPayload(bool Ready) : IProtocolPayload;

public sealed record InputStatePayload(
    float Horizontal,
    float Vertical,
    bool Fire,
    long LocalTick) : IProtocolPayload;

public sealed record PingResponsePayload(long PingId) : IProtocolPayload;

public sealed record LeavePayload(string? Reason = null) : IProtocolPayload;
