using SpaceShooter.Server.Simulation;
using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Tests;

public sealed class WorldSimulationTests
{
  [Fact]
  public void MovementUsesFixedStepAndNormalizesDiagonalInput()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(50, 50));

    simulation.Step(new Dictionary<PlayerId, PlayerInput>
    {
      [player.Id] = new(new Vector2D(1, 1), Fire: false),
    });

    float expectedDelta = 10 / MathF.Sqrt(2);
    Assert.Equal(50 + expectedDelta, player.Position.X, 4);
    Assert.Equal(50 + expectedDelta, player.Position.Y, 4);
  }

  [Fact]
  public void MovementCannotLeavePlayerBounds()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(95, 95));
    Dictionary<PlayerId, PlayerInput> input = new Dictionary<PlayerId, PlayerInput>
    {
      [player.Id] = new(new Vector2D(1, 1), Fire: false),
    };

    simulation.Step(input);

    Assert.Equal(new Vector2D(95, 95), player.Position);
  }

  [Fact]
  public void HeldFireRespectsConfiguredCadence()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(50, 90));
    Dictionary<PlayerId, PlayerInput> input = new Dictionary<PlayerId, PlayerInput>
    {
      [player.Id] = new(Vector2D.Zero, Fire: true),
    };

    for (int tick = 0; tick < 4; tick++)
    {
      simulation.Step(input);
    }

    Assert.Equal(2, simulation.State.Projectiles.Count);
  }

  [Fact]
  public void ProjectileCollisionDealsDamageAndAwardsScoreOnKill()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(50, 90));
    Enemy enemy = simulation.AddEnemy(EnemyKind.Scout, new Vector2D(50, 70));

    simulation.Step(new Dictionary<PlayerId, PlayerInput>
    {
      [player.Id] = new(Vector2D.Zero, Fire: true),
    });

    Assert.Equal(EnemyStatus.Destroyed, enemy.Status);
    Assert.Equal(0, enemy.Health);
    Assert.Equal(100, player.Score);
    Assert.Empty(simulation.State.Projectiles);
    Assert.Equal(MatchStatus.Won, simulation.State.MatchStatus);
  }

  [Fact]
  public void LethalDamageConsumesLivesAndEventuallyEliminatesPlayer()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(50, 50));

    for (int hit = 0; hit < 3; hit++)
    {
      simulation.DamagePlayer(player.Id, 100);
      for (int tick = 0; tick < 50 && player.Status != PlayerStatus.Active; tick++)
      {
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());
      }
    }

    Assert.Equal(0, player.Lives);
    Assert.Equal(0, player.Health);
    Assert.Equal(PlayerStatus.Eliminated, player.Status);
    Assert.Equal(MatchStatus.Lost, simulation.State.MatchStatus);
  }

  [Fact]
  public void PlayerCollectsOverlappingPowerUp()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(50, 50));
    PowerUp powerUp = simulation.AddPowerUp(PowerUpType.Shield, player.Position);

    simulation.Step(new Dictionary<PlayerId, PlayerInput>());

    Assert.Equal(PowerUpStatus.Collected, powerUp.Status);
    Assert.DoesNotContain(powerUp.Id, simulation.State.PowerUps.Keys);
  }

  [Fact]
  public void RespawnWaitsThenGrantsTemporaryInvulnerability()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(50, 50));

    simulation.DamagePlayer(player.Id, 100);

    Assert.Equal(PlayerStatus.Respawning, player.Status);
    Assert.Equal(2, player.Lives);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    Assert.Equal(PlayerStatus.Respawning, player.Status);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    Assert.Equal(PlayerStatus.Invulnerable, player.Status);

    simulation.DamagePlayer(player.Id, 100);
    Assert.Equal(2, player.Lives);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    Assert.Equal(PlayerStatus.Active, player.Status);
  }

  [Fact]
  public void PowerUpsApplyExpireAndRefreshWithoutStacking()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(50, 50));
    simulation.AddPowerUp(PowerUpType.RapidFire, player.Position);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    Assert.Equal(3, player.Effects[PowerUpType.RapidFire].RemainingTicks);

    simulation.AddPowerUp(PowerUpType.RapidFire, player.Position);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    Assert.Equal(3, player.Effects[PowerUpType.RapidFire].RemainingTicks);

    for (int tick = 0; tick < 3; tick++)
    {
      simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    }

    Assert.DoesNotContain(PowerUpType.RapidFire, player.Effects.Keys);
  }

  [Fact]
  public void MultiShotCreatesThreeProjectilesAndShieldAbsorbsDamage()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(50, 90));
    simulation.AddPowerUp(PowerUpType.MultiShot, player.Position);
    simulation.AddPowerUp(PowerUpType.Shield, player.Position);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());

    simulation.Step(new Dictionary<PlayerId, PlayerInput>
    {
      [player.Id] = new(Vector2D.Zero, Fire: true),
    });
    simulation.DamagePlayer(player.Id, 100);

    Assert.Equal(3, simulation.State.Projectiles.Count);
    Assert.Equal(3, player.Lives);
    Assert.False(player.Shield);
  }

  [Fact]
  public void SpeedBoostChangesMovementAndExtraLifeAppliesImmediately()
  {
    WorldSimulation simulation = CreateSimulation();
    Player player = simulation.AddPlayer(new Vector2D(20, 50));
    simulation.AddPowerUp(PowerUpType.SpeedBoost, player.Position);
    simulation.AddPowerUp(PowerUpType.ExtraLife, player.Position);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());

    simulation.Step(new Dictionary<PlayerId, PlayerInput>
    {
      [player.Id] = new(new Vector2D(1, 0), Fire: false),
    });

    Assert.Equal(4, player.Lives);
    Assert.Equal(35, player.Position.X);
    Assert.Contains(PowerUpType.SpeedBoost, player.Effects.Keys);
  }

  [Fact]
  public void CampaignTransitionFreezesCombatAndClearsTransientEntities()
  {
    SimulationConfig config = CreateConfig() with
    {
      CampaignEnabled = true,
      Levels = CreateFastCampaign(),
      WaveTransitionDuration = TimeSpan.FromMilliseconds(300),
    };
    WorldSimulation simulation = new WorldSimulation(config, new SeededRandomSource(1));
    Player player = simulation.AddPlayer(new Vector2D(50, 90));
    Dictionary<PlayerId, PlayerInput> input = new()
    {
      [player.Id] = new(new Vector2D(1, 0), Fire: true),
    };

    simulation.StartCampaign();

    Assert.Equal(1, simulation.State.Wave);
    Assert.Equal(3, simulation.State.WaveTransitionTicksRemaining);
    Assert.Empty(ActiveEnemies(simulation));

    simulation.Step(input);
    simulation.Step(input);

    Assert.Equal(new Vector2D(50, 90), player.Position);
    Assert.Empty(simulation.State.Projectiles);
    Assert.Empty(ActiveEnemies(simulation));

    simulation.Step(input);

    Assert.NotEmpty(ActiveEnemies(simulation));
    Assert.Equal(0, simulation.State.WaveTransitionTicksRemaining);

    simulation.AddPowerUp(PowerUpType.Shield, new Vector2D(10, 10));
    DestroyActiveEnemies(simulation, player.Id);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());

    Assert.Equal(2, simulation.State.Wave);
    Assert.Equal(3, simulation.State.WaveTransitionTicksRemaining);
    Assert.Equal(config.Levels[0].CompletionBonus, simulation.State.WaveCompletionBonus);
    Assert.Empty(simulation.State.PowerUps);
    Assert.Empty(ActiveEnemies(simulation));
  }

  [Fact]
  public void CampaignAdvancesThroughTenLevelsAndTwoBossPhases()
  {
    SimulationConfig config = CreateConfig() with
    {
      CampaignEnabled = true,
      Levels = CreateFastCampaign(),
      WaveTransitionDuration = TimeSpan.Zero,
      BossHealth = 20,
      ScoutSpeed = 0,
      DiverSpeed = 0,
      TankSpeed = 0,
      BossSpeed = 0,
    };
    WorldSimulation simulation = new WorldSimulation(config, new SeededRandomSource(1));
    Player player = simulation.AddPlayer(new Vector2D(50, 90));
    simulation.StartCampaign();

    Assert.Equal(1, simulation.State.Wave);
    Assert.All(ActiveEnemies(simulation), enemy => Assert.Equal(EnemyKind.Scout, enemy.Kind));
    DestroyActiveEnemies(simulation, player.Id);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());

    for (int expectedLevel = 2; expectedLevel <= 9; expectedLevel++)
    {
      Assert.Equal(expectedLevel, simulation.State.Wave);
      DestroyActiveEnemies(simulation, player.Id);
      simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    }

    Enemy boss = Assert.Single(ActiveEnemies(simulation));
    Assert.Equal(10, simulation.State.Wave);
    Assert.Equal(EnemyKind.Boss, boss.Kind);
    Assert.Equal(1, simulation.State.BossPhase);
    simulation.DamageEnemy(boss.Id, player.Id, 10);
    Assert.Equal(2, simulation.State.BossPhase);
    simulation.DamageEnemy(boss.Id, player.Id, 10);
    simulation.Step(new Dictionary<PlayerId, PlayerInput>());

    Assert.Equal(MatchStatus.Won, simulation.State.MatchStatus);
  }

  [Fact]
  public void BossChangesFromSingleTargetToFasterGroupAttack()
  {
    SimulationConfig config = CreateConfig() with
    {
      BossHealth = 20,
      BossSpeed = 0,
      BossAttackDamage = 50,
      BossPhase1AttackInterval = TimeSpan.FromMilliseconds(200),
      BossPhase2AttackInterval = TimeSpan.FromMilliseconds(100),
    };
    WorldSimulation simulation = new WorldSimulation(config, new SeededRandomSource(1));
    Player first = simulation.AddPlayer(new Vector2D(30, 90));
    Player second = simulation.AddPlayer(new Vector2D(70, 90));
    Enemy boss = simulation.AddEnemy(EnemyKind.Boss, new Vector2D(50, 20));

    for (int tick = 0; tick < 20 && first.Health == 100; tick++)
    {
      simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    }

    Assert.Equal(50, first.Health);
    Assert.Equal(100, second.Health);
    simulation.DamageEnemy(boss.Id, first.Id, 10);
    for (int tick = 0; tick < 20 && second.Health == 100; tick++)
    {
      simulation.Step(new Dictionary<PlayerId, PlayerInput>());
    }

    Assert.Equal(2, simulation.State.BossPhase);
    Assert.Equal(50, second.Health);
  }

  [Fact]
  public void SameSeedAndInputsProduceSameState()
  {
    ReplayResult first = RunReplay(seed: 12345);
    ReplayResult second = RunReplay(seed: 12345);

    Assert.Equal(first.Tick, second.Tick);
    Assert.Equal(first.PlayerPosition, second.PlayerPosition);
    Assert.Equal(first.Score, second.Score);
    Assert.Equal(first.EnemyPositions, second.EnemyPositions);
    Assert.Equal(first.ProjectilePositions, second.ProjectilePositions);
  }

  private static ReplayResult RunReplay(int seed)
  {
    WorldSimulation simulation = CreateSimulation(seed);
    Player player = simulation.AddPlayer(new Vector2D(50, 90));
    simulation.AddRandomEnemy(EnemyKind.Scout);
    simulation.AddRandomEnemy(EnemyKind.Diver);
    simulation.AddRandomEnemy(EnemyKind.Tank);

    PlayerInput[] inputs = new[]
    {
            new PlayerInput(new Vector2D(1, 0), false),
            new PlayerInput(new Vector2D(1, -1), true),
            new PlayerInput(Vector2D.Zero, true),
            new PlayerInput(new Vector2D(-1, 0), false),
        };

    foreach (PlayerInput input in inputs)
    {
      simulation.Step(new Dictionary<PlayerId, PlayerInput> { [player.Id] = input });
    }

    return new ReplayResult(
        simulation.State.Tick,
        player.Position,
        player.Score,
        simulation.State.Enemies.Values.OrderBy(enemy => enemy.Id.Value).Select(enemy => enemy.Position).ToArray(),
        simulation.State.Projectiles.Values.OrderBy(projectile => projectile.Id.Value).Select(projectile => projectile.Position).ToArray());
  }

  private static WorldSimulation CreateSimulation(int seed = 7) =>
      new(CreateConfig(), new SeededRandomSource(seed));

  private static SimulationConfig CreateConfig() => new()
  {
    FixedStep = TimeSpan.FromMilliseconds(100),
    MaxCatchUpSteps = 5,
    WorldBounds = new WorldBounds(0, 0, 100, 100),
    PlayerBounds = new WorldBounds(0, 0, 100, 100),
    PlayerSpeed = 100,
    PlayerRadius = 5,
    PlayerStartingLives = 3,
    PlayerMaxHealth = 100,
    FireCooldown = TimeSpan.FromMilliseconds(300),
    ProjectileSpeed = 100,
    ProjectileRadius = 5,
    ProjectileDamage = 10,
    EnemyRadius = 5,
    EnemyHealth = 10,
    EnemyCollisionDamage = 100,
    EnemyKillScore = 100,
    PowerUpRadius = 5,
    RespawnDelay = TimeSpan.FromMilliseconds(200),
    InvulnerabilityDuration = TimeSpan.FromMilliseconds(200),
    PowerUpDuration = TimeSpan.FromMilliseconds(300),
  };

  private static Enemy[] ActiveEnemies(WorldSimulation simulation) =>
      simulation.State.Enemies.Values.Where(enemy => enemy.Status == EnemyStatus.Active).ToArray();

  private static void DestroyActiveEnemies(WorldSimulation simulation, PlayerId playerId)
  {
    foreach (Enemy enemy in ActiveEnemies(simulation))
    {
      simulation.DamageEnemy(enemy.Id, playerId, int.MaxValue);
    }
  }

  private static LevelConfig[] CreateFastCampaign()
  {
    return CampaignLevels.CreateDefault()
        .Select(level => level with
        {
          EnemyGroups = level.EnemyGroups
                .Select(group => group with
                {
                  Count = group.Kind == EnemyKind.Boss ? 1 : group.Count,
                  Health = group.Kind == EnemyKind.Boss ? 20 : 10,
                  Speed = 0,
                })
                .ToArray(),
        })
        .ToArray();
  }

  private sealed record ReplayResult(
      long Tick,
      Vector2D PlayerPosition,
      int Score,
      IReadOnlyList<Vector2D> EnemyPositions,
      IReadOnlyList<Vector2D> ProjectilePositions);
}
