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
      [new(new PlayerId(1), new Vector2D(10, 20), PlayerStatus.Active, 3, true, 100, [PowerUpType.Shield])],
            [new(new EnemyId(1), EnemyKind.Scout, new Vector2D(30, 40), EnemyStatus.Active)],
            [new(new ProjectileId(1), new Vector2D(10, 15), ProjectileStatus.Active)],
            [new(new PowerUpId(1), PowerUpType.Shield, new Vector2D(50, 60), PowerUpStatus.Available)],
            1,
            200,
            BossPhase: 2,
            WaveTransitionRemainingMilliseconds: 2_500,
            WaveCompletionBonus: 750)),
        new(11, new GameEventPayload("waveStarted", "Wave 1")),
        new(12, new MatchEndedPayload(
            true,
            [new(new PlayerId(1), "One", 100)],
            [new(new PlayerId(2), "Record", 500)])),
        new(13, new PingPayload(9, 123456)),
        new(14, new ErrorPayload("invalid", "Invalid request")),
        new(15, new ServerShutdownPayload("maintenance")),
        new(16, new SetPausePayload(true)),
        new(17, new PauseStatePayload(true, new PlayerId(1), 42)),
    };

  [Theory]
  [MemberData(nameof(Messages))]
  public void EveryMessageRoundTripsWithStableJson(ProtocolMessage original)
  {
    byte[] serialized = ProtocolSerializer.Serialize(original);

    ProtocolMessage deserialized = ProtocolSerializer.Deserialize(serialized);
    byte[] serializedAgain = ProtocolSerializer.Serialize(deserialized);

    Assert.Equal(original.Type, deserialized.Type);
    Assert.Equal(original.Sequence, deserialized.Sequence);
    Assert.Equal(serialized, serializedAgain);
  }

  [Fact]
  public void UnicodePlayerNameIsPreserved()
  {
    ProtocolMessage message = new ProtocolMessage(1, new HelloPayload("1.0", "Nébula 🚀"));

    ProtocolMessage result = ProtocolSerializer.Deserialize(ProtocolSerializer.Serialize(message));

    HelloPayload hello = Assert.IsType<HelloPayload>(result.Payload);
    Assert.Equal("Nébula 🚀", hello.PlayerName);
  }

  [Theory]
  [InlineData("{\"type\":\"hello\",\"sequence\":1,\"payload\":{}}", ProtocolError.MissingField)]
  [InlineData("{\"version\":2,\"type\":\"hello\",\"sequence\":1,\"payload\":{}}", ProtocolError.IncompatibleVersion)]
  [InlineData("{\"version\":1,\"type\":\"unknown\",\"sequence\":1,\"payload\":{}}", ProtocolError.UnknownMessageType)]
  [InlineData("{\"version\":1,\"type\":\"hello\",\"sequence\":-1,\"payload\":{}}", ProtocolError.InvalidSequence)]
  public void InvalidEnvelopeIsRejected(string json, ProtocolError expectedError)
  {
    ProtocolException exception = Assert.Throws<ProtocolException>(() =>
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
    ProtocolMessage message = new ProtocolMessage(
        1,
        new HelloPayload("1.0", "Pilot", RequestedPlayers: requestedPlayers));

    ProtocolException exception = Assert.Throws<ProtocolException>(() =>
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
    ProtocolMessage message = new ProtocolMessage(
        1,
        new HelloPayload("1.0", "Pilot", RequestedPlayers: requestedPlayers));

    ProtocolMessage result = ProtocolSerializer.Deserialize(ProtocolSerializer.Serialize(message));

    Assert.Equal(requestedPlayers, Assert.IsType<HelloPayload>(result.Payload).RequestedPlayers);
  }

  [Theory]
  [InlineData(true, false, 10)]
  [InlineData(false, true, 10)]
  [InlineData(true, true, -1)]
  public void InvalidPauseStateIsRejected(bool paused, bool hasOwner, long serverTick)
  {
    PlayerId? owner = hasOwner ? new PlayerId(1) : null;
    ProtocolMessage message = new(1, new PauseStatePayload(paused, owner, serverTick));

    ProtocolException exception = Assert.Throws<ProtocolException>(() =>
        ProtocolSerializer.Deserialize(ProtocolSerializer.Serialize(message)));

    Assert.Equal(ProtocolError.InvalidPayload, exception.Error);
  }
}
