using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

internal sealed class HudController(ScreenController screens)
{
    public void Update(WorldSnapshotPayload snapshot, PlayerId? localPlayerId)
    {
        PlayerSnapshot? player = snapshot.Players
            .FirstOrDefault(candidate => candidate.Id == localPlayerId);
        string boss = BossText(snapshot);
        string effects = EffectsText(player);
        string text = player is null
            ? $"Wave {snapshot.Wave}    Tick {snapshot.ServerTick}{boss}"
            : PlayerText(snapshot, player, boss, effects);
        screens.SetHud(text);
    }

    private static string BossText(WorldSnapshotPayload snapshot)
    {
        return snapshot.BossHealth is null
            ? string.Empty
            : $"    Boss {snapshot.BossHealth} (phase {snapshot.BossPhase})";
    }

    private static string EffectsText(PlayerSnapshot? player)
    {
        return player?.ActivePowerUps is { Length: > 0 }
            ? $"    {string.Join(", ", player.ActivePowerUps)}"
            : string.Empty;
    }

    private static string PlayerText(
        WorldSnapshotPayload snapshot,
        PlayerSnapshot player,
        string boss,
        string effects)
    {
        string shield = player.Shield ? "ON" : "OFF";
        return $"Lives {player.Lives}    Shield {shield}    " +
                $"Score {player.Score}    Wave {snapshot.Wave}{boss}{effects}";
    }
}
