namespace SpaceShooter.GameDomain;

public enum PlayerStatus
{
    Active,
    Respawning,
    Invulnerable,
    Eliminated,
}

public enum EnemyStatus
{
    Active,
    Destroyed,
}

public enum ProjectileStatus
{
    Active,
    Consumed,
}

public enum PowerUpStatus
{
    Available,
    Collected,
}

public enum MatchStatus
{
    Waiting,
    Running,
    Won,
    Lost,
}

public enum EnemyKind
{
    Scout,
    Diver,
    Tank,
    Boss,
}

public enum PowerUpKind
{
    RapidFire,
    MultiShot,
    Shield,
}
