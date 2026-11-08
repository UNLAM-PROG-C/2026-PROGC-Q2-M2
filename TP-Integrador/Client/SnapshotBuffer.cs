using Godot;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

/// <summary>
/// Retains two authoritative snapshots for visual interpolation only.
/// </summary>
internal sealed class SnapshotBuffer
{
    private const float SnapshotIntervalMilliseconds = 50f;
    private WorldSnapshotPayload? _previous;

    public WorldSnapshotPayload? Current { get; private set; }

    public ulong ReceivedAt { get; private set; }

    public void Accept(WorldSnapshotPayload snapshot, ulong receivedAt)
    {
        _previous = Current;
        Current = snapshot;
        ReceivedAt = receivedAt;
    }

    public float GetInterpolationAmount(ulong now)
    {
        return Math.Clamp((now - ReceivedAt) / SnapshotIntervalMilliseconds, 0f, 1f);
    }

    public Vector2D Interpolate(PlayerSnapshot current, float amount)
    {
        PlayerSnapshot? previous = _previous?.Players.FirstOrDefault(player => player.Id == current.Id);
        return previous is null ? current.Position : Lerp(previous.Position, current.Position, amount);
    }

    public Vector2D Interpolate(EnemySnapshot current, float amount)
    {
        EnemySnapshot? previous = _previous?.Enemies.FirstOrDefault(enemy => enemy.Id == current.Id);
        return previous is null ? current.Position : Lerp(previous.Position, current.Position, amount);
    }

    public Vector2D Interpolate(ProjectileSnapshot current, float amount)
    {
        ProjectileSnapshot? previous = _previous?.Projectiles
            .FirstOrDefault(projectile => projectile.Id == current.Id);
        return previous is null ? current.Position : Lerp(previous.Position, current.Position, amount);
    }

    private static Vector2D Lerp(Vector2D from, Vector2D to, float amount)
    {
        return new Vector2D(
            Mathf.Lerp(from.X, to.X, amount),
            Mathf.Lerp(from.Y, to.Y, amount));
    }
}
