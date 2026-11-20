using Godot;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

public partial class Main : Node2D
{
    private readonly ClientCoordinator _coordinator = new();
    private readonly SnapshotBuffer _snapshots = new();
    private AudioController _audio = null!;
    private HudController _hud = null!;
    private InputController _input = null!;
    private PlayerId? _localPlayerId;
    private bool _playing;
    private bool _ready;
    private bool _waveTransition;
    private ScreenController _screens = null!;

    public override void _Ready()
    {
        _screens = new ScreenController(this);
        _audio = new AudioController(this);
        _hud = new HudController(_screens);
        _input = new InputController(_coordinator);
        BindInterfaceEvents();
        _screens.ShowConnection();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        DrainNetworkMessages();
        if (_playing)
        {
            if (!_waveTransition)
            {
                _input.Update();
            }

            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        GameRenderer.Draw(this, _snapshots, _localPlayerId, _playing);
    }

    public override void _ExitTree()
    {
        _ = _coordinator.DisposeAsync().AsTask();
    }

    private void BindInterfaceEvents()
    {
        _screens.ConnectButton.Pressed += ConnectPressed;
        _screens.ReadyButton.Pressed += ReadyPressed;
        _screens.ReplayButton.Pressed += ReplayPressed;
        _screens.ExitButton.Pressed += ExitPressed;
        _screens.ShipSelector.ItemSelected += ShipSelected;
    }

    private async void ConnectPressed()
    {
        _screens.SetConnecting();
        try
        {
            await _coordinator.ConnectAsync(
                _screens.HostInput.Text.Trim(),
                _screens.NameInput.Text.Trim(),
                (int)_screens.PlayerCountSelector.GetSelectedId());
        }
        catch (Exception exception)
        {
            _screens.SetConnectError($"No se pudo conectar: {exception.Message}");
        }
    }

    private void DrainNetworkMessages()
    {
        while (_coordinator.TryDequeue(out IProtocolPayload? payload))
        {
            HandleMessage(payload);
        }
    }

    private void HandleMessage(IProtocolPayload? payload)
    {
        switch (payload)
        {
            case WelcomePayload welcome:
                HandleWelcome(welcome);
                break;
            case LobbyStatePayload lobby:
                _screens.ShowLobbyState(lobby);
                break;
            case MatchStartedPayload:
                ShowGame();
                break;
            case WorldSnapshotPayload snapshot:
                HandleSnapshot(snapshot);
                break;
            case MatchEndedPayload result:
                ShowResult(result);
                break;
            case ErrorPayload error:
                ShowError(error);
                break;
        }
    }

    private void HandleWelcome(WelcomePayload welcome)
    {
        _localPlayerId = welcome.PlayerId;
        _ready = false;
        _screens.ShowLobby();
        _screens.SetReady(ready: false);
        string ship = ScreenController.GetShipId(_screens.ShipSelector.Selected);
        _ = SendSafelyAsync(new SelectShipPayload(ship));
    }

    private void HandleSnapshot(WorldSnapshotPayload snapshot)
    {
        _audio.HandleSnapshot(_snapshots.Current, snapshot, _localPlayerId);
        _snapshots.Accept(snapshot, Time.GetTicksMsec());
        _hud.Update(snapshot, _localPlayerId);
        _waveTransition = snapshot.WaveTransitionRemainingMilliseconds > 0;
        _screens.SetWaveTransition(
            snapshot.Wave,
            snapshot.WaveTransitionRemainingMilliseconds,
            snapshot.WaveCompletionBonus);
    }

    private async void ReadyPressed()
    {
        if (!_coordinator.IsConnected)
        {
            return;
        }

        _ready = !_ready;
        _screens.SetReady(_ready);
        await SendSafelyAsync(new SetReadyPayload(_ready));
    }

    private async void ReplayPressed()
    {
        if (!_coordinator.IsConnected)
        {
            return;
        }

        _ready = !_ready;
        _screens.SetRematchReady(_ready);
        await SendSafelyAsync(new SetReadyPayload(_ready));
    }

    private async void ExitPressed()
    {
        _screens.SetLeaving();
        await _coordinator.DisconnectAsync("Player returned to the start screen.");
        ResetClientState();
        _screens.ShowConnection();
        QueueRedraw();
    }

    private async void ShipSelected(long index)
    {
        if (_coordinator.IsConnected)
        {
            string ship = ScreenController.GetShipId(index);
            await SendSafelyAsync(new SelectShipPayload(ship));
        }
    }

    private async Task SendSafelyAsync(IProtocolPayload payload)
    {
        try
        {
            await _coordinator.SendAsync(payload);
        }
        catch (Exception exception)
        {
            _playing = false;
            _screens.ShowConnection();
            _screens.SetConnectError($"Error de conexión: {exception.Message}");
        }
    }

    private void ShowGame()
    {
        _snapshots.Clear();
        _playing = true;
        _ready = false;
        _waveTransition = false;
        _screens.SetReady(ready: false);
        _screens.ShowGame();
    }

    private void ShowResult(MatchEndedPayload result)
    {
        _playing = false;
        _ready = false;
        _waveTransition = false;
        _audio.HandleResult(result);
        _screens.ShowResult(result, _localPlayerId);
        QueueRedraw();
    }

    private void ShowError(ErrorPayload error)
    {
        ResetClientState();
        _screens.ShowConnection();
        _screens.SetConnectError($"{error.Code}: {error.Description}");
    }

    private void ResetClientState()
    {
        _playing = false;
        _ready = false;
        _waveTransition = false;
        _localPlayerId = null;
        _snapshots.Clear();
        _screens.SetReady(ready: false);
        _screens.SetRematchReady(ready: false);
    }
}
