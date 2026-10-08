namespace SpaceShooter.GameDomain;

public readonly record struct Vector2D(float X, float Y)
{
    public static Vector2D Zero => new(0, 0);

    public float LengthSquared => (X * X) + (Y * Y);

    public Vector2D NormalizedOrZero()
    {
        var lengthSquared = LengthSquared;
        if (lengthSquared <= 0)
        {
            return Zero;
        }

        var inverseLength = 1f / MathF.Sqrt(lengthSquared);
        return new Vector2D(X * inverseLength, Y * inverseLength);
    }

    public static Vector2D operator +(Vector2D left, Vector2D right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static Vector2D operator *(Vector2D vector, float scalar) =>
        new(vector.X * scalar, vector.Y * scalar);
}
