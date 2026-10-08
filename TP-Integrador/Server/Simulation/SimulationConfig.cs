using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

public sealed record SimulationConfig
{
    public TimeSpan FixedStep { get; init; } = TimeSpan.FromSeconds(1d / 60d);

    public int MaxCatchUpSteps { get; init; } = 5;

    public WorldBounds WorldBounds { get; init; } = new(0, 0, 1280, 720);

    public WorldBounds PlayerBounds { get; init; } = new(0, 480, 1280, 720);

    public float PlayerSpeed { get; init; } = 300;

    public float PlayerRadius { get; init; } = 16;

    public int PlayerStartingLives { get; init; } = 3;

    public int PlayerMaxHealth { get; init; } = 100;

    public TimeSpan FireCooldown { get; init; } = TimeSpan.FromMilliseconds(250);

    public float ProjectileSpeed { get; init; } = 600;

    public float ProjectileRadius { get; init; } = 4;

    public int ProjectileDamage { get; init; } = 10;

    public float EnemyRadius { get; init; } = 16;

    public int EnemyHealth { get; init; } = 10;

    public int EnemyCollisionDamage { get; init; } = 100;

    public int EnemyKillScore { get; init; } = 100;

    public float PowerUpRadius { get; init; } = 12;

    public bool CampaignEnabled { get; init; }

    public int Wave1EnemyCount { get; init; } = 3;

    public int Wave2EnemyCount { get; init; } = 5;

    public int Wave3EnemyCount { get; init; } = 7;

    public int DiverHealth { get; init; } = 20;

    public int TankHealth { get; init; } = 30;

    public int BossHealth { get; init; } = 200;

    public int BossKillScore { get; init; } = 1_000;

    public int WaveBonusScore { get; init; } = 250;

    public float ScoutSpeed { get; init; } = 55;

    public float DiverSpeed { get; init; } = 70;

    public float TankSpeed { get; init; } = 35;

    public float BossSpeed { get; init; } = 80;

    public int BossAttackDamage { get; init; } = 50;

    public TimeSpan BossPhase1AttackInterval { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan BossPhase2AttackInterval { get; init; } = TimeSpan.FromSeconds(1);

    public float PowerUpFallSpeed { get; init; } = 90;

    public TimeSpan RespawnDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan InvulnerabilityDuration { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan PowerUpDuration { get; init; } = TimeSpan.FromSeconds(8);

    public void Validate()
    {
        if (FixedStep <= TimeSpan.Zero || MaxCatchUpSteps <= 0 ||
            PlayerSpeed < 0 || PlayerRadius < 0 || PlayerStartingLives <= 0 || PlayerMaxHealth <= 0 ||
            FireCooldown < TimeSpan.Zero || ProjectileSpeed <= 0 || ProjectileRadius < 0 || ProjectileDamage <= 0 ||
            EnemyRadius < 0 || EnemyHealth <= 0 || EnemyCollisionDamage <= 0 || EnemyKillScore < 0 ||
            PowerUpRadius < 0 || Wave1EnemyCount <= 0 || Wave2EnemyCount <= 0 || Wave3EnemyCount <= 0 ||
            DiverHealth <= 0 || TankHealth <= 0 || BossHealth <= 0 || BossKillScore < 0 || WaveBonusScore < 0 ||
            BossAttackDamage <= 0 || BossPhase1AttackInterval <= TimeSpan.Zero || BossPhase2AttackInterval <= TimeSpan.Zero ||
            ScoutSpeed < 0 || DiverSpeed < 0 || TankSpeed < 0 || BossSpeed < 0 || PowerUpFallSpeed < 0 ||
            RespawnDelay < TimeSpan.Zero || InvulnerabilityDuration < TimeSpan.Zero || PowerUpDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(SimulationConfig), "Simulation values are outside their valid range.");
        }
    }
}
