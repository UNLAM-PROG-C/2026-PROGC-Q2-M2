using System.Text;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.SharedTests;

public sealed class ProtocolSerializerTests
{
    public static TheoryData<ProtocolMessage> Messages => new()
    {
        new(1, new HelloPayload("1.0", "Piloto ñ", ["keyboard"], RequestedPlayers: 4)),
        new(2, new SelectShipPayload("blue")),
        new(3, new SetReadyPayload(true)),
        new(4, new InputStatePayload(0.5f, -1, true, 20)),
        new(5, new PingResponsePayload(9)),
        new(6, new LeavePayload("bye")),
        new(7, new WelcomePayload(new PlayerId(1), 1, 4, 1)),
        new(8, new LobbyStatePayload([new(new PlayerId(1), "One", "blue", true)], 3, RequiredPlayers: 4)),
        new(9, new MatchStartedPayload(100, 42, 1)),
        new(10, new WorldSnapshotPayload(
            12,
            [new(new PlayerId(1), new Vector2D(10, 20), PlayerStatus.Active, 3, 1, 100, [PowerUpKind.Shield])],
            [new(new EnemyId(1), EnemyKind.Scout, new Vector2D(30, 40), EnemyStatus.Active)],
            [new(new ProjectileId(1), new Vector2D(10, 15), ProjectileStatus.Active)],
            [new(new PowerUpId(1), PowerUpKind.Shield, new Vector2D(50, 60), PowerUpStatus.Available)],
            1,
            200,
            BossPhase: 2)),
        new(11, new GameEventPayload("waveStarted", "Wave 1")),
        new(12, new MatchEndedPayload(
            true,
            [new(new PlayerId(1), "One", 100)],
            [new(new PlayerId(2), "Record", 500)])),
        new(13, new PingPayload(9, 123456)),
        new(14, new ErrorPayload("invalid", "Invalid request")),
        new(15, new ServerShutdownPayload("maintenance")),
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void EveryMessageRoundTripsWithStableJson(ProtocolMessage original)
    {
        var serialized = ProtocolSerializer.Serialize(original);

        var deserialized = ProtocolSerializer.Deserialize(serialized);
        var serializedAgain = ProtocolSerializer.Serialize(deserialized);

        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Sequence, deserialized.Sequence);
        Assert.Equal(serialized, serializedAgain);
    }

    [Fact]
    public void UnicodePlayerNameIsPreserved()
    {
        var message = new ProtocolMessage(1, new HelloPayload("1.0", "Nébula 🚀"));

        var result = ProtocolSerializer.Deserialize(ProtocolSerializer.Serialize(message));

        var hello = Assert.IsType<HelloPayload>(result.Payload);
        Assert.Equal("Nébula 🚀", hello.PlayerName);
    }

    [Theory]
    [InlineData("{\"type\":\"hello\",\"sequence\":1,\"payload\":{}}", ProtocolError.MissingField)]
    [InlineData("{\"version\":2,\"type\":\"hello\",\"sequence\":1,\"payload\":{}}", ProtocolError.IncompatibleVersion)]
    [InlineData("{\"version\":1,\"type\":\"unknown\",\"sequence\":1,\"payload\":{}}", ProtocolError.UnknownMessageType)]
    [InlineData("{\"version\":1,\"type\":\"hello\",\"sequence\":-1,\"payload\":{}}", ProtocolError.InvalidSequence)]
    public void InvalidEnvelopeIsRejected(string json, ProtocolError expectedError)
    {
        var exception = Assert.Throws<ProtocolException>(() =>
            ProtocolSerializer.Deserialize(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(expectedError, exception.Error);
    }

    [Fact]
    public void MissingRequiredPayloadFieldIsRejected()
    {
        const string json = """
            {"version":1,"type":"hello","sequence":1,"payload":{"clientVersion":"1.0"}}
            """;

        Assert.Throws<ProtocolException>(() => ProtocolSerializer.Deserialize(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void UnsupportedRequestedPlayerCountIsRejected(int requestedPlayers)
    {
        var message = new ProtocolMessage(
            1,
            new HelloPayload("1.0", "Pilot", RequestedPlayers: requestedPlayers));

        var exception = Assert.Throws<ProtocolException>(() =>
            ProtocolSerializer.Deserialize(ProtocolSerializer.Serialize(message)));

        Assert.Equal(ProtocolError.InvalidPayload, exception.Error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RequestedPlayerCountsFromOneToFourAreAccepted(int requestedPlayers)
    {
        var message = new ProtocolMessage(
            1,
            new HelloPayload("1.0", "Pilot", RequestedPlayers: requestedPlayers));

        var result = ProtocolSerializer.Deserialize(ProtocolSerializer.Serialize(message));

        Assert.Equal(requestedPlayers, Assert.IsType<HelloPayload>(result.Payload).RequestedPlayers);
    }
}
