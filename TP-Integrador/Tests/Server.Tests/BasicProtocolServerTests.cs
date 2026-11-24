using System.Net;
using System.Net.Sockets;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Networking;
using SpaceShooter.Transport;

namespace SpaceShooter.Server.Tests;

public sealed class BasicProtocolServerTests
{
  [Fact]
  public async Task TestClientCompletesHelloWelcomeAndPingThenServerStopsCleanly()
  {
    using CancellationTokenSource shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using BasicProtocolServer server = new BasicProtocolServer(IPAddress.Loopback, port: 0, TextWriter.Null);
    Task serverTask = server.RunAsync(shutdown.Token);
    int port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));

    HandshakeResult result = await TestProtocolClient.ConnectAsync(port, shutdown.Token);

    Assert.Equal(1, result.Welcome.PlayerId.Value);
    Assert.Equal(1, result.Welcome.MinimumPlayers);
    Assert.Equal(4, result.Welcome.MaximumPlayers);
    Assert.Equal(ProtocolVersion.Current, result.Welcome.ProtocolVersion);
    Assert.True(result.Ping.PingId > 0);

    shutdown.Cancel();
    await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
  }

  private static class TestProtocolClient
  {
    public static async Task<HandshakeResult> ConnectAsync(int port, CancellationToken cancellationToken)
    {
      using TcpClient client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
      await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
      NetworkStream stream = client.GetStream();

      await WriteAsync(stream, new ProtocolMessage(1, new HelloPayload("test", "Pilot")), cancellationToken);

      ProtocolMessage welcomeMessage = await ReadAsync(stream, cancellationToken);
      WelcomePayload welcome = Assert.IsType<WelcomePayload>(welcomeMessage.Payload);
      ProtocolMessage pingMessage = await ReadAsync(stream, cancellationToken);
      PingPayload ping = Assert.IsType<PingPayload>(pingMessage.Payload);

      await WriteAsync(stream, new ProtocolMessage(2, new PingResponsePayload(ping.PingId)), cancellationToken);
      byte[]? endOfStream = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken);
      Assert.Null(endOfStream);

      return new HandshakeResult(welcome, ping);
    }

    private static async Task<ProtocolMessage> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
      byte[]? frame = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken);
      Assert.NotNull(frame);
      return ProtocolSerializer.Deserialize(frame);
    }

    private static ValueTask WriteAsync(
        Stream stream,
        ProtocolMessage message,
        CancellationToken cancellationToken) =>
        LengthPrefixedFrame.WriteAsync(stream, ProtocolSerializer.Serialize(message), cancellationToken);
  }

  private sealed record HandshakeResult(WelcomePayload Welcome, PingPayload Ping);
}
