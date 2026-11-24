using SpaceShooter.GameDomain;
using SpaceShooter.Server.Simulation;

namespace SpaceShooter.Server.Tests;

public sealed class GameplayConfigurationTests
{
  [Fact]
  public void DefaultRespawnDelayIsOneSecond()
  {
    SimulationConfig config = new();

    Assert.Equal(TimeSpan.FromSeconds(1), config.RespawnDelay);
  }

  [Fact]
  public void BossUsesConfiguredRectangularHitbox()
  {
    SimulationConfig config = new() { BossHitboxWidth = 120, BossHitboxHeight = 80 };
    WorldSimulation simulation = new(config, new SeededRandomSource(1));

    Enemy boss = simulation.AddEnemy(EnemyKind.Boss, new Vector2D(640, 120));

    RectangleShape shape = Assert.IsType<RectangleShape>(boss.Hitbox.Shape);
    Assert.Equal(120, shape.Width);
    Assert.Equal(80, shape.Height);
  }

  [Fact]
  public void ZeroDropChancePreventsPowerUps()
  {
    WorldSimulation simulation = CreateSimulation(dropChance: 0);
    Player player = simulation.AddPlayer(new Vector2D(100, 600));
    Enemy enemy = simulation.AddEnemy(EnemyKind.Scout, new Vector2D(100, 100));

    simulation.DamageEnemy(enemy.Id, player.Id, int.MaxValue);

    Assert.Empty(simulation.State.PowerUps);
  }

  [Fact]
  public void CertainDropChanceCreatesPowerUp()
  {
    WorldSimulation simulation = CreateSimulation(dropChance: 1);
    Player player = simulation.AddPlayer(new Vector2D(100, 600));
    Enemy enemy = simulation.AddEnemy(EnemyKind.Scout, new Vector2D(100, 100));

    simulation.DamageEnemy(enemy.Id, player.Id, int.MaxValue);

    Assert.Single(simulation.State.PowerUps);
  }

  private static WorldSimulation CreateSimulation(float dropChance)
  {
    SimulationConfig config = new() { PowerUpDropChance = dropChance };
    return new WorldSimulation(config, new SeededRandomSource(1));
  }
}
