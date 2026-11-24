namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Advances every enemy type through the same deterministic behavior states.
/// </summary>
internal static class EnemyStateMachine
{
  private const int AttackDurationTicks = 30;
  private const int AttackPeriodTicks = 120;
  private const int EnteringDurationTicks = 30;
  private const int ReturningDurationTicks = 30;

  public static void Destroy(Enemy enemy)
  {
    enemy.BehaviorState = EnemyBehaviorState.Destroyed;
    enemy.BehaviorTicks = 0;
  }

  public static void Update(Enemy enemy)
  {
    if (enemy.BehaviorState == EnemyBehaviorState.Destroyed)
    {
      return;
    }

    enemy.BehaviorTicks++;
    TryTransition(enemy);
  }

  private static void TryTransition(Enemy enemy)
  {
    switch (enemy.BehaviorState)
    {
      case EnemyBehaviorState.Entering when enemy.BehaviorTicks >= EnteringDurationTicks:
        Transition(enemy, EnemyBehaviorState.InFormation);
        break;
      case EnemyBehaviorState.InFormation when enemy.BehaviorTicks >= AttackPeriodTicks:
        Transition(enemy, EnemyBehaviorState.Attacking);
        break;
      case EnemyBehaviorState.Attacking when enemy.BehaviorTicks >= AttackDurationTicks:
        Transition(enemy, EnemyBehaviorState.Returning);
        break;
      case EnemyBehaviorState.Returning when enemy.BehaviorTicks >= ReturningDurationTicks:
        Transition(enemy, EnemyBehaviorState.InFormation);
        break;
    }
  }

  private static void Transition(Enemy enemy, EnemyBehaviorState state)
  {
    enemy.BehaviorState = state;
    enemy.BehaviorTicks = 0;
  }
}
