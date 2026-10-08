namespace SpaceShooter.Protocol;

public enum ProtocolError
{
    InvalidJson,
    MissingField,
    IncompatibleVersion,
    UnknownMessageType,
    InvalidSequence,
    InvalidFrameLength,
    IncompleteFrame,
    UnexpectedMessage,
    InvalidPayload,
}

public sealed class ProtocolException : Exception
{
    public ProtocolException(ProtocolError error, string message)
        : base(message)
    {
        Error = error;
    }

    public ProtocolException(ProtocolError error, string message, Exception innerException)
        : base(message, innerException)
    {
        Error = error;
    }

    public ProtocolError Error { get; }
}
