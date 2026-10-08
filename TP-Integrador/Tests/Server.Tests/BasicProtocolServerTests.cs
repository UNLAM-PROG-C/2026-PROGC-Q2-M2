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
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var server = new BasicProtocolServer(IPAddress.Loopback, port: 0, TextWriter.Null);
        var serverTask = server.RunAsync(shutdown.Token);
        var port = await server.Started.WaitAsync(TimeSpan.FromSeconds(1));

        var result = await TestProtocolClient.ConnectAsync(port, shutdown.Token);

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
            using var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
            await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
            var stream = client.GetStream();

            await WriteAsync(stream, new ProtocolMessage(1, new HelloPayload("test", "Pilot")), cancellationToken);

            var welcomeMessage = await ReadAsync(stream, cancellationToken);
            var welcome = Assert.IsType<WelcomePayload>(welcomeMessage.Payload);
            var pingMessage = await ReadAsync(stream, cancellationToken);
            var ping = Assert.IsType<PingPayload>(pingMessage.Payload);

            await WriteAsync(stream, new ProtocolMessage(2, new PingResponsePayload(ping.PingId)), cancellationToken);
            var endOfStream = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken);
            Assert.Null(endOfStream);

            return new HandshakeResult(welcome, ping);
        }

        private static async Task<ProtocolMessage> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var frame = await LengthPrefixedFrame.ReadAsync(stream, cancellationToken);
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
