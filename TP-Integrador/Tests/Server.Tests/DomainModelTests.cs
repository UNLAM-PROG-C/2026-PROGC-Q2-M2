using SpaceShooter.GameDomain;
using SpaceShooter.Server.Simulation;

namespace SpaceShooter.Server.Tests;

public sealed class DomainModelTests
{
  private static readonly Hitbox UnitCircle = new(Vector2D.Zero, new CircleShape(1f));

  [Fact]
  public void CollisionDetectorUsesHitboxOffset()
  {
    Player player = CreatePlayer(new Vector2D(0f, 0f), new Vector2D(10f, 0f));
    Enemy enemy = CreateScout(new Vector2D(12f, 0f));

    Assert.True(CollisionDetector.Intersects(player, enemy));
  }

  [Fact]
  public void CollisionManagerReturnsStableEntityOrder()
  {
    GameState state = new();
    state.AddEnemy(CreateScout(Vector2D.Zero));
    state.AddPlayer(CreatePlayer(Vector2D.Zero, Vector2D.Zero));

    CollisionEvent collision = Assert.Single(CollisionManager.Detect(state));

    Assert.True(collision.First.Key < collision.Second.Key);
  }

  [Fact]
  public void RecollectingEffectRefreshesWithoutStackingModifier()
  {
    Player player = CreatePlayer(Vector2D.Zero, Vector2D.Zero);
    PowerUpManager.Collect(player, PowerUpType.SpeedBoost, 2);
    PowerUpEffect initialEffect = player.Effects[PowerUpType.SpeedBoost];
    PowerUpManager.Update(player);

    PowerUpManager.Collect(player, PowerUpType.SpeedBoost, 2);

    Assert.Same(initialEffect, player.Effects[PowerUpType.SpeedBoost]);
    Assert.Equal(15f, player.Speed);
    PowerUpManager.Update(player);
    PowerUpManager.Update(player);
    Assert.Equal(10f, player.Speed);
  }

  [Fact]
  public void ExtraLifeAppliesImmediatelyWithoutTimedEffect()
  {
    Player player = CreatePlayer(Vector2D.Zero, Vector2D.Zero);

    PowerUpManager.Collect(player, PowerUpType.ExtraLife, 10);

    Assert.Equal(4, player.Lives);
    Assert.Empty(player.Effects);
  }

  [Fact]
  public void DestroyedEntityIgnoresLaterDamageInSameTick()
  {
    CombatManager manager = new();
    Enemy enemy = CreateScout(Vector2D.Zero);
    manager.BeginTick();

    bool destroyed = manager.ApplyDamage(enemy, 10);
    bool damagedAgain = manager.ApplyDamage(enemy, 10);

    Assert.True(destroyed);
    Assert.False(damagedAgain);
    Assert.Equal(0, enemy.Health);
  }

  [Fact]
  public void EnemyStateMachineUsesSameStatesForBossAndScout()
  {
    Enemy scout = new Scout(new EnemyId(1), Vector2D.Zero, 5f, UnitCircle, 10);
    Enemy boss = new Boss(new EnemyId(2), Vector2D.Zero, 5f, UnitCircle, 100);

    for (int tick = 0; tick < 30; tick++)
    {
      EnemyStateMachine.Update(scout);
      EnemyStateMachine.Update(boss);
    }

    Assert.Equal(EnemyBehaviorState.InFormation, scout.BehaviorState);
    Assert.Equal(scout.BehaviorState, boss.BehaviorState);
  }

  private static Player CreatePlayer(Vector2D position, Vector2D offset)
  {
    Hitbox hitbox = new(offset, new CircleShape(1f));
    return new Player(new PlayerId(1), position, 10f, hitbox, 3, 100);
  }

  private static Scout CreateScout(Vector2D position)
  {
    return new Scout(new EnemyId(1), position, 5f, UnitCircle, 10);
  }
}
