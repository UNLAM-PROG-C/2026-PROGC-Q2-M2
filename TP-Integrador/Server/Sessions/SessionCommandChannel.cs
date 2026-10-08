using System.Threading.Channels;

namespace SpaceShooter.Server.Sessions;

public sealed class SessionCommandChannel
{
    private readonly Channel<SessionCommand> _channel;

    public SessionCommandChannel(int capacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _channel = Channel.CreateBounded<SessionCommand>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
        });
    }

    public ChannelReader<SessionCommand> Reader => _channel.Reader;

    public bool TryEnqueue(SessionCommand command) => _channel.Writer.TryWrite(command);

    public void Complete() => _channel.Writer.TryComplete();
}
