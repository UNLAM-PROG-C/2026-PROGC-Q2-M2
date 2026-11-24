using Godot;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

/// <summary>
/// Derives presentation-only sound cues from consecutive server snapshots.
/// </summary>
internal sealed class AudioController
{
    private readonly AudioStreamPlayer _laserPlayer;
    private readonly AudioStreamPlayer _losePlayer;
    private readonly AudioStreamPlayer _shieldPlayer;

    public AudioController(Node owner)
    {
        _laserPlayer = CreatePlayer(owner, AssetCatalog.LaserSound);
        _losePlayer = CreatePlayer(owner, AssetCatalog.LoseSound);
        _shieldPlayer = CreatePlayer(owner, AssetCatalog.ShieldSound);
    }

    public void HandleResult(MatchEndedPayload result)
    {
        if (!result.Victory)
        {
            _losePlayer.Play();
        }
    }

    public void HandleSnapshot(
        WorldSnapshotPayload? previous,
        WorldSnapshotPayload current,
        PlayerId? localPlayerId)
    {
        if (previous is not null && current.Projectiles.Length > previous.Projectiles.Length)
        {
            _laserPlayer.Play();
        }

        if (ShieldActivated(previous, current, localPlayerId))
        {
            _shieldPlayer.Play();
        }
    }

    private static AudioStreamPlayer CreatePlayer(Node owner, AudioStream stream)
    {
        AudioStreamPlayer player = new() { Stream = stream };
        owner.AddChild(player);
        return player;
    }

    private static bool ShieldActivated(
        WorldSnapshotPayload? previous,
        WorldSnapshotPayload current,
        PlayerId? localPlayerId)
    {
        PlayerSnapshot? currentPlayer = FindPlayer(current, localPlayerId);
        PlayerSnapshot? previousPlayer = previous is null ? null : FindPlayer(previous, localPlayerId);
        return currentPlayer?.Shield == true && previousPlayer?.Shield != true;
    }

    private static PlayerSnapshot? FindPlayer(
        WorldSnapshotPayload snapshot,
        PlayerId? playerId)
    {
        return snapshot.Players.FirstOrDefault(player => player.Id == playerId);
    }
}
