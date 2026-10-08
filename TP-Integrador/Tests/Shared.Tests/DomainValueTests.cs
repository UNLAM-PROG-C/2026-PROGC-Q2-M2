using SpaceShooter.GameDomain;

namespace SpaceShooter.SharedTests;

public sealed class DomainValueTests
{
    [Fact]
    public void NormalizedVectorHasUnitLength()
    {
        var normalized = new Vector2D(3, 4).NormalizedOrZero();

        Assert.Equal(0.6f, normalized.X, 5);
        Assert.Equal(0.8f, normalized.Y, 5);
    }

    [Fact]
    public void BoundsClampPositionAndEntityRadius()
    {
        var bounds = new WorldBounds(0, 10, 100, 80);

        var clamped = bounds.Clamp(new Vector2D(-20, 100), radius: 5);

        Assert.Equal(new Vector2D(5, 75), clamped);
    }
}
