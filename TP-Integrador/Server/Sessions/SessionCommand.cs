using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Server.Sessions;

public abstract record SessionCommand(PlayerId PlayerId);

public sealed record JoinSessionCommand(PlayerId PlayerId, long Sequence, HelloPayload Hello)
    : SessionCommand(PlayerId);

public sealed record ClientPayloadCommand(PlayerId PlayerId, long Sequence, IProtocolPayload Payload)
    : SessionCommand(PlayerId);

public sealed record DisconnectSessionCommand(PlayerId PlayerId)
    : SessionCommand(PlayerId);
