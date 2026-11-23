using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Owns deterministic enemy targeting, firing, and boss phase transitions.
/// </summary>
internal sealed class EnemyAttackManager(
    SimulationContext context,
    LevelManager levels,
    ProjectileManager projectiles)
{
  public void Update(Enemy enemy)
  {
    if (enemy.Kind == EnemyKind.Boss)
    {
      UpdateBossAttack(enemy);
    }
    else if (enemy.Kind == EnemyKind.Tank)
    {
      UpdateTankAttack(enemy);
    }
  }

  public void UpdateBossPhase(Enemy enemy)
  {
    if (enemy.Kind != EnemyKind.Boss)
    {
      return;
    }

    if (context.State.Wave <= 0)
    {
      UpdateLegacyPhase(enemy);
      return;
    }

    ApplyCampaignPhase(enemy);
  }

  public float BossSpeedMultiplier()
  {
    if (context.State.Wave > 0)
    {
      return CurrentBossPhase().SpeedMultiplier;
    }

    return context.State.BossPhase == 2 ? 1.5f : 1f;
  }

  public TimeSpan AttackInterval(EnemyKind kind)
  {
    return kind switch
    {
      EnemyKind.Tank => context.Config.EnemyAttackInterval,
      EnemyKind.Boss => CurrentBossAttackInterval(),
      _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Enemy cannot fire."),
    };
  }

  private void UpdateBossAttack(Enemy boss)
  {
    if (!IsReadyToAttack(boss))
    {
      return;
    }

    Player[] targets = ActivePlayers().ToArray();
    FireAtBossTargets(boss, targets);
    boss.AttackCooldownTicks = context.DurationInTicks(CurrentBossAttackInterval());
  }

  private void FireAtBossTargets(Enemy boss, Player[] targets)
  {
    IEnumerable<Player> selected = context.State.BossPhase == 1
        ? targets.Take(1)
        : targets;
    foreach (Player target in selected)
    {
      projectiles.AddEnemy(boss, target, CurrentBossAttackDamage());
    }
  }

  private void UpdateTankAttack(Enemy tank)
  {
    if (!IsReadyToAttack(tank))
    {
      return;
    }

    Player? target = ActivePlayers().FirstOrDefault();
    if (target is not null)
    {
      projectiles.AddEnemy(tank, target, context.Config.EnemyProjectileDamage);
    }

    tank.AttackCooldownTicks = context.DurationInTicks(context.Config.EnemyAttackInterval);
  }

  private static bool IsReadyToAttack(Enemy enemy)
  {
    return enemy.AttackCooldownTicks <= 0 || --enemy.AttackCooldownTicks <= 0;
  }

  private IEnumerable<Player> ActivePlayers()
  {
    return context.State.Players.Values
        .Where(player => player.Status is PlayerStatus.Active or PlayerStatus.Invulnerable)
        .OrderBy(player => player.Id.Value);
  }

  private void ApplyCampaignPhase(Enemy enemy)
  {
    LevelConfig level = levels.GetLevel(context.State.Wave);
    float healthRatio = (float)enemy.Health / BossHealth(level);
    int phaseIndex = FindPhase(level.BossPhases, healthRatio);
    context.State.BossPhase = phaseIndex + 1;
    int cooldown = context.DurationInTicks(level.BossPhases[phaseIndex].AttackInterval);
    enemy.AttackCooldownTicks = Math.Min(enemy.AttackCooldownTicks, cooldown);
  }

  private static int FindPhase(IReadOnlyList<BossPhaseConfig> phases, float healthRatio)
  {
    int selected = 0;
    for (int index = 0; index < phases.Count; index++)
    {
      selected = healthRatio <= phases[index].HealthThreshold ? index : selected;
    }

    return selected;
  }

  private static int BossHealth(LevelConfig level)
  {
    return level.EnemyGroups.Single(group => group.Kind == EnemyKind.Boss).Health;
  }

  private void UpdateLegacyPhase(Enemy enemy)
  {
    if (enemy.Health > context.Config.BossHealth / 2)
    {
      return;
    }

    context.State.BossPhase = 2;
    int cooldown = context.DurationInTicks(context.Config.BossPhase2AttackInterval);
    enemy.AttackCooldownTicks = Math.Min(enemy.AttackCooldownTicks, cooldown);
  }

  private int CurrentBossAttackDamage()
  {
    return context.State.Wave > 0
        ? CurrentBossPhase().AttackDamage
        : context.Config.BossAttackDamage;
  }

  private TimeSpan CurrentBossAttackInterval()
  {
    if (context.State.Wave > 0)
    {
      return CurrentBossPhase().AttackInterval;
    }

    return context.State.BossPhase == 2
        ? context.Config.BossPhase2AttackInterval
        : context.Config.BossPhase1AttackInterval;
  }

  private BossPhaseConfig CurrentBossPhase()
  {
    LevelConfig level = levels.GetLevel(context.State.Wave);
    int index = Math.Clamp(context.State.BossPhase - 1, 0, level.BossPhases.Count - 1);
    return level.BossPhases[index];
  }
}
