using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;
using SpaceShooter.Server.Game;
using SpaceShooter.Server.Sessions;
using SpaceShooter.Server.Simulation;

namespace SpaceShooter.Server.Tests;

public sealed class AuthoritativeMatchTests
{
    [Fact]
    public void MatchPreservesSessionPlayerIdsAndAppliesOnlyTheirInputs()
    {
        var fixture = new MatchFixture(new PlayerId(7), new PlayerId(9));

        var firstPosition = fixture.Match.Simulation!.State.Players[new PlayerId(7)].Position;
        fixture.ApplyInput(new PlayerId(7), new InputStatePayload(1, 0, false, 1));

        fixture.Match.Step();

        var players = fixture.Match.Simulation.State.Players;
        Assert.Equal([new PlayerId(7), new PlayerId(9)], players.Keys.OrderBy(id => id.Value));
        Assert.True(players[new PlayerId(7)].Position.X > firstPosition.X);
        Assert.Equal(
            fixture.StartingPositions[new PlayerId(9)],
            players[new PlayerId(9)].Position);
    }

    [Fact]
    public void ShootingEnemyProducesAuthoritativeScoreSnapshotAndResult()
    {
        var fixture = new MatchFixture(new PlayerId(1), new PlayerId(2));
        fixture.Match.TryStart();
        fixture.ApplyInput(new PlayerId(1), new InputStatePayload(0, 0, true, 1));

        for (var tick = 0; tick < 120 && !fixture.Match.IsEnded; tick++)
        {
            fixture.Match.Step();
        }

        var snapshot = fixture.Match.CreateSnapshot();
        var result = fixture.Match.CreateResult();

        Assert.True(fixture.Match.IsEnded);
        Assert.Equal(100, snapshot.Players.Single(player => player.Id == new PlayerId(1)).Score);
        Assert.Equal(EnemyStatus.Destroyed, Assert.Single(snapshot.Enemies).Status);
        Assert.True(result.Victory);
        Assert.Equal(new PlayerId(1), result.Ranking[0].PlayerId);
    }

    [Fact]
    public void DisconnectedPlayerIsRemovedFromAuthoritativeWorld()
    {
        var fixture = new MatchFixture(new PlayerId(1), new PlayerId(2));
        fixture.Match.TryStart();

        fixture.Lobby.Apply(new DisconnectSessionCommand(new PlayerId(2)), fixture.Now);
        fixture.Match.Step();

        Assert.DoesNotContain(new PlayerId(2), fixture.Match.Simulation!.State.Players.Keys);
    }

    [Fact]
    public void EndedMatchCanResetOnlyAfterEverySessionDisconnects()
    {
        var fixture = new MatchFixture(new PlayerId(1), new PlayerId(2));
        fixture.ApplyInput(new PlayerId(1), new InputStatePayload(0, 0, true, 1));
        for (var tick = 0; tick < 120 && !fixture.Match.IsEnded; tick++)
        {
            fixture.Match.Step();
        }

        Assert.False(fixture.Match.TryReset());
        fixture.Lobby.Apply(new DisconnectSessionCommand(new PlayerId(1)), fixture.Now);
        fixture.Lobby.Apply(new DisconnectSessionCommand(new PlayerId(2)), fixture.Now);

        Assert.True(fixture.Match.TryReset());
        Assert.False(fixture.Match.IsStarted);
    }

    private sealed class MatchFixture
    {
        private long _sequence = 10;

        public MatchFixture(params PlayerId[] playerIds)
        {
            Lobby = new LobbyCoordinator(Registry, _ => { }, TimeSpan.FromTicks(1));
            foreach (var playerId in playerIds)
            {
                Lobby.Apply(
                    new JoinSessionCommand(playerId, NextSequence(), new HelloPayload("test", $"Pilot {playerId.Value}")),
                    Now);
                Lobby.Apply(
                    new ClientPayloadCommand(playerId, NextSequence(), new SetReadyPayload(true)),
                    Now);
            }

            Lobby.Update(Now + TimeSpan.FromMilliseconds(1));
            Match = new AuthoritativeMatch(Registry, Config, new SeededRandomSource(1));
            Match.TryStart();
            StartingPositions = Match.Simulation!.State.Players.ToDictionary(pair => pair.Key, pair => pair.Value.Position);
        }

        public TimeSpan Now { get; } = TimeSpan.FromSeconds(1);

        public SessionRegistry Registry { get; } = new();

        public SimulationConfig Config { get; } = new();

        public LobbyCoordinator Lobby { get; }

        public AuthoritativeMatch Match { get; private set; } = null!;

        public Dictionary<PlayerId, Vector2D> StartingPositions { get; private set; } = null!;

        public void ApplyInput(PlayerId playerId, InputStatePayload input) => Lobby.Apply(
            new ClientPayloadCommand(playerId, NextSequence(), input),
            Now);

        private long NextSequence() => ++_sequence;
    }
}
