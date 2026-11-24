namespace SpaceShooter.Server.Simulation;

internal static class LevelConfigValidator
{
  public static void Validate(IReadOnlyList<LevelConfig> levels)
  {
    ArgumentNullException.ThrowIfNull(levels);
    if (levels.Count == 0)
    {
      throw new ArgumentException("The campaign must contain at least one level.", nameof(levels));
    }

    for (int index = 0; index < levels.Count; index++)
    {
      ValidateLevel(levels[index], index + 1);
    }
  }

  private static void ValidateLevel(LevelConfig level, int expectedNumber)
  {
    if (level.Number != expectedNumber || level.EnemyGroups.Count == 0)
    {
      throw new ArgumentException("Levels must be sequential and contain enemies.", nameof(level));
    }

    if (level.CompletionBonus < 0 || level.AttackInterval <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(nameof(level), "Level values are outside their valid range.");
    }

    if (level.EnemyGroups.Any(IsInvalidGroup))
    {
      throw new ArgumentOutOfRangeException(nameof(level), "Enemy group values are outside their valid range.");
    }
  }

  private static bool IsInvalidGroup(EnemyGroupConfig group)
  {
    return group.Count <= 0 || group.Health <= 0 || group.Speed < 0f;
  }
}
