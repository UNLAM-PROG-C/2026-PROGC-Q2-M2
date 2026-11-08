using System.Buffers.Binary;
using SpaceShooter.Protocol;
using SpaceShooter.Transport;

namespace SpaceShooter.SharedTests;

public sealed class LengthPrefixedFrameTests
{
  [Fact]
  public async Task ReadsHeaderAndPayloadFragmentedByteByByte()
  {
    byte[] framed = await CreateFrameAsync([1, 2, 3, 4, 5]);
    await using FragmentedReadStream stream = new FragmentedReadStream(framed, maximumChunkSize: 1);

    byte[]? payload = await LengthPrefixedFrame.ReadAsync(stream);

    Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, payload);
  }

  [Fact]
  public async Task ReadsTwoConcatenatedFramesSeparately()
  {
    byte[] first = await CreateFrameAsync([1, 2]);
    byte[] second = await CreateFrameAsync([3, 4, 5]);
    await using MemoryStream stream = new MemoryStream([.. first, .. second]);

    byte[]? firstPayload = await LengthPrefixedFrame.ReadAsync(stream);
    byte[]? secondPayload = await LengthPrefixedFrame.ReadAsync(stream);
    byte[]? endOfStream = await LengthPrefixedFrame.ReadAsync(stream);

    Assert.Equal(new byte[] { 1, 2 }, firstPayload);
    Assert.Equal(new byte[] { 3, 4, 5 }, secondPayload);
    Assert.Null(endOfStream);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(LengthPrefixedFrame.MaximumPayloadLength + 1)]
  public async Task RejectsInvalidFrameLength(int payloadLength)
  {
    byte[] header = new byte[LengthPrefixedFrame.HeaderLength];
    BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payloadLength);
    await using MemoryStream stream = new MemoryStream(header);

    ProtocolException exception = await Assert.ThrowsAsync<ProtocolException>(async () =>
        await LengthPrefixedFrame.ReadAsync(stream));

    Assert.Equal(ProtocolError.InvalidFrameLength, exception.Error);
  }

  [Fact]
  public async Task AcceptsMaximumPayloadAndRejectsExcess()
  {
    byte[] maximumPayload = new byte[LengthPrefixedFrame.MaximumPayloadLength];
    await using MemoryStream stream = new MemoryStream();

    await LengthPrefixedFrame.WriteAsync(stream, maximumPayload);
    byte[] oversized = new byte[LengthPrefixedFrame.MaximumPayloadLength + 1];
    ProtocolException exception = await Assert.ThrowsAsync<ProtocolException>(async () =>
        await LengthPrefixedFrame.WriteAsync(stream, oversized));

    Assert.Equal(ProtocolError.InvalidFrameLength, exception.Error);
  }

  [Fact]
  public async Task RejectsDisconnectInMiddleOfFrame()
  {
    byte[] header = new byte[LengthPrefixedFrame.HeaderLength];
    BinaryPrimitives.WriteUInt32BigEndian(header, 10);
    await using MemoryStream stream = new MemoryStream([.. header, 1, 2]);

    ProtocolException exception = await Assert.ThrowsAsync<ProtocolException>(async () =>
        await LengthPrefixedFrame.ReadAsync(stream));

    Assert.Equal(ProtocolError.IncompleteFrame, exception.Error);
  }

  private static async Task<byte[]> CreateFrameAsync(byte[] payload)
  {
    await using MemoryStream stream = new MemoryStream();
    await LengthPrefixedFrame.WriteAsync(stream, payload);
    return stream.ToArray();
  }

  private sealed class FragmentedReadStream(byte[] content, int maximumChunkSize) : MemoryStream(content)
  {
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumChunkSize)], cancellationToken);
  }
}
