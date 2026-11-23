using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Provides the extensible ten-level campaign configured entirely in C#.
/// </summary>
public static class CampaignLevels
{
  private const int BaseBonus = 250;
  private const int BaseHealth = 10;
  private const float BaseSpeed = 55f;

  /// <summary>Creates the immutable default ten-level campaign.</summary>
  /// <returns>The ordered campaign configuration.</returns>
  public static IReadOnlyList<LevelConfig> CreateDefault()
  {
    return
    [
      CreateLevel(1, [Group(EnemyKind.Scout, 3, 1f, 1f)], FormationType.Grid),
      CreateLevel(2, [Group(EnemyKind.Scout, 4, 1f, 1.1f)], FormationType.VShape),
      CreateLevel(3, [Group(EnemyKind.Scout, 3, 1.2f, 1.1f), Group(EnemyKind.Diver, 2, 2f, 1.25f)]),
      CreateLevel(4, [Group(EnemyKind.Diver, 5, 2f, 1.3f)], FormationType.VShape),
      CreateLevel(5, [Group(EnemyKind.Scout, 4, 1.4f, 1.25f), Group(EnemyKind.Tank, 2, 3f, 0.75f)]),
      CreateLevel(6, [Group(EnemyKind.Diver, 4, 2.2f, 1.4f), Group(EnemyKind.Tank, 2, 3.2f, 0.8f)]),
      CreateLevel(7, [Group(EnemyKind.Scout, 4, 1.6f, 1.4f), Group(EnemyKind.Tank, 3, 3.4f, 0.85f)]),
      CreateLevel(8, [Group(EnemyKind.Diver, 5, 2.5f, 1.5f), Group(EnemyKind.Tank, 3, 3.6f, 0.9f)]),
      CreateLevel(9, LevelNineGroups()),
      CreateBossLevel(),
    ];
  }

  private static EnemyGroupConfig[] LevelNineGroups()
  {
    return
    [
      Group(EnemyKind.Scout, 4, 2f, 1.5f),
      Group(EnemyKind.Diver, 4, 3f, 1.6f),
      Group(EnemyKind.Tank, 3, 4f, 1f),
    ];
  }

  private static LevelConfig CreateBossLevel()
  {
    return new LevelConfig(
        10,
        [new EnemyGroupConfig(EnemyKind.Boss, 1, 200, 80f)],
        FormationType.Boss,
        100f,
        0f,
        TimeSpan.FromSeconds(1),
        Enum.GetValues<PowerUpType>(),
        BaseBonus * 10,
        CreateBossPhases());
  }

  private static BossPhaseConfig[] CreateBossPhases()
  {
    return
    [
      new BossPhaseConfig(1f, 1f, TimeSpan.FromSeconds(2), 50),
      new BossPhaseConfig(0.5f, 1.5f, TimeSpan.FromSeconds(1), 50),
    ];
  }

  private static LevelConfig CreateLevel(
      int number,
      IReadOnlyList<EnemyGroupConfig> groups,
      FormationType formation = FormationType.Grid)
  {
    return new LevelConfig(
        number,
        groups,
        formation,
        80f,
        64f,
        TimeSpan.FromSeconds(Math.Max(0.5d, 2.1d - (number * 0.1d))),
        AvailablePowerUps(number),
        BaseBonus * number,
        []);
  }

  private static EnemyGroupConfig Group(
      EnemyKind kind,
      int count,
      float healthMultiplier,
      float speedMultiplier)
  {
    int health = (int)MathF.Round(BaseHealth * healthMultiplier);
    return new EnemyGroupConfig(kind, count, health, BaseSpeed * speedMultiplier);
  }

  private static PowerUpType[] AvailablePowerUps(int level)
  {
    return level switch
    {
      <= 2 => [PowerUpType.Shield],
      <= 4 => [PowerUpType.Shield, PowerUpType.RapidFire],
      <= 6 => [PowerUpType.Shield, PowerUpType.RapidFire, PowerUpType.MultiShot],
      <= 8 => [PowerUpType.Shield, PowerUpType.RapidFire, PowerUpType.MultiShot, PowerUpType.SpeedBoost],
      _ => Enum.GetValues<PowerUpType>(),
    };
  }
}
