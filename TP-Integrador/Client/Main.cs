using Godot;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

/// <summary>
/// Orchestrates client presentation, input, and protocol messages on the Godot thread.
/// </summary>
public partial class Main : Node2D
{
  private readonly ClientCoordinator _coordinator = new();
  private readonly SnapshotBuffer _snapshots = new();
  private AudioController _audio = null!;
  private ExitConfirmationView _exitConfirmation = null!;
  private HudController _hud = null!;
  private InputController _input = null!;
  private bool _isPaused;
  private PlayerId? _localPlayerId;
  private PlayerId? _pauseOwnerId;
  private bool _pauseTransitionInProgress;
  private bool _playing;
  private bool _ready;
  private ScreenController _screens = null!;
  private bool _waveTransition;

  public override void _Ready()
  {
    _screens = new ScreenController(this);
    _exitConfirmation = new ExitConfirmationView(this);
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
    HandlePauseShortcut();
    if (_playing)
    {
      UpdateActiveGame();
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
    _screens.LobbyExitButton.Pressed += LobbyExitPressed;
    _screens.PauseExitButton.Pressed += PauseExitPressed;
    _screens.ReadyButton.Pressed += ReadyPressed;
    _screens.ReplayButton.Pressed += ReplayPressed;
    _screens.ResumeButton.Pressed += ResumePressed;
    _screens.ExitButton.Pressed += ResultExitPressed;
    _screens.ShipSelector.ItemSelected += ShipSelected;
    _exitConfirmation.ContinueButton.Pressed += CancelExitConfirmation;
    _exitConfirmation.ExitButton.Pressed += MatchExitPressed;
  }

  private void HandlePauseShortcut()
  {
    if (!_playing || !Input.IsActionJustPressed("pause"))
    {
      return;
    }

    if (_exitConfirmation.IsVisible)
    {
      CancelExitConfirmation();
      return;
    }

    if (_isPaused && _pauseOwnerId != _localPlayerId)
    {
      return;
    }

    TogglePause();
  }

  private void UpdateActiveGame()
  {
    if (!_waveTransition && !_isPaused && !_exitConfirmation.IsVisible)
    {
      _input.Update();
    }

    QueueRedraw();
  }

  private async void ConnectPressed()
  {
    _screens.SetConnecting();
    try
    {
      await ConnectAsync();
    }
    catch (Exception exception) when (IsConnectionFailure(exception))
    {
      _screens.SetConnectError(ClientErrorMessages.For("connection"));
    }
  }

  private Task ConnectAsync()
  {
    return _coordinator.ConnectAsync(
        _screens.HostInput.Text.Trim(),
        _screens.NameInput.Text.Trim(),
        (int)_screens.PlayerCountSelector.GetSelectedId());
  }

  private static bool IsConnectionFailure(Exception exception)
  {
    return exception is IOException or InvalidOperationException or
        System.Net.Sockets.SocketException or OperationCanceledException;
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
      default:
        HandleMatchMessage(payload);
        break;
    }
  }

  private void HandleMatchMessage(IProtocolPayload? payload)
  {
    switch (payload)
    {
      case MatchStartedPayload:
        ShowGame();
        break;
      case PauseStatePayload pause:
        HandlePauseState(pause);
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

  private async void LobbyExitPressed()
  {
    await LeaveMatchAsync("Player returned to the start screen.");
  }

  private async void ResultExitPressed()
  {
    _screens.SetLeaving();
    await LeaveMatchAsync("Player returned to the start screen.");
  }

  private async void MatchExitPressed()
  {
    _exitConfirmation.Hide();
    await LeaveMatchAsync("Player left the active match.");
  }

  private void CancelExitConfirmation()
  {
    _exitConfirmation.Hide();
    if (_isPaused)
    {
      _screens.ShowPause(_pauseOwnerId == _localPlayerId);
    }
  }

  private async void TogglePause()
  {
    if (_pauseTransitionInProgress || !_playing)
    {
      return;
    }

    _pauseTransitionInProgress = true;
    try
    {
      if (_isPaused)
      {
        await SendSafelyAsync(new SetPausePayload(false));
      }
      else
      {
        await RequestPauseAsync();
      }
    }
    catch (Exception exception) when (IsConnectionFailure(exception))
    {
      ShowConnectionFailure();
    }
    finally
    {
      _pauseTransitionInProgress = false;
    }
  }

  private async Task RequestPauseAsync()
  {
    _input.Disable();
    if (!_playing)
    {
      return;
    }

    await SendSafelyAsync(new SetPausePayload(true));
  }

  private async void ResumePressed()
  {
    if (_isPaused && _pauseOwnerId == _localPlayerId)
    {
      await SendSafelyAsync(new SetPausePayload(false));
    }
  }

  private void HandlePauseState(PauseStatePayload pause)
  {
    _isPaused = pause.Paused;
    _pauseOwnerId = pause.PausedBy;
    if (pause.Paused)
    {
      _input.Disable();
      _screens.ShowPause(pause.PausedBy == _localPlayerId);
      return;
    }

    _screens.HidePause();
    _input.Resume();
  }

  private void PauseExitPressed()
  {
    _screens.HidePause();
    _exitConfirmation.Show();
  }

  private async Task LeaveMatchAsync(string reason)
  {
    await _coordinator.DisconnectAsync(reason);
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
    catch (Exception exception) when (IsConnectionFailure(exception))
    {
      ShowConnectionFailure();
    }
  }

  private void ShowConnectionFailure()
  {
    _playing = false;
    _exitConfirmation.Hide();
    _screens.ShowConnection();
    _screens.SetConnectError(ClientErrorMessages.For("connection"));
  }

  private void ShowGame()
  {
    _snapshots.Clear();
    _playing = true;
    _ready = false;
    _waveTransition = false;
    _isPaused = false;
    _pauseOwnerId = null;
    _exitConfirmation.Hide();
    _input.Enable();
    _screens.SetReady(ready: false);
    _screens.ShowGame();
  }

  private void ShowResult(MatchEndedPayload result)
  {
    _playing = false;
    _ready = false;
    _waveTransition = false;
    _isPaused = false;
    _pauseOwnerId = null;
    _exitConfirmation.Hide();
    _audio.HandleResult(result);
    _screens.ShowResult(result, _localPlayerId);
    _input.Enable();
    QueueRedraw();
  }

  private void ShowError(ErrorPayload error)
  {
    ResetClientState();
    _screens.ShowConnection();
    _screens.SetConnectError(ClientErrorMessages.For(error.Code));
    _ = _coordinator.DisposeAsync().AsTask();
  }

  private void ResetClientState()
  {
    _playing = false;
    _ready = false;
    _waveTransition = false;
    _isPaused = false;
    _pauseOwnerId = null;
    _localPlayerId = null;
    _snapshots.Clear();
    _exitConfirmation.Hide();
    _screens.SetReady(ready: false);
    _screens.SetRematchReady(ready: false);
    _input.Enable();
  }
}
