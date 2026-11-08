using Godot;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

/// <summary>
/// Renders snapshots without owning networking or simulation state.
/// </summary>
internal static class GameRenderer
{
    private const float EnemySize = 16f;
    private const float BossSize = 42f;
    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.09f);

    public static void Draw(
        Node2D canvas,
        SnapshotBuffer snapshots,
        PlayerId? localPlayerId,
        bool playing)
    {
        DrawBackground(canvas);
        if (!playing || snapshots.Current is null)
        {
            return;
        }

        float amount = snapshots.GetInterpolationAmount(Time.GetTicksMsec());
        DrawPlayers(canvas, snapshots, localPlayerId, amount);
        DrawEnemies(canvas, snapshots, amount);
        DrawProjectiles(canvas, snapshots, amount);
        DrawPowerUps(canvas, snapshots.Current);
    }

    private static void DrawPlayers(
        Node2D canvas,
        SnapshotBuffer snapshots,
        PlayerId? localPlayerId,
        float amount)
    {
        IEnumerable<PlayerSnapshot> players = snapshots.Current!.Players
            .Where(IsVisiblePlayer);
        foreach (PlayerSnapshot player in players)
        {
            DrawPlayer(canvas, snapshots, player, localPlayerId, amount);
        }
    }

    private static void DrawPlayer(
        Node2D canvas,
        SnapshotBuffer snapshots,
        PlayerSnapshot player,
        PlayerId? localPlayerId,
        float amount)
    {
        Vector2 position = ToGodot(snapshots.Interpolate(player, amount));
        Rect2 destination = CenteredRect(position, new Vector2(48f, 36f));
        canvas.DrawTextureRect(AssetCatalog.GetPlayer(player.Id), destination, false);
        DrawPlayerIndicators(canvas, player, localPlayerId, position);
    }

    private static void DrawPlayerIndicators(
        Node2D canvas,
        PlayerSnapshot player,
        PlayerId? localPlayerId,
        Vector2 position)
    {
        if (localPlayerId == player.Id)
        {
            canvas.DrawArc(position, 22f, 0f, Mathf.Tau, 24, Colors.White, 2f);
        }

        if (player.Status == PlayerStatus.Invulnerable || player.Shield)
        {
            canvas.DrawArc(position, 28f, 0f, Mathf.Tau, 24, new Color(0.3f, 0.9f, 1f), 3f);
        }
    }

    private static void DrawEnemies(Node2D canvas, SnapshotBuffer snapshots, float amount)
    {
        IEnumerable<EnemySnapshot> enemies = snapshots.Current!.Enemies
            .Where(enemy => enemy.Status == EnemyStatus.Active);
        foreach (EnemySnapshot enemy in enemies)
        {
            Vector2 position = ToGodot(snapshots.Interpolate(enemy, amount));
            float size = enemy.Kind == EnemyKind.Boss ? BossSize : EnemySize;
            Vector2 dimensions = new(size * 2f, size * 2f);
            canvas.DrawTextureRect(AssetCatalog.GetEnemy(enemy.Kind), CenteredRect(position, dimensions), false);
        }
    }

    private static void DrawProjectiles(Node2D canvas, SnapshotBuffer snapshots, float amount)
    {
        IEnumerable<ProjectileSnapshot> projectiles = snapshots.Current!.Projectiles
            .Where(projectile => projectile.Status == ProjectileStatus.Active);
        foreach (ProjectileSnapshot projectile in projectiles)
        {
            Vector2 position = ToGodot(snapshots.Interpolate(projectile, amount));
            Rect2 destination = CenteredRect(position, new Vector2(8f, 20f));
            canvas.DrawTextureRect(AssetCatalog.Projectile, destination, false);
        }
    }

    private static void DrawPowerUps(Node2D canvas, WorldSnapshotPayload snapshot)
    {
        IEnumerable<PowerUpSnapshot> powerUps = snapshot.PowerUps
            .Where(powerUp => powerUp.Status == PowerUpStatus.Available);
        foreach (PowerUpSnapshot powerUp in powerUps)
        {
            Vector2 position = ToGodot(powerUp.Position);
            Rect2 destination = CenteredRect(position, new Vector2(24f, 24f));
            canvas.DrawTextureRect(AssetCatalog.GetPowerUp(powerUp.Kind), destination, false);
        }
    }

    private static Rect2 CenteredRect(Vector2 center, Vector2 size)
    {
        return new Rect2(center - (size / 2f), size);
    }

    private static void DrawBackground(Node2D canvas)
    {
        canvas.DrawRect(new Rect2(0f, 0f, 1280f, 720f), BackgroundColor);
        canvas.DrawTextureRect(
            AssetCatalog.Background,
            new Rect2(0f, 0f, 1280f, 720f),
            true);
    }

    private static bool IsVisiblePlayer(PlayerSnapshot player)
    {
        return player.Status is PlayerStatus.Active or PlayerStatus.Invulnerable;
    }

    private static Vector2 ToGodot(Vector2D value)
    {
        return new Vector2(value.X, value.Y);
    }
}
