namespace SpaceShooter.Protocol;

public static class ProtocolVersion
{
    public const int Current = 1;
}

public interface IProtocolPayload;

public sealed record ProtocolMessage
{
    public ProtocolMessage(long sequence, IProtocolPayload payload)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        Sequence = sequence;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public string Type => ProtocolTypes.GetName(Payload.GetType());

    public long Sequence { get; }

    public IProtocolPayload Payload { get; }
}
