using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

public sealed class PlayerState
{
    internal PlayerState(PlayerId id, Vector2D position, int lives, int health)
    {
        Id = id;
        Position = position;
        SpawnPosition = position;
        Lives = lives;
        Health = health;
    }

    public PlayerId Id { get; }

    public Vector2D Position { get; internal set; }

    public Vector2D SpawnPosition { get; }

    public int Lives { get; internal set; }

    public int Health { get; internal set; }

    public int Score { get; internal set; }

    public PlayerStatus Status { get; internal set; } = PlayerStatus.Active;

    public int Shield { get; internal set; }

    public int RapidFireTicks { get; internal set; }

    public int MultiShotTicks { get; internal set; }

    public int ShieldTicks { get; internal set; }

    internal int FireCooldownTicks { get; set; }

    internal int RespawnTicks { get; set; }

    internal int InvulnerabilityTicks { get; set; }
}

public sealed class EnemyState
{
    internal EnemyState(EnemyId id, EnemyKind kind, Vector2D position, int health)
    {
        Id = id;
        Kind = kind;
        Position = position;
        Health = health;
    }

    public EnemyId Id { get; }

    public EnemyKind Kind { get; }

    public Vector2D Position { get; internal set; }

    public int Health { get; internal set; }

    public EnemyStatus Status { get; internal set; } = EnemyStatus.Active;

    internal float MovementPhase { get; set; }

    internal int AttackCooldownTicks { get; set; }
}

public sealed class ProjectileState
{
    internal ProjectileState(ProjectileId id, PlayerId ownerId, Vector2D position, Vector2D velocity, int damage)
    {
        Id = id;
        OwnerId = ownerId;
        Position = position;
        Velocity = velocity;
        Damage = damage;
    }

    public ProjectileId Id { get; }

    public PlayerId OwnerId { get; }

    public Vector2D Position { get; internal set; }

    public Vector2D Velocity { get; }

    public int Damage { get; }

    public ProjectileStatus Status { get; internal set; } = ProjectileStatus.Active;
}

public sealed class PowerUpState
{
    internal PowerUpState(PowerUpId id, PowerUpKind kind, Vector2D position)
    {
        Id = id;
        Kind = kind;
        Position = position;
    }

    public PowerUpId Id { get; }

    public PowerUpKind Kind { get; }

    public Vector2D Position { get; internal set; }

    public PowerUpStatus Status { get; internal set; } = PowerUpStatus.Available;
}
