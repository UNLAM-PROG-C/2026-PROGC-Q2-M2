namespace SpaceShooter.Server.Simulation;

public interface IRandomSource
{
    int NextInt(int minimumInclusive, int maximumExclusive);
}

public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private uint _state = unchecked((uint)seed) is 0 ? 0x9E3779B9u : unchecked((uint)seed);

    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minimumInclusive, maximumExclusive);

        var value = NextUInt32();
        var range = (uint)(maximumExclusive - minimumInclusive);
        return minimumInclusive + (int)(value % range);
    }

    private uint NextUInt32()
    {
        var value = _state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _state = value;
        return value;
    }
}
