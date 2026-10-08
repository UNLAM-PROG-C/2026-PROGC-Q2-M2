namespace SpaceShooter.GameDomain;

public readonly record struct WorldBounds
{
    public WorldBounds(float minX, float minY, float maxX, float maxY)
    {
        if (!float.IsFinite(minX) || !float.IsFinite(minY) ||
            !float.IsFinite(maxX) || !float.IsFinite(maxY) ||
            minX >= maxX || minY >= maxY)
        {
            throw new ArgumentOutOfRangeException(nameof(minX), "Bounds must be finite and have positive area.");
        }

        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public float MinX { get; }

    public float MinY { get; }

    public float MaxX { get; }

    public float MaxY { get; }

    public Vector2D Clamp(Vector2D position, float radius = 0) => new(
        Math.Clamp(position.X, MinX + radius, MaxX - radius),
        Math.Clamp(position.Y, MinY + radius, MaxY - radius));

    public bool Contains(Vector2D position) =>
        position.X >= MinX && position.X <= MaxX &&
        position.Y >= MinY && position.Y <= MaxY;
}
