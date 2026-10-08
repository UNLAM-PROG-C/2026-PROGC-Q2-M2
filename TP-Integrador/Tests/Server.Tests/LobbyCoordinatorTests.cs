using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Sessions;

namespace SpaceShooter.Server.Tests;

public sealed class LobbyCoordinatorTests
{
    [Fact]
    public void FourReadyPlayersEnterPlayingAfterCountdown()
    {
        var fixture = new LobbyFixture();
        var players = Enumerable.Range(1, 4).Select(fixture.Join).ToArray();

        foreach (var playerId in players)
        {
            fixture.Ready(playerId);
        }

        Assert.NotNull(fixture.Coordinator.CountdownDeadline);
        fixture.Coordinator.Update(fixture.Now + LobbyCoordinator.DefaultCountdownDuration);

        Assert.All(fixture.Registry.Sessions, session =>
            Assert.Equal(SessionStatus.Playing, session.Status));
        Assert.Contains(fixture.Broadcasts, message => message.Payload is MatchStartedPayload);
    }

    [Fact]
    public void FourPlayerLobbyWaitsForExactlyFourReadyPlayers()
    {
        var fixture = new LobbyFixture(requiredPlayers: 4);
        var first = fixture.Join(1);
        var second = fixture.Join(2);
        fixture.Ready(first);
        fixture.Ready(second);

        Assert.Null(fixture.Coordinator.CountdownDeadline);

        var third = fixture.Join(3);
        var fourth = fixture.Join(4);
        fixture.Ready(third);
        fixture.Ready(fourth);

        Assert.NotNull(fixture.Coordinator.CountdownDeadline);
        var lobby = fixture.Broadcasts
            .Select(message => message.Payload)
            .OfType<LobbyStatePayload>()
            .Last();
        Assert.Equal(4, lobby.RequiredPlayers);
        Assert.Equal(4, lobby.Participants.Length);
    }

    [Fact]
    public void CommandIdentityCannotChangeAnotherPlayer()
    {
        var fixture = new LobbyFixture();
        var first = fixture.Join(1);
        var second = fixture.Join(2);

        fixture.Ready(first);

        Assert.True(fixture.Registry.TryGet(first, out var firstSession));
        Assert.True(fixture.Registry.TryGet(second, out var secondSession));
        Assert.Equal(SessionStatus.Ready, firstSession!.Status);
        Assert.Equal(SessionStatus.InLobby, secondSession!.Status);
    }

    [Fact]
    public void DisconnectingUnreadyPlayerAllowsRemainingReadyPlayersToStart()
    {
        var fixture = new LobbyFixture();
        var first = fixture.Join(1);
        var second = fixture.Join(2);
        var third = fixture.Join(3);
        fixture.Ready(first);
        fixture.Ready(second);

        Assert.Null(fixture.Coordinator.CountdownDeadline);

        fixture.Coordinator.Apply(new DisconnectSessionCommand(third), fixture.Now);

        Assert.NotNull(fixture.Coordinator.CountdownDeadline);
        fixture.Coordinator.Update(fixture.Now + LobbyCoordinator.DefaultCountdownDuration);
        Assert.All(fixture.Registry.Sessions, session =>
            Assert.Equal(SessionStatus.Playing, session.Status));
    }

    [Fact]
    public void PlayerCanSelectShipAndCancelReadyState()
    {
        var fixture = new LobbyFixture();
        var playerId = fixture.Join(1);
        fixture.Coordinator.Apply(
            new ClientPayloadCommand(playerId, 2, new SelectShipPayload("red")),
            fixture.Now);
        fixture.Ready(playerId);
        fixture.Coordinator.Apply(
            new ClientPayloadCommand(playerId, 4, new SetReadyPayload(false)),
            fixture.Now);

        Assert.True(fixture.Registry.TryGet(playerId, out var session));
        Assert.Equal("Pilot 1", session!.PlayerName);
        Assert.Equal("red", session.ShipId);
        Assert.Equal(SessionStatus.InLobby, session.Status);
    }

