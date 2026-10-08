using Godot;
using SpaceShooter.Client.Networking;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

namespace SpaceShooter.Client;

public partial class Main : Node2D
{
    private readonly Color[] _playerColors =
    [
        new(0.20f, 0.75f, 1.00f),
        new(1.00f, 0.35f, 0.45f),
        new(0.35f, 1.00f, 0.55f),
        new(1.00f, 0.80f, 0.25f),
    ];

    private VBoxContainer _connectionPanel = null!;
    private VBoxContainer _lobbyPanel = null!;
    private VBoxContainer _resultPanel = null!;
    private LineEdit _hostInput = null!;
    private LineEdit _nameInput = null!;
    private Button _connectButton = null!;
    private OptionButton _playerCountSelector = null!;
    private Button _readyButton = null!;
    private OptionButton _shipSelector = null!;
    private Label _lobbyLabel = null!;
    private Label _hudLabel = null!;
    private Label _resultLabel = null!;
    private LanClient? _client;
    private PlayerId? _localPlayerId;
    private WorldSnapshotPayload? _previousSnapshot;
    private WorldSnapshotPayload? _currentSnapshot;
    private ulong _snapshotReceivedAt;
    private InputStatePayload? _lastInput;
    private ulong _lastInputSentAt;
    private bool _inputSendInFlight;
    private bool _ready;
    private bool _playing;

    public override void _Ready()
    {
        BuildInterface();
        ShowConnection();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        DrainNetworkMessages();
        if (_playing)
        {
            CaptureAndSendInput();
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 1280, 720), new Color(0.025f, 0.035f, 0.09f));
        if (!_playing || _currentSnapshot is null)
        {
            return;
        }

        var interpolation = Math.Clamp((Time.GetTicksMsec() - _snapshotReceivedAt) / 50f, 0, 1);
        foreach (var player in _currentSnapshot.Players.Where(player =>
                     player.Status is PlayerStatus.Active or PlayerStatus.Invulnerable))
        {
            var position = InterpolatePlayer(player, interpolation);
            var color = _playerColors[(player.Id.Value - 1) % _playerColors.Length];
            DrawCircle(ToGodot(position), 16, color);
            DrawLine(
                ToGodot(position) + new Vector2(0, -16),
                ToGodot(position) + new Vector2(0, -25),
                color,
                4);
            if (_localPlayerId == player.Id)
            {
                DrawArc(ToGodot(position), 22, 0, Mathf.Tau, 24, Colors.White, 2);
            }

            if (player.Status == PlayerStatus.Invulnerable || player.Shield > 0)
            {
                DrawArc(ToGodot(position), 28, 0, Mathf.Tau, 24, new Color(0.3f, 0.9f, 1f), 3);
            }
        }

        foreach (var enemy in _currentSnapshot.Enemies.Where(enemy => enemy.Status == EnemyStatus.Active))
        {
            var position = InterpolateEnemy(enemy, interpolation);
            var size = enemy.Kind == EnemyKind.Boss ? 42f : 16f;
            var color = enemy.Kind switch
            {
                EnemyKind.Scout => new Color(1, 0.25f, 0.65f),
                EnemyKind.Diver => new Color(1, 0.55f, 0.15f),
                EnemyKind.Tank => new Color(0.65f, 0.35f, 1),
                EnemyKind.Boss => new Color(1, 0.1f, 0.1f),
                _ => Colors.White,
            };
            DrawRect(new Rect2(ToGodot(position) - new Vector2(size, size), new Vector2(size * 2, size * 2)), color);
        }

        foreach (var projectile in _currentSnapshot.Projectiles.Where(projectile => projectile.Status == ProjectileStatus.Active))
        {
            var position = InterpolateProjectile(projectile, interpolation);
            DrawRect(new Rect2(ToGodot(position) - new Vector2(2, 8), new Vector2(4, 16)), Colors.White);
        }


