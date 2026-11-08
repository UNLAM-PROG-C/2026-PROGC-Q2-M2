using Godot;
using SpaceShooter.Protocol;
using System.Diagnostics.CodeAnalysis;

namespace SpaceShooter.Client;

/// <summary>
/// Builds the user interface and controls screen visibility.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Godot owns and disposes nodes through the scene tree.")]
internal sealed class ScreenController
{
    private readonly VBoxContainer _connectionPanel;
    private readonly Label _hudLabel;
    private readonly VBoxContainer _lobbyPanel;
    private readonly Label _lobbyLabel;
    private readonly VBoxContainer _resultPanel;
    private readonly Label _resultLabel;

    public ScreenController(Node owner)
    {
        CanvasLayer layer = new();
        owner.AddChild(layer);
        _connectionPanel = BuildConnectionPanel(layer);
        _lobbyPanel = BuildLobbyPanel(layer);
        _lobbyLabel = new Label();
        _lobbyPanel.AddChild(_lobbyLabel);
        AddLobbyControls();
        _hudLabel = new Label { Position = new Vector2(20f, 15f) };
        layer.AddChild(_hudLabel);
        (_resultPanel, _resultLabel) = BuildResultPanel(layer);
    }

    public Button ConnectButton { get; private set; } = null!;

    public LineEdit HostInput { get; private set; } = null!;

    public LineEdit NameInput { get; private set; } = null!;

    public OptionButton PlayerCountSelector { get; private set; } = null!;

    public Button ReadyButton { get; private set; } = null!;

    public OptionButton ShipSelector { get; private set; } = null!;

    public void SetConnectError(string message)
    {
        ConnectButton.Disabled = false;
        ConnectButton.Text = message;
    }

    public void SetConnecting()
    {
        ConnectButton.Disabled = true;
        ConnectButton.Text = "Connecting...";
    }

    public void SetHud(string text)
    {
        _hudLabel.Text = text;
    }

    public void SetReady(bool ready)
    {
        ReadyButton.Text = ready ? "Cancel ready" : "Ready";
    }

    public void ShowConnection()
    {
        SetVisibility(connection: true, lobby: false, result: false, hud: false);
    }

    public void ShowGame()
    {
        SetVisibility(connection: false, lobby: false, result: false, hud: true);
    }

    public void ShowLobby()
    {
        SetVisibility(connection: false, lobby: true, result: false, hud: false);
    }

    public void ShowLobbyState(LobbyStatePayload lobby)
    {
        string players = string.Join('\n', lobby.Participants.Select(ParticipantLine));
        string countdown = lobby.CountdownSeconds is null
            ? string.Empty
            : $"\nStarting in {lobby.CountdownSeconds}...";
        _lobbyLabel.Text =
            $"Players {lobby.Participants.Length}/{lobby.RequiredPlayers}\n{players}{countdown}";
    }

    public void ShowResult(MatchEndedPayload result)
    {
        SetVisibility(connection: false, lobby: false, result: true, hud: false);
        string ranking = string.Join('\n', result.Ranking.Select(RankingLine));
        string records = FormatRecords(result.Records);
        _resultLabel.Text = $"{(result.Victory ? "VICTORY" : "DEFEAT")}\n{ranking}{records}";
    }

    private static string FormatRecords(ScoreEntry[]? records)
    {
        return records is { Length: > 0 }
            ? $"\n\nRECORDS\n{string.Join('\n', records.Select(RankingLine))}"
            : string.Empty;
    }

    private static string ParticipantLine(LobbyParticipant participant)
    {
        string status = participant.Ready ? "READY" : "waiting";
        return $"{participant.PlayerName} - {participant.ShipId} - {status}";
    }

    private static string RankingLine(ScoreEntry entry, int index)
    {
        return $"{index + 1}. {entry.PlayerName}: {entry.Score}";
    }

    private VBoxContainer BuildConnectionPanel(CanvasLayer layer)
    {
        VBoxContainer panel = CreatePanel();
        HostInput = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Server IP" };
        NameInput = new LineEdit
        {
            Text = System.Environment.MachineName,
            PlaceholderText = "Player name",
            MaxLength = 32,
        };
        PlayerCountSelector = CreatePlayerCountSelector();
        ConnectButton = new Button { Text = "Connect" };
        AddConnectionControls(panel);
        layer.AddChild(panel);
        return panel;
    }

    private static VBoxContainer BuildLobbyPanel(CanvasLayer layer)
    {
        VBoxContainer panel = CreatePanel();
        panel.AddChild(new Label { Text = "LOBBY" });
        layer.AddChild(panel);
        return panel;
    }

    private static (VBoxContainer Panel, Label Label) BuildResultPanel(CanvasLayer layer)
    {
        VBoxContainer panel = CreatePanel();
        Label label = new();
        panel.AddChild(new Label { Text = "MATCH RESULT" });
        panel.AddChild(label);
        layer.AddChild(panel);
        return (panel, label);
    }

    private void AddConnectionControls(VBoxContainer panel)
    {
        panel.AddChild(new Label { Text = "SPACE SHOOTER LAN" });
        panel.AddChild(HostInput);
        panel.AddChild(NameInput);
        panel.AddChild(new Label { Text = "Match size" });
        panel.AddChild(PlayerCountSelector);
        panel.AddChild(ConnectButton);
    }

    private void AddLobbyControls()
    {
        ShipSelector = new OptionButton();
        foreach (string ship in new[] { "blue", "red", "green", "gold" })
        {
            ShipSelector.AddItem(ship);
        }

        ReadyButton = new Button { Text = "Ready" };
        _lobbyPanel.AddChild(ShipSelector);
        _lobbyPanel.AddChild(ReadyButton);
    }

    private static OptionButton CreatePlayerCountSelector()
    {
        OptionButton selector = new();
        for (int count = 1; count <= 4; count++)
        {
            selector.AddItem($"{count} player{(count == 1 ? string.Empty : "s")}", count);
        }

        return selector;
    }

    private static VBoxContainer CreatePanel()
    {
        return new VBoxContainer
        {
            Position = new Vector2(40f, 40f),
            Size = new Vector2(420f, 400f),
        };
    }

    private void SetVisibility(bool connection, bool lobby, bool result, bool hud)
    {
        _connectionPanel.Visible = connection;
        _lobbyPanel.Visible = lobby;
        _resultPanel.Visible = result;
        _hudLabel.Visible = hud;
    }
}
