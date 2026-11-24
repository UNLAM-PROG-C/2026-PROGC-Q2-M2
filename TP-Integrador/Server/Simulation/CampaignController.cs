using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Owns campaign progression, transitions, bonuses, and wave creation.
/// </summary>
internal sealed class CampaignController(
    SimulationContext context,
    LevelManager levels,
    Action<EnemySpawn> spawnEnemy)
{
  public void Start()
  {
    if (!context.Config.CampaignEnabled || context.State.Wave != 0)
    {
      return;
    }

    context.State.Wave = 1;
    BeginTransition(completionBonus: 0);
  }

  public bool AdvanceTransition()
  {
    if (context.State.WaveTransitionTicksRemaining <= 0)
    {
      return false;
    }

    context.State.WaveTransitionTicksRemaining--;
    CompleteTransitionIfReady();
    return true;
  }

  public void AdvanceOrComplete()
  {
    if (HasActiveOrNoEnemies())
    {
      return;
    }

    if (ShouldCompleteMatch())
    {
      context.State.MatchStatus = MatchStatus.Won;
      return;
    }

    AdvanceWave();
  }

  private bool HasActiveOrNoEnemies()
  {
    return context.State.Enemies.Count == 0 ||
        context.State.Enemies.Values.Any(enemy => enemy.Status == EnemyStatus.Active);
  }

  private bool ShouldCompleteMatch()
  {
    return !context.Config.CampaignEnabled || context.State.Wave >= levels.LevelCount;
  }

  private void AdvanceWave()
  {
    int bonus = levels.GetLevel(context.State.Wave).CompletionBonus;
    AwardBonus(bonus);
    context.State.Wave++;
    BeginTransition(bonus);
  }

  private void AwardBonus(int bonus)
  {
    IEnumerable<Player> players = context.State.Players.Values
        .Where(player => player.Status != PlayerStatus.Eliminated);
    foreach (Player player in players)
    {
      player.Score += bonus;
    }
  }

  private void BeginTransition(int completionBonus)
  {
    ClearTransientEntities();
    context.State.WaveCompletionBonus = completionBonus;
    context.State.WaveTransitionTicksRemaining = TransitionDurationInTicks();
    CompleteTransitionIfReady();
  }

  private int TransitionDurationInTicks()
  {
    return context.Config.WaveTransitionDuration == TimeSpan.Zero
        ? 0
        : context.DurationInTicks(context.Config.WaveTransitionDuration);
  }

  private void CompleteTransitionIfReady()
  {
    if (context.State.WaveTransitionTicksRemaining == 0)
    {
      context.State.WaveCompletionBonus = 0;
      SpawnWave();
    }
  }

  private void SpawnWave()
  {
    IReadOnlyList<EnemySpawn> spawns = levels.CreateSpawns(
        context.State.Wave,
        context.Config.WorldBounds);
    if (spawns.Any(spawn => spawn.Kind == EnemyKind.Boss))
    {
      context.State.BossPhase = 1;
    }

    foreach (EnemySpawn spawn in spawns)
    {
      spawnEnemy(spawn);
    }
  }

  private void ClearTransientEntities()
  {
    RemoveProjectiles();
    RemovePowerUps();
  }

  private void RemoveProjectiles()
  {
    foreach (ProjectileId id in context.State.Projectiles.Keys.ToArray())
    {
      context.State.RemoveProjectile(id);
    }
  }

  private void RemovePowerUps()
  {
    foreach (PowerUpId id in context.State.PowerUps.Keys.ToArray())
    {
      context.State.RemovePowerUp(id);
    }
  }
}
