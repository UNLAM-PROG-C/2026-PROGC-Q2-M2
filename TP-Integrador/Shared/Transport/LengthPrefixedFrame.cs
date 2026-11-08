using System.Buffers.Binary;
using SpaceShooter.Protocol;

namespace SpaceShooter.Transport;

public static class LengthPrefixedFrame
{
  public const int HeaderLength = sizeof(uint);
  public const int MaximumPayloadLength = 64 * 1024;

  public static async ValueTask<byte[]?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(stream);

    byte[] header = new byte[HeaderLength];
    int headerBytesRead = await ReadUntilFullAsync(stream, header, allowCleanEndOfStream: true, cancellationToken);
    if (headerBytesRead == 0)
    {
      return null;
    }

    uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(header);
    if (payloadLength is 0 or > MaximumPayloadLength)
    {
      throw new ProtocolException(
          ProtocolError.InvalidFrameLength,
          $"Frame payload length must be between 1 and {MaximumPayloadLength} bytes.");
    }

    byte[] payload = new byte[payloadLength];
    await ReadUntilFullAsync(stream, payload, allowCleanEndOfStream: false, cancellationToken);
    return payload;
  }

  public static async ValueTask WriteAsync(
      Stream stream,
      ReadOnlyMemory<byte> payload,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(stream);
    if (payload.Length is 0 or > MaximumPayloadLength)
    {
      throw new ProtocolException(
          ProtocolError.InvalidFrameLength,
          $"Frame payload length must be between 1 and {MaximumPayloadLength} bytes.");
    }

    byte[] header = new byte[HeaderLength];
    BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
    await stream.WriteAsync(header, cancellationToken);
    await stream.WriteAsync(payload, cancellationToken);
  }

  private static async ValueTask<int> ReadUntilFullAsync(
      Stream stream,
      Memory<byte> destination,
      bool allowCleanEndOfStream,
      CancellationToken cancellationToken)
  {
    int totalBytesRead = 0;
    while (totalBytesRead < destination.Length)
    {
      int bytesRead = await stream.ReadAsync(destination[totalBytesRead..], cancellationToken);
      if (bytesRead == 0)
      {
        if (allowCleanEndOfStream && totalBytesRead == 0)
        {
          return 0;
        }

        throw new ProtocolException(ProtocolError.IncompleteFrame, "The connection ended before the frame was complete.");
      }

      totalBytesRead += bytesRead;
    }

    return totalBytesRead;
  }
}
