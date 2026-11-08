using Godot;
using SpaceShooter.GameDomain;

namespace SpaceShooter.Client;

/// <summary>
/// Centralizes resource paths so rendering code does not depend on file layout.
/// </summary>
internal static class AssetCatalog
{
    private const string Root = "res://Assets/KenneySpaceShooter";
    private static readonly Texture2D[] PlayerShips =
    [
      LoadTexture($"{Root}/Ships/player-blue.png"),
    LoadTexture($"{Root}/Ships/player-red.png"),
    LoadTexture($"{Root}/Ships/player-green.png"),
    LoadTexture($"{Root}/Ships/player-gold.png"),
  ];

    public static Texture2D Background { get; } = LoadTexture($"{Root}/Background/space.png");

    public static AudioStream LaserSound { get; } = LoadAudio($"{Root}/Audio/laser.ogg");

    public static AudioStream LoseSound { get; } = LoadAudio($"{Root}/Audio/lose.ogg");

    public static Texture2D Projectile { get; } = LoadTexture($"{Root}/projectile.png");

    public static AudioStream ShieldSound { get; } = LoadAudio($"{Root}/Audio/shield-up.ogg");

    public static Texture2D GetEnemy(EnemyKind kind)
    {
        string name = kind switch
        {
            EnemyKind.Scout => "scout",
            EnemyKind.Diver => "diver",
            EnemyKind.Tank => "tank",
            EnemyKind.Boss => "boss",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported enemy kind."),
        };
        return LoadTexture($"{Root}/Enemies/{name}.png");
    }

    public static Texture2D GetPlayer(PlayerId id)
    {
        int index = Math.Abs(id.Value - 1) % PlayerShips.Length;
        return PlayerShips[index];
    }

    public static Texture2D GetPowerUp(PowerUpType type)
    {
        string name = type switch
        {
            PowerUpType.RapidFire => "rapid-fire",
            PowerUpType.MultiShot => "multi-shot",
            PowerUpType.Shield => "shield",
            PowerUpType.SpeedBoost => "speed-boost",
            PowerUpType.ExtraLife => "extra-life",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported power-up type."),
        };
        return LoadTexture($"{Root}/PowerUps/{name}.png");
    }

    private static Texture2D LoadTexture(string path)
    {
        return GD.Load<Texture2D>(path);
    }

    private static AudioStream LoadAudio(string path)
    {
        return GD.Load<AudioStream>(path);
    }
}
