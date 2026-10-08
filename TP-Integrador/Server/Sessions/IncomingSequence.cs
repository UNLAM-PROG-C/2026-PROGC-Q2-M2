namespace SpaceShooter.Server.Sessions;

public sealed class IncomingSequence(long initialSequence)
{
    private long _lastSequence = initialSequence;

    public bool TryAccept(long sequence)
    {
        if (sequence <= _lastSequence)
        {
            return false;
        }

        _lastSequence = sequence;
        return true;
    }
}