        foreach (var powerUp in _currentSnapshot.PowerUps.Where(powerUp => powerUp.Status == PowerUpStatus.Available))
        {
            var color = powerUp.Kind switch
            {
                PowerUpKind.RapidFire => Colors.Yellow,
                PowerUpKind.MultiShot => Colors.LimeGreen,
                PowerUpKind.Shield => Colors.Cyan,
                _ => Colors.White,
            };
            DrawCircle(ToGodot(powerUp.Position), 11, color);
        }
    }

    public override void _ExitTree()
    {
        if (_client is not null)
        {
            _ = _client.DisposeAsync().AsTask();
        }
    }

    private void BuildInterface()
    {
        var ui = new CanvasLayer();
        AddChild(ui);

        _connectionPanel = CreatePanel(new Vector2(40, 40));
        _hostInput = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Server IP" };
        _nameInput = new LineEdit { Text = System.Environment.MachineName, PlaceholderText = "Player name", MaxLength = 32 };
        _playerCountSelector = new OptionButton();
        _playerCountSelector.AddItem("1 player", 1);
        _playerCountSelector.AddItem("2 players", 2);
        _playerCountSelector.AddItem("3 players", 3);
        _playerCountSelector.AddItem("4 players", 4);
        _connectButton = new Button { Text = "Connect" };
        _connectButton.Pressed += ConnectPressed;
        _connectionPanel.AddChild(new Label { Text = "SPACE SHOOTER LAN" });
        _connectionPanel.AddChild(_hostInput);
        _connectionPanel.AddChild(_nameInput);
        _connectionPanel.AddChild(new Label { Text = "Match size" });
        _connectionPanel.AddChild(_playerCountSelector);
        _connectionPanel.AddChild(_connectButton);
        ui.AddChild(_connectionPanel);

        _lobbyPanel = CreatePanel(new Vector2(40, 40));
        _lobbyLabel = new Label();
        _shipSelector = new OptionButton();
        foreach (var ship in new[] { "blue", "red", "green", "gold" })
        {
            _shipSelector.AddItem(ship);
        }

        _shipSelector.ItemSelected += ShipSelected;
        _readyButton = new Button { Text = "Ready" };
        _readyButton.Pressed += ReadyPressed;
        _lobbyPanel.AddChild(new Label { Text = "LOBBY" });
        _lobbyPanel.AddChild(_lobbyLabel);
        _lobbyPanel.AddChild(_shipSelector);
        _lobbyPanel.AddChild(_readyButton);
        ui.AddChild(_lobbyPanel);

        _hudLabel = new Label { Position = new Vector2(20, 15) };
        ui.AddChild(_hudLabel);

        _resultPanel = CreatePanel(new Vector2(40, 40));
        _resultLabel = new Label();
        _resultPanel.AddChild(new Label { Text = "MATCH RESULT" });
        _resultPanel.AddChild(_resultLabel);
        ui.AddChild(_resultPanel);
    }

    private static VBoxContainer CreatePanel(Vector2 position) => new()
    {
        Position = position,
        Size = new Vector2(420, 400),
    };

    private async void ConnectPressed()
    {
        _connectButton.Disabled = true;
        _connectButton.Text = "Connecting...";
        try
        {
            if (_client is not null)
            {
                await _client.DisposeAsync();
            }

            _client = new LanClient();
            await _client.ConnectAsync(
                _hostInput.Text.Trim(),
                7777,
                _nameInput.Text.Trim(),
                (int)_playerCountSelector.GetSelectedId());
        }
        catch (Exception exception)
        {
            _connectButton.Disabled = false;
            _connectButton.Text = $"Retry: {exception.Message}";
        }
    }

    private async void ShipSelected(long index)
    {
        if (_client?.IsConnected == true)
        {
            await _client.SendAsync(new SelectShipPayload(_shipSelector.GetItemText((int)index)));
        }
    }

    private async void ReadyPressed()
    {
        if (_client?.IsConnected != true)
        {
            return;
        }

        _ready = !_ready;
        _readyButton.Text = _ready ? "Cancel ready" : "Ready";
        await _client.SendAsync(new SetReadyPayload(_ready));
    }

    private void DrainNetworkMessages()
    {
        while (_client?.TryDequeue(out var payload) == true)
        {
            switch (payload)
            {
                case WelcomePayload welcome:
                    _localPlayerId = welcome.PlayerId;
                    ShowLobby();
                    _ = _client.SendAsync(new SelectShipPayload(_shipSelector.GetItemText(_shipSelector.Selected)));
                    break;
                case LobbyStatePayload lobby:
                    UpdateLobby(lobby);
                    break;
                case MatchStartedPayload:
                    ShowGame();
                    break;
                case WorldSnapshotPayload snapshot:
                    _previousSnapshot = _currentSnapshot;
                    _currentSnapshot = snapshot;
                    _snapshotReceivedAt = Time.GetTicksMsec();
                    UpdateHud(snapshot);
                    break;
                case MatchEndedPayload result:
                    ShowResult(result);
                    break;
                case ErrorPayload error:
                    ShowError(error);
                    break;
            }
        }
    }

    private void UpdateLobby(LobbyStatePayload lobby)
    {
        var players = string.Join('\n', lobby.Participants.Select(participant =>
            $"{participant.PlayerName} · {participant.ShipId} · {(participant.Ready ? "READY" : "waiting")}"));
        var countdown = lobby.CountdownSeconds is null ? string.Empty : $"\nStarting in {lobby.CountdownSeconds}...";
        _lobbyLabel.Text = $"Players {lobby.Participants.Length}/{lobby.RequiredPlayers}\n{players}{countdown}";
    }

    private void UpdateHud(WorldSnapshotPayload snapshot)
    {
        var local = snapshot.Players.FirstOrDefault(player => player.Id == _localPlayerId);
        var boss = snapshot.BossHealth is null
            ? string.Empty
            : $"    Boss {snapshot.BossHealth} (phase {snapshot.BossPhase})";
        var effects = local?.ActivePowerUps is { Length: > 0 }
            ? $"    {string.Join(", ", local.ActivePowerUps)}"
            : string.Empty;
        _hudLabel.Text = local is null
            ? $"Wave {snapshot.Wave}    Tick {snapshot.ServerTick}{boss}"
            : $"Lives {local.Lives}    Shield {local.Shield}    Score {local.Score}    Wave {snapshot.Wave}{boss}{effects}";
    }

    private void CaptureAndSendInput()
    {
        var input = new InputStatePayload(
            Input.GetAxis("ui_left", "ui_right"),
            Input.GetAxis("ui_up", "ui_down"),
            Input.IsActionPressed("ui_accept"),
            (long)Time.GetTicksMsec());
        var now = Time.GetTicksMsec();
        if (_inputSendInFlight || (input == _lastInput && now - _lastInputSentAt < 100))
        {
            return;
        }

        _lastInput = input;
        _lastInputSentAt = now;
        _inputSendInFlight = true;
        _ = SendInputAsync(input);
    }

    private async Task SendInputAsync(InputStatePayload input)
    {
        try
        {
            if (_client?.IsConnected == true)
            {
                await _client.SendAsync(input);
            }
        }
        finally
        {
            _inputSendInFlight = false;
        }
    }

    private Vector2D InterpolatePlayer(PlayerSnapshot current, float amount)
    {
        var previous = _previousSnapshot?.Players.FirstOrDefault(player => player.Id == current.Id);
        return previous is null ? current.Position : Lerp(previous.Position, current.Position, amount);
    }

    private Vector2D InterpolateEnemy(EnemySnapshot current, float amount)
    {
        var previous = _previousSnapshot?.Enemies.FirstOrDefault(enemy => enemy.Id == current.Id);
        return previous is null ? current.Position : Lerp(previous.Position, current.Position, amount);
    }

    private Vector2D InterpolateProjectile(ProjectileSnapshot current, float amount)
    {
        var previous = _previousSnapshot?.Projectiles.FirstOrDefault(projectile => projectile.Id == current.Id);
        return previous is null ? current.Position : Lerp(previous.Position, current.Position, amount);
    }

    private static Vector2D Lerp(Vector2D from, Vector2D to, float amount) => new(
        Mathf.Lerp(from.X, to.X, amount),
        Mathf.Lerp(from.Y, to.Y, amount));

    private static Vector2 ToGodot(Vector2D value) => new(value.X, value.Y);

    private void ShowConnection()
    {
        _connectionPanel.Visible = true;
        _lobbyPanel.Visible = false;
        _resultPanel.Visible = false;
        _hudLabel.Visible = false;
        _playing = false;
    }

    private void ShowLobby()
    {
        _connectionPanel.Visible = false;
        _lobbyPanel.Visible = true;
        _resultPanel.Visible = false;
        _hudLabel.Visible = false;
        _playing = false;
    }

    private void ShowGame()
    {
        _connectionPanel.Visible = false;
        _lobbyPanel.Visible = false;
        _resultPanel.Visible = false;
        _hudLabel.Visible = true;
        _playing = true;
    }

    private void ShowResult(MatchEndedPayload result)
    {
        _playing = false;
        _hudLabel.Visible = false;
        _resultPanel.Visible = true;
        var ranking = string.Join('\n', result.Ranking.Select((entry, index) =>
            $"{index + 1}. {entry.PlayerName}: {entry.Score}"));
        var records = result.Records is { Length: > 0 }
            ? $"\n\nRECORDS\n{string.Join('\n', result.Records.Select((entry, index) => $"{index + 1}. {entry.PlayerName}: {entry.Score}"))}"
            : string.Empty;
        _resultLabel.Text = $"{(result.Victory ? "VICTORY" : "DEFEAT")}\n{ranking}{records}";
        QueueRedraw();
    }

    private void ShowError(ErrorPayload error)
    {
        _playing = false;
        _connectionPanel.Visible = true;
        _lobbyPanel.Visible = false;
        _resultPanel.Visible = false;
        _hudLabel.Visible = false;
        _connectButton.Disabled = false;
        _connectButton.Text = $"{error.Code}: {error.Description}";
    }
}
