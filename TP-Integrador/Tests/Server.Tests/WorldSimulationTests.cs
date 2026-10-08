using SpaceShooter.Server.Simulation;
using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Tests;

public sealed class WorldSimulationTests
{
    [Fact]
    public void MovementUsesFixedStepAndNormalizesDiagonalInput()
    {
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(50, 50));

        simulation.Step(new Dictionary<PlayerId, PlayerInput>
        {
            [player.Id] = new(new Vector2D(1, 1), Fire: false),
        });

        var expectedDelta = 10 / MathF.Sqrt(2);
        Assert.Equal(50 + expectedDelta, player.Position.X, 4);
        Assert.Equal(50 + expectedDelta, player.Position.Y, 4);
    }

    [Fact]
    public void MovementCannotLeavePlayerBounds()
    {
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(95, 95));
        var input = new Dictionary<PlayerId, PlayerInput>
        {
            [player.Id] = new(new Vector2D(1, 1), Fire: false),
        };

        simulation.Step(input);

        Assert.Equal(new Vector2D(95, 95), player.Position);
    }

    [Fact]
    public void HeldFireRespectsConfiguredCadence()
    {
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(50, 90));
        var input = new Dictionary<PlayerId, PlayerInput>
        {
            [player.Id] = new(Vector2D.Zero, Fire: true),
        };

        for (var tick = 0; tick < 4; tick++)
        {
            simulation.Step(input);
        }

        Assert.Equal(2, simulation.State.Projectiles.Count);
    }

    [Fact]
    public void ProjectileCollisionDealsDamageAndAwardsScoreOnKill()
    {
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(50, 90));
        var enemy = simulation.AddEnemy(EnemyKind.Scout, new Vector2D(50, 70));

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
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(50, 50));

        for (var hit = 0; hit < 3; hit++)
        {
            simulation.DamagePlayer(player.Id, 100);
            for (var tick = 0; tick < 50 && player.Status != PlayerStatus.Active; tick++)
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
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(50, 50));
        var powerUp = simulation.AddPowerUp(PowerUpKind.Shield, player.Position);

        simulation.Step(new Dictionary<PlayerId, PlayerInput>());

        Assert.Equal(PowerUpStatus.Collected, powerUp.Status);
    }

    [Fact]
    public void RespawnWaitsThenGrantsTemporaryInvulnerability()
    {
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(50, 50));

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
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(50, 50));
        simulation.AddPowerUp(PowerUpKind.RapidFire, player.Position);
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());
        Assert.Equal(3, player.RapidFireTicks);

        simulation.AddPowerUp(PowerUpKind.RapidFire, player.Position);
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());
        Assert.Equal(3, player.RapidFireTicks);

        for (var tick = 0; tick < 3; tick++)
        {
            simulation.Step(new Dictionary<PlayerId, PlayerInput>());
        }

        Assert.Equal(0, player.RapidFireTicks);
    }

    [Fact]
    public void MultiShotCreatesThreeProjectilesAndShieldAbsorbsDamage()
    {
        var simulation = CreateSimulation();
        var player = simulation.AddPlayer(new Vector2D(50, 90));
        simulation.AddPowerUp(PowerUpKind.MultiShot, player.Position);
        simulation.AddPowerUp(PowerUpKind.Shield, player.Position);
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());

        simulation.Step(new Dictionary<PlayerId, PlayerInput>
        {
            [player.Id] = new(Vector2D.Zero, Fire: true),
        });
        simulation.DamagePlayer(player.Id, 100);

        Assert.Equal(3, simulation.State.Projectiles.Count);
        Assert.Equal(3, player.Lives);
        Assert.Equal(0, player.Shield);
    }

    [Fact]
    public void CampaignAdvancesThroughThreeFormationsAndTwoBossPhases()
    {
        var config = CreateConfig() with
        {
            CampaignEnabled = true,
            Wave1EnemyCount = 3,
            Wave2EnemyCount = 3,
            Wave3EnemyCount = 3,
            BossHealth = 20,
            ScoutSpeed = 0,
            DiverSpeed = 0,
            TankSpeed = 0,
            BossSpeed = 0,
        };
        var simulation = new WorldSimulation(config, new SeededRandomSource(1));
        var player = simulation.AddPlayer(new Vector2D(50, 90));
        simulation.StartCampaign();

        Assert.Equal(1, simulation.State.Wave);
        Assert.All(ActiveEnemies(simulation), enemy => Assert.Equal(EnemyKind.Scout, enemy.Kind));
        DestroyActiveEnemies(simulation, player.Id);
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());

        Assert.Equal(2, simulation.State.Wave);
        Assert.Contains(ActiveEnemies(simulation), enemy => enemy.Kind == EnemyKind.Diver);
        DestroyActiveEnemies(simulation, player.Id);
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());

        Assert.Equal(3, simulation.State.Wave);
        Assert.Contains(ActiveEnemies(simulation), enemy => enemy.Kind == EnemyKind.Tank);
        DestroyActiveEnemies(simulation, player.Id);
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());

        var boss = Assert.Single(ActiveEnemies(simulation));
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
        var config = CreateConfig() with
        {
            BossHealth = 20,
            BossSpeed = 0,
            BossAttackDamage = 50,
            BossPhase1AttackInterval = TimeSpan.FromMilliseconds(200),
            BossPhase2AttackInterval = TimeSpan.FromMilliseconds(100),
        };
        var simulation = new WorldSimulation(config, new SeededRandomSource(1));
        var first = simulation.AddPlayer(new Vector2D(30, 90));
        var second = simulation.AddPlayer(new Vector2D(70, 90));
        var boss = simulation.AddEnemy(EnemyKind.Boss, new Vector2D(50, 20));

        simulation.Step(new Dictionary<PlayerId, PlayerInput>());
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());

        Assert.Equal(50, first.Health);
        Assert.Equal(100, second.Health);
        simulation.DamageEnemy(boss.Id, first.Id, 10);
        simulation.Step(new Dictionary<PlayerId, PlayerInput>());

        Assert.Equal(2, simulation.State.BossPhase);
        Assert.Equal(PlayerStatus.Respawning, first.Status);
        Assert.Equal(50, second.Health);
    }

    [Fact]
    public void SameSeedAndInputsProduceSameState()
    {
        var first = RunReplay(seed: 12345);
        var second = RunReplay(seed: 12345);

        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.PlayerPosition, second.PlayerPosition);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.EnemyPositions, second.EnemyPositions);
        Assert.Equal(first.ProjectilePositions, second.ProjectilePositions);
    }

    private static ReplayResult RunReplay(int seed)
    {
        var simulation = CreateSimulation(seed);
        var player = simulation.AddPlayer(new Vector2D(50, 90));
        simulation.AddRandomEnemy(EnemyKind.Scout);
        simulation.AddRandomEnemy(EnemyKind.Diver);
        simulation.AddRandomEnemy(EnemyKind.Tank);

        var inputs = new[]
        {
            new PlayerInput(new Vector2D(1, 0), false),
            new PlayerInput(new Vector2D(1, -1), true),
            new PlayerInput(Vector2D.Zero, true),
            new PlayerInput(new Vector2D(-1, 0), false),
        };

        foreach (var input in inputs)
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

    private static EnemyState[] ActiveEnemies(WorldSimulation simulation) =>
        simulation.State.Enemies.Values.Where(enemy => enemy.Status == EnemyStatus.Active).ToArray();

    private static void DestroyActiveEnemies(WorldSimulation simulation, PlayerId playerId)
    {
        foreach (var enemy in ActiveEnemies(simulation))
        {
            simulation.DamageEnemy(enemy.Id, playerId, int.MaxValue);
        }
    }

    private sealed record ReplayResult(
        long Tick,
        Vector2D PlayerPosition,
        int Score,
        IReadOnlyList<Vector2D> EnemyPositions,
        IReadOnlyList<Vector2D> ProjectilePositions);
}
