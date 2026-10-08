using SpaceShooter.GameDomain;

namespace SpaceShooter.Protocol;

public sealed record WelcomePayload(
    PlayerId PlayerId,
    int MinimumPlayers,
    int MaximumPlayers,
    int ProtocolVersion) : IProtocolPayload;

public sealed record LobbyParticipant(
    PlayerId PlayerId,
    string PlayerName,
    string ShipId,
    bool Ready);

public sealed record LobbyStatePayload(
    LobbyParticipant[] Participants,
    int? CountdownSeconds,
    int RequiredPlayers = 2) : IProtocolPayload;

public sealed record MatchStartedPayload(
    long MatchId,
    int Seed,
    long InitialTick) : IProtocolPayload;

public sealed record PlayerSnapshot(
    PlayerId Id,
    Vector2D Position,
    PlayerStatus Status,
    int Lives,
    int Shield,
    int Score,
    PowerUpKind[]? ActivePowerUps = null);

public sealed record EnemySnapshot(
    EnemyId Id,
    EnemyKind Kind,
    Vector2D Position,
    EnemyStatus Status);

public sealed record ProjectileSnapshot(
    ProjectileId Id,
    Vector2D Position,
    ProjectileStatus Status);

public sealed record PowerUpSnapshot(
    PowerUpId Id,
    PowerUpKind Kind,
    Vector2D Position,
    PowerUpStatus Status);

public sealed record WorldSnapshotPayload(
    long ServerTick,
    PlayerSnapshot[] Players,
    EnemySnapshot[] Enemies,
    ProjectileSnapshot[] Projectiles,
    PowerUpSnapshot[] PowerUps,
    int Wave,
    int? BossHealth,
    int BossPhase = 0) : IProtocolPayload;

public sealed record GameEventPayload(string EventCode, string? Detail = null) : IProtocolPayload;

public sealed record ScoreEntry(PlayerId PlayerId, string PlayerName, int Score);

public sealed record MatchEndedPayload(
    bool Victory,
    ScoreEntry[] Ranking,
    ScoreEntry[]? Records = null) : IProtocolPayload;

public sealed record PingPayload(long PingId, long ServerTimestampMilliseconds) : IProtocolPayload;

public sealed record ErrorPayload(string Code, string Description) : IProtocolPayload;

public sealed record ServerShutdownPayload(string Reason) : IProtocolPayload;
