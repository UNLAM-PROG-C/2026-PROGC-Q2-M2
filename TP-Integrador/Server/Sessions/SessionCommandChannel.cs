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

  /// <summary>
  /// Waits asynchronously for capacity so lifecycle commands are not silently lost.
  /// </summary>
  /// <param name="command">The validated command produced by a connection.</param>
  /// <param name="cancellationToken">Cancels the wait during server shutdown.</param>
  /// <returns>A value task that completes after the command is accepted.</returns>
  public ValueTask EnqueueAsync(SessionCommand command, CancellationToken cancellationToken)
  {
    return _channel.Writer.WriteAsync(command, cancellationToken);
  }

  public void Complete() => _channel.Writer.TryComplete();
}
