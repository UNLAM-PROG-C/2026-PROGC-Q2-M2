using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

public sealed class WorldState
{
    internal Dictionary<PlayerId, PlayerState> MutablePlayers { get; } = [];

    internal Dictionary<EnemyId, EnemyState> MutableEnemies { get; } = [];

    internal Dictionary<ProjectileId, ProjectileState> MutableProjectiles { get; } = [];

    internal Dictionary<PowerUpId, PowerUpState> MutablePowerUps { get; } = [];

    public IReadOnlyDictionary<PlayerId, PlayerState> Players => MutablePlayers;

    public IReadOnlyDictionary<EnemyId, EnemyState> Enemies => MutableEnemies;

    public IReadOnlyDictionary<ProjectileId, ProjectileState> Projectiles => MutableProjectiles;

    public IReadOnlyDictionary<PowerUpId, PowerUpState> PowerUps => MutablePowerUps;

    public long Tick { get; internal set; }

    public int Wave { get; internal set; }

    public int BossPhase { get; internal set; }

    public MatchStatus MatchStatus { get; internal set; } = MatchStatus.Waiting;
}