    [Fact]
    public void InputIsAcceptedOnlyForTheOwningPlayingSession()
    {
        var fixture = new LobbyFixture();
        var first = fixture.Join(1);
        var second = fixture.Join(2);
        fixture.Ready(first);
        fixture.Ready(second);
        fixture.Coordinator.Update(fixture.Now + LobbyCoordinator.DefaultCountdownDuration);
        var input = new InputStatePayload(1, 0, true, 10);

        fixture.Coordinator.Apply(new ClientPayloadCommand(first, 3, input), fixture.Now);

        Assert.True(fixture.Registry.TryGet(first, out var firstSession));
        Assert.True(fixture.Registry.TryGet(second, out var secondSession));
        Assert.Equal(input, firstSession!.LastInput);
        Assert.Null(secondSession!.LastInput);
    }

    [Fact]
    public void InputChannelIsBounded()
    {
        var channel = new SessionCommandChannel(capacity: 1);

        var firstAccepted = channel.TryEnqueue(new DisconnectSessionCommand(new PlayerId(1)));
        var secondAccepted = channel.TryEnqueue(new DisconnectSessionCommand(new PlayerId(2)));

        Assert.True(firstAccepted);
        Assert.False(secondAccepted);
    }

    [Fact]
    public void DuplicateAndOutOfOrderSequencesAreRejected()
    {
        var sequence = new IncomingSequence(initialSequence: 5);

        Assert.False(sequence.TryAccept(5));
        Assert.False(sequence.TryAccept(4));
        Assert.True(sequence.TryAccept(6));
        Assert.False(sequence.TryAccept(6));
    }

    [Fact]
    public void EmptyLobbyCanChooseADifferentSizeForNextMatch()
    {
        var admission = new LobbyAdmission();
        var first = new PlayerId(1);
        var second = new PlayerId(2);

        Assert.True(admission.TryAdmit(first, 2, out _, out _));
        Assert.True(admission.TryAdmit(second, 2, out _, out _));
        admission.MarkMatchStarted();
        admission.Release(first);
        admission.Release(second);

        Assert.True(admission.TryAdmit(new PlayerId(3), 4, out _, out _));
        Assert.Equal(4, admission.TargetPlayers);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void AdmissionAcceptsEverySupportedLobbySize(int requestedPlayers)
    {
        var admission = new LobbyAdmission();

        Assert.True(admission.TryAdmit(new PlayerId(1), requestedPlayers, out _, out _));
        Assert.Equal(requestedPlayers, admission.TargetPlayers);
    }

    [Fact]
    public void ThreePlayerLobbyWaitsForExactlyThreeReadyPlayers()
    {
        var fixture = new LobbyFixture(requiredPlayers: 3);
        var first = fixture.Join(1);
        var second = fixture.Join(2);
        fixture.Ready(first);
        fixture.Ready(second);
        Assert.Null(fixture.Coordinator.CountdownDeadline);

        var third = fixture.Join(3);
        fixture.Ready(third);

        Assert.NotNull(fixture.Coordinator.CountdownDeadline);
    }

    private sealed class LobbyFixture
    {
        public TimeSpan Now { get; } = TimeSpan.FromSeconds(10);

        public SessionRegistry Registry { get; } = new();

        public List<ProtocolMessage> Broadcasts { get; } = [];

        public LobbyCoordinator Coordinator { get; }

        public LobbyFixture(int? requiredPlayers = null)
        {
            Coordinator = new LobbyCoordinator(
                Registry,
                Broadcasts.Add,
                requiredPlayers: requiredPlayers is null ? null : () => requiredPlayers.Value);
        }

        public PlayerId Join(int number)
        {
            var playerId = new PlayerId(number);
            Coordinator.Apply(
                new JoinSessionCommand(playerId, 1, new HelloPayload("test", $"Pilot {number}")),
                Now);
            return playerId;
        }

        public void Ready(PlayerId playerId) => Coordinator.Apply(
            new ClientPayloadCommand(playerId, 2, new SetReadyPayload(true)),
            Now);
    }
}
