using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

public enum FormationType
{
  Grid,
  VShape,
  Boss,
}

public sealed record EnemyGroupConfig(
    EnemyKind Kind,
    int Count,
    int Health,
    float Speed);

public sealed record BossPhaseConfig(
    float HealthThreshold,
    float SpeedMultiplier,
    TimeSpan AttackInterval,
    int AttackDamage);

public sealed record LevelConfig(
    int Number,
    IReadOnlyList<EnemyGroupConfig> EnemyGroups,
    FormationType Formation,
    float FormationTop,
    float HorizontalSpacing,
    TimeSpan AttackInterval,
    IReadOnlyList<PowerUpType> AvailablePowerUps,
    int CompletionBonus,
    IReadOnlyList<BossPhaseConfig> BossPhases);
