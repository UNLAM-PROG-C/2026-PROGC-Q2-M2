using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Owns campaign progression data and produces deterministic formation spawns.
/// </summary>
internal sealed class LevelManager
{
  private const int MaximumColumns = 5;
  private readonly IReadOnlyList<LevelConfig> _levels;

  public LevelManager(IReadOnlyList<LevelConfig> levels)
  {
    LevelConfigValidator.Validate(levels);
    _levels = levels;
  }

  public int LevelCount => _levels.Count;

  public IReadOnlyList<EnemySpawn> CreateSpawns(int levelNumber, WorldBounds bounds)
  {
    LevelConfig level = GetLevel(levelNumber);
    List<EnemySpawn> spawns = [];
    int totalCount = level.EnemyGroups.Sum(group => group.Count);
    int index = 0;
    foreach (EnemyGroupConfig group in level.EnemyGroups)
    {
      AddGroupSpawns(level, group, bounds, totalCount, ref index, spawns);
    }

    return spawns;
  }

  public LevelConfig GetLevel(int levelNumber)
  {
    if (levelNumber <= 0 || levelNumber > _levels.Count)
    {
      throw new ArgumentOutOfRangeException(nameof(levelNumber));
    }

    return _levels[levelNumber - 1];
  }

  private static void AddGroupSpawns(
      LevelConfig level,
      EnemyGroupConfig group,
      WorldBounds bounds,
      int totalCount,
      ref int index,
      List<EnemySpawn> spawns)
  {
    for (int groupIndex = 0; groupIndex < group.Count; groupIndex++, index++)
    {
      Vector2D position = FormationPosition(level, bounds, totalCount, index);
      spawns.Add(new EnemySpawn(group.Kind, position, group.Health, group.Speed));
    }
  }

  private static Vector2D FormationPosition(
      LevelConfig level,
      WorldBounds bounds,
      int totalCount,
      int index)
  {
    if (level.Formation == FormationType.Boss)
    {
      float centerX = (bounds.MinX + bounds.MaxX) / 2f;
      return new Vector2D(centerX, level.FormationTop);
    }

    int columns = Math.Min(totalCount, MaximumColumns);
    int column = index % columns;
    int row = index / columns;
    float centerOffset = column - ((columns - 1) / 2f);
    float x = ((bounds.MinX + bounds.MaxX) / 2f) + (centerOffset * level.HorizontalSpacing);
    float rowOffset = level.Formation == FormationType.VShape
        ? MathF.Abs(centerOffset) * level.HorizontalSpacing * 0.5f
        : row * level.HorizontalSpacing;
    return new Vector2D(x, level.FormationTop + rowOffset);
  }
}
