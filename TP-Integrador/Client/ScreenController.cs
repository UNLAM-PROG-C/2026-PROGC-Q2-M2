using System.Diagnostics.CodeAnalysis;
using Godot;
using SpaceShooter.GameDomain;
using SpaceShooter.Protocol;

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
    private static readonly string[] ShipIds = ["blue", "red", "green", "gold"];

    private readonly Control _connectionScreen;
    private readonly Label _connectionStatusLabel;
    private readonly Control _hudPanel;
    private readonly Label _hudLabel;
    private readonly Control _lobbyScreen;
    private readonly Label _lobbyLabel;
    private readonly Control _pauseScreen;
    private readonly ColorRect _pauseShade;
    private readonly Label _rematchStatusLabel;
    private readonly Label _recordsLabel;
    private readonly Control _resultScreen;
    private readonly Label _rankingLabel;
    private readonly Label _resultSubtitle;
    private readonly Label _resultTitle;
    private readonly ColorRect _screenShade;
    private readonly Label _waveBonusLabel;
    private readonly Control _waveTransitionScreen;
    private readonly ColorRect _waveTransitionShade;
    private readonly Label _waveTransitionSubtitle;
    private readonly Label _waveTransitionTimer;
    private readonly Label _waveTransitionTitle;

    public ScreenController(Node owner)
    {
        CanvasLayer layer = new();
        owner.AddChild(layer);

        _screenShade = new ColorRect
        {
            Color = new Color(0.005f, 0.015f, 0.04f, 0.62f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        FillParent(_screenShade);
        layer.AddChild(_screenShade);

        (_connectionScreen, _connectionStatusLabel) = BuildConnectionScreen(layer);
        (_lobbyScreen, _lobbyLabel) = BuildLobbyScreen(layer);
        (_resultScreen, _resultTitle, _resultSubtitle, _rankingLabel, _recordsLabel, _rematchStatusLabel) =
            BuildResultScreen(layer);
        (_hudPanel, _hudLabel) = BuildHud(layer);
        (_waveTransitionScreen, _waveTransitionShade, _waveTransitionTitle, _waveTransitionSubtitle,
            _waveBonusLabel, _waveTransitionTimer) = BuildWaveTransition(layer);
        (_pauseScreen, _pauseShade, ResumeButton, PauseExitButton) = BuildPauseScreen(layer);
    }

    public Button ConnectButton { get; private set; } = null!;

    public Button ExitButton { get; private set; } = null!;

    public LineEdit HostInput { get; private set; } = null!;

    public LineEdit NameInput { get; private set; } = null!;

    public OptionButton PlayerCountSelector { get; private set; } = null!;

    public Button ReadyButton { get; private set; } = null!;

    public Button LobbyExitButton { get; private set; } = null!;

    public Button PauseExitButton { get; private set; } = null!;

    public Button ReplayButton { get; private set; } = null!;

    public Button ResumeButton { get; private set; } = null!;

    public OptionButton ShipSelector { get; private set; } = null!;

    public static string GetShipId(long index)
    {
        return index >= 0 && index < ShipIds.Length ? ShipIds[index] : ShipIds[0];
    }

    public void SetConnectError(string message)
    {
        ConnectButton.Disabled = false;
        ConnectButton.Text = "CONECTARSE";
        _connectionStatusLabel.Text = message;
        _connectionStatusLabel.AddThemeColorOverride("font_color", InterfaceStyles.Danger);
    }

    public void SetConnecting()
    {
        ConnectButton.Disabled = true;
        ConnectButton.Text = "CONECTANDO...";
        _connectionStatusLabel.Text = "Estableciendo conexión con el servidor";
        _connectionStatusLabel.AddThemeColorOverride("font_color", InterfaceStyles.Accent);
    }

    public void SetHud(string text)
    {
        _hudLabel.Text = text;
    }

    public void SetWaveTransition(int wave, int remainingMilliseconds, int completionBonus)
    {
        bool visible = remainingMilliseconds > 0;
        _waveTransitionScreen.Visible = visible;
        _waveTransitionShade.Visible = visible;
        if (!visible)
        {
            return;
        }

        int seconds = Math.Max(1, (int)Math.Ceiling(remainingMilliseconds / 1000d));
        _waveTransitionTitle.Text = $"FASE {wave}";
        _waveTransitionSubtitle.Text = wave == 10
            ? "EL ENEMIGO FINAL SE ACERCA"
            : "¡PREPÁRATE!";
        _waveBonusLabel.Text = completionBonus > 0
            ? $"BONIFICACIÓN DE FASE  +{completionBonus} PTS"
            : "LA MISIÓN COMIENZA";
        _waveTransitionTimer.Text = $"COMENZANDO EN {seconds}...";
    }

    public void SetLeaving()
    {
        ReplayButton.Disabled = true;
        ExitButton.Disabled = true;
        _rematchStatusLabel.Text = "Cerrando la conexión...";
    }

    public void SetReady(bool ready)
    {
        ReadyButton.Text = ready ? "CANCELAR LISTO" : "ESTOY LISTO";
        LobbyExitButton.Disabled = ready;
    }

    public void ShowPause(bool canResume)
    {
        _pauseShade.Visible = true;
        _pauseScreen.Visible = true;
        ResumeButton.Disabled = !canResume;
        ResumeButton.Text = canResume ? "REANUDAR" : "ESPERANDO AL JUGADOR QUE PAUSÓ";
        if (canResume)
        {
            ResumeButton.GrabFocus();
        }
    }

    public void HidePause()
    {
        _pauseScreen.Visible = false;
        _pauseShade.Visible = false;
    }

    public void SetRematchReady(bool ready)
    {
        ReplayButton.Text = ready ? "CANCELAR REVANCHA" : "SÍ, REVANCHA";
        _rematchStatusLabel.Text = ready
            ? "Estás listo. Esperando la decisión de los demás pilotos..."
            : "Elegí si querés disputar una nueva partida.";
        _rematchStatusLabel.AddThemeColorOverride(
            "font_color",
            ready ? InterfaceStyles.Success : InterfaceStyles.MutedText);
    }

    public void ShowConnection()
    {
        ConnectButton.Disabled = false;
        ConnectButton.Text = "CONECTARSE";
        _connectionStatusLabel.Text = string.Empty;
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
            : $"\n\nTODOS LISTOS · COMENZANDO EN {lobby.CountdownSeconds}...";
        _lobbyLabel.Text = $"PILOTOS {lobby.Participants.Length}/{lobby.RequiredPlayers}\n\n{players}{countdown}";

        if (_resultScreen.Visible)
        {
            int readyPlayers = lobby.Participants.Count(participant => participant.Ready);
            string decisions = string.Join('\n', lobby.Participants.Select(RematchParticipantLine));
            string rematchCountdown = lobby.CountdownSeconds is null
                ? $"Esperando jugadores: {readyPlayers}/{lobby.RequiredPlayers}"
                : $"Todos están listos · Nueva partida en {lobby.CountdownSeconds}...";
            _rematchStatusLabel.Text = $"{decisions}\n\n{rematchCountdown}";
        }
    }

  public void ShowResult(MatchEndedPayload result, PlayerId? localPlayerId)
  {
    SetVisibility(connection: false, lobby: false, result: true, hud: false);
    SetResultHeader(result.Victory);
    _rankingLabel.Text = FormatRanking(result.Ranking, localPlayerId);
    _recordsLabel.Text = FormatRecords(result.Records);
    ReplayButton.Disabled = false;
    ExitButton.Disabled = false;
    SetRematchReady(ready: false);
  }

  private void SetResultHeader(bool victory)
  {
    _resultTitle.Text = victory ? "¡VICTORIA!" : "MISIÓN FALLIDA";
    Color color = victory ? InterfaceStyles.Success : InterfaceStyles.Danger;
    _resultTitle.AddThemeColorOverride("font_color", color);
    _resultSubtitle.Text = victory
        ? "LA GALAXIA ESTÁ A SALVO"
        : "LA FLOTA ENEMIGA SUPERÓ AL EQUIPO";
  }

  private static string FormatRanking(ScoreEntry[] ranking, PlayerId? localPlayerId)
  {
    return string.Join(
        '\n',
        ranking.Select((entry, index) => RankingLine(entry, index, localPlayerId)));
  }

  private static string FormatRecords(ScoreEntry[]? records)
  {
    return records is { Length: > 0 }
        ? string.Join('\n', records.Take(5).Select(
            (entry, index) => RankingLine(entry, index, localPlayerId: null)))
        : "Todavía no hay récords registrados.";
  }

    private static string ParticipantLine(LobbyParticipant participant)
    {
        string status = participant.Ready ? "LISTO" : "ESPERANDO";
        return $"{participant.PlayerName,-20}  NAVE {participant.ShipId.ToUpperInvariant(),-7}  {status}";
    }

    private static string RankingLine(ScoreEntry entry, int index, PlayerId? localPlayerId)
    {
        string local = entry.PlayerId == localPlayerId ? "  ← VOS" : string.Empty;
        return $"{index + 1}.  {entry.PlayerName,-22}  {entry.Score,6} PTS{local}";
    }

    private static string RematchParticipantLine(LobbyParticipant participant)
    {
        return participant.Ready
            ? $"LISTO       {participant.PlayerName}"
            : $"DECIDIENDO  {participant.PlayerName}";
    }

  private (Control Screen, Label Status) BuildConnectionScreen(CanvasLayer layer)
  {
    (Control screen, VBoxContainer content) = CreateScreen(
        layer,
        "Connection",
        new Vector2(660f, 610f));
    AddHeader(content, "SPACE SHOOTER LAN", "DEFENDÉ LA GALAXIA EN EQUIPO");
    AddConnectionFields(content);
    AddMatchSelector(content);
    AddConnectButton(content);
    Label status = AddConnectionStatus(content);
    AddControlsHelp(content);
    return (screen, status);
  }

  private void AddConnectionFields(VBoxContainer content)
  {
    VBoxContainer connection = AddSection(content, "CONEXIÓN", "ConnectionSection");
    connection.AddChild(CreateFieldLabel("IP DEL SERVIDOR"));
    HostInput = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Dirección IPv4" };
    InterfaceStyles.ApplyField(HostInput);
    connection.AddChild(HostInput);
    connection.AddChild(CreateFieldLabel("NOMBRE DEL PILOTO"));
    NameInput = CreateNameInput();
    connection.AddChild(NameInput);
  }

  private static LineEdit CreateNameInput()
  {
    LineEdit input = new()
    {
      Text = System.Environment.MachineName,
      PlaceholderText = "Tu nombre",
      MaxLength = 32,
    };
    InterfaceStyles.ApplyField(input);
    return input;
  }

  private void AddMatchSelector(VBoxContainer content)
  {
    VBoxContainer match = AddSection(content, "TIPO DE PARTIDA", "Match");
    PlayerCountSelector = CreatePlayerCountSelector();
    match.AddChild(PlayerCountSelector);
  }

  private void AddConnectButton(VBoxContainer content)
  {
    ConnectButton = new Button { Text = "CONECTARSE" };
    InterfaceStyles.ApplyButton(ConnectButton, primary: true);
    content.AddChild(Center(ConnectButton));
  }

  private static Label AddConnectionStatus(VBoxContainer content)
  {
    Label status = new()
    {
      HorizontalAlignment = HorizontalAlignment.Center,
      AutowrapMode = TextServer.AutowrapMode.WordSmart,
      CustomMinimumSize = new Vector2(0f, 44f),
    };
    InterfaceStyles.ApplyLabel(status, 14, InterfaceStyles.MutedText);
    content.AddChild(status);
    return status;
  }

  private static void AddControlsHelp(VBoxContainer content)
  {
    Label controls = new()
    {
      Text = "MOVIMIENTO: FLECHAS  ·  DISPARO: ESPACIO  ·  MENÚ: ESC",
      HorizontalAlignment = HorizontalAlignment.Center,
    };
    InterfaceStyles.ApplyLabel(controls, 13, InterfaceStyles.MutedText);
    content.AddChild(controls);
  }

  private (Control Screen, Label Lobby) BuildLobbyScreen(CanvasLayer layer)
  {
    (Control screen, VBoxContainer content) = CreateScreen(
        layer,
        "Lobby",
        new Vector2(760f, 590f));
    AddHeader(content, "SALA DE ESPERA", "PREPARÁ TU NAVE PARA LA MISIÓN");
    Label lobby = AddLobbyParticipants(content);
    AddShipSelector(content);
    AddReadyButton(content);
    AddLobbyExitButton(content);
    return (screen, lobby);
  }

  private static Label AddLobbyParticipants(VBoxContainer content)
  {
    VBoxContainer pilots = AddSection(content, "PILOTOS CONECTADOS", "Pilots");
    Label lobby = new();
    InterfaceStyles.ApplyLabel(lobby, 16);
    pilots.AddChild(lobby);
    return lobby;
  }

  private void AddShipSelector(VBoxContainer content)
  {
    VBoxContainer ship = AddSection(content, "ELEGÍ TU NAVE", "Ship");
    ShipSelector = new OptionButton();
    foreach (string shipId in ShipIds)
    {
      ShipSelector.AddIconItem(AssetCatalog.GetShip(shipId), ShipDisplayName(shipId));
    }

    InterfaceStyles.ApplyOptionButton(ShipSelector);
    ship.AddChild(ShipSelector);
  }

  private void AddReadyButton(VBoxContainer content)
  {
    ReadyButton = new Button { Text = "ESTOY LISTO" };
    InterfaceStyles.ApplyButton(ReadyButton, primary: true);
    content.AddChild(Center(ReadyButton));
  }

  private void AddLobbyExitButton(VBoxContainer content)
  {
    LobbyExitButton = new Button { Text = "DESCONECTARSE" };
    InterfaceStyles.ApplyButton(LobbyExitButton);
    content.AddChild(Center(LobbyExitButton));
  }

  private (Control Screen, Label Title, Label Subtitle, Label Ranking, Label Records, Label Rematch)
      BuildResultScreen(CanvasLayer layer)
  {
    (Control screen, VBoxContainer content) = CreateScreen(layer, "Result", new Vector2(790f, 680f));
    Label title = AddHeader(content, "RESULTADO", "FIN DE LA PARTIDA");
    Label subtitle = content.GetNode<Label>("Header/Subtitle");
    Label ranking = AddResultSection(content, "CLASIFICACIÓN", "Ranking", InterfaceStyles.Text);
    Label records = AddResultSection(content, "MEJORES PUNTAJES", "Records", InterfaceStyles.MutedText);
    Label rematch = AddRematchQuestion(content);
    AddResultActions(content);
    return (screen, title, subtitle, ranking, records, rematch);
  }

  private static Label AddResultSection(
      VBoxContainer content,
      string title,
      string name,
      Color color)
  {
    VBoxContainer section = AddSection(content, title, name);
    Label text = new() { Name = "Text" };
    InterfaceStyles.ApplyLabel(text, 15, color);
    section.AddChild(text);
    return text;
  }

  private static Label AddRematchQuestion(VBoxContainer content)
  {
    Label question = new()
    {
      Text = "¿QUIEREN VOLVER A JUGAR?",
      HorizontalAlignment = HorizontalAlignment.Center,
    };
    InterfaceStyles.ApplyLabel(question, 19, InterfaceStyles.Accent);
    content.AddChild(question);
    Label status = CreateRematchStatus();
    content.AddChild(status);
    return status;
  }

  private static Label CreateRematchStatus()
  {
    Label status = new()
    {
      HorizontalAlignment = HorizontalAlignment.Center,
      CustomMinimumSize = new Vector2(0f, 42f),
    };
    InterfaceStyles.ApplyLabel(status, 14, InterfaceStyles.MutedText);
    return status;
  }

  private void AddResultActions(VBoxContainer content)
  {
    ReplayButton = new Button { Text = "SÍ, REVANCHA" };
    ExitButton = new Button { Text = "NO, VOLVER AL INICIO" };
    InterfaceStyles.ApplyButton(ReplayButton, primary: true);
    InterfaceStyles.ApplyButton(ExitButton);
    HBoxContainer actions = new() { Alignment = BoxContainer.AlignmentMode.Center };
    actions.AddThemeConstantOverride("separation", 18);
    actions.AddChild(ReplayButton);
    actions.AddChild(ExitButton);
    content.AddChild(actions);
  }

    private static (Control Panel, Label Label) BuildHud(CanvasLayer layer)
    {
        PanelContainer panel = new()
        {
            Position = new Vector2(18f, 16f),
            CustomMinimumSize = new Vector2(620f, 48f),
        };
        InterfaceStyles.ApplySection(panel, InterfaceStyles.Accent);
        Label label = new();
        InterfaceStyles.ApplyLabel(label, 14);
        panel.AddChild(label);
        layer.AddChild(panel);
        return (panel, label);
    }

  private static (Control Screen, ColorRect Shade, Label Title, Label Subtitle, Label Bonus, Label Timer)
      BuildWaveTransition(CanvasLayer layer)
  {
    ColorRect shade = CreateTransitionShade(layer);
    (Control screen, VBoxContainer content) = CreateScreen(
        layer,
        "WaveTransition",
        new Vector2(560f, 320f));
    screen.MouseFilter = Control.MouseFilterEnum.Ignore;
    (Label title, Label subtitle, Label bonus, Label timer) = AddTransitionContent(content);
    screen.Visible = false;
    shade.Visible = false;
    return (screen, shade, title, subtitle, bonus, timer);
  }

  private static ColorRect CreateTransitionShade(CanvasLayer layer)
  {
    ColorRect shade = new()
    {
      Color = new Color(0.005f, 0.015f, 0.04f, 0.78f),
      MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    FillParent(shade);
    layer.AddChild(shade);
    return shade;
  }

  private static (Label Title, Label Subtitle, Label Bonus, Label Timer) AddTransitionContent(
      VBoxContainer content)
  {
    Label title = AddTransitionTitle(content);
    Label subtitle = AddTransitionLabel(content, 23, InterfaceStyles.Text);
    HSeparator separator = new();
    separator.AddThemeConstantOverride("separation", 12);
    content.AddChild(separator);
    Label bonus = AddTransitionLabel(content, 17, InterfaceStyles.Success);
    Label timer = AddTransitionLabel(content, 15, InterfaceStyles.MutedText);
    return (title, subtitle, bonus, timer);
  }

  private static Label AddTransitionTitle(VBoxContainer content)
  {
    Label title = new();
    InterfaceStyles.ApplyTitle(title, InterfaceStyles.Accent);
    title.AddThemeFontSizeOverride("font_size", 48);
    content.AddChild(title);
    return title;
  }

  private static Label AddTransitionLabel(VBoxContainer content, int size, Color color)
  {
    Label label = new() { HorizontalAlignment = HorizontalAlignment.Center };
    InterfaceStyles.ApplyLabel(label, size, color);
    content.AddChild(label);
    return label;
  }

  private static (Control Screen, ColorRect Shade, Button Resume, Button Exit)
      BuildPauseScreen(CanvasLayer layer)
  {
    ColorRect shade = new()
    {
      Color = new Color(0.005f, 0.015f, 0.04f, 0.78f),
    };
    FillParent(shade);
    layer.AddChild(shade);

    (Control screen, VBoxContainer content) = CreateScreen(
        layer,
        "Pause",
        new Vector2(560f, 360f));
    AddHeader(content, "PAUSA", "LA MISIÓN ESTÁ EN CURSO");

    Button resume = new() { Text = "REANUDAR" };
    InterfaceStyles.ApplyButton(resume, primary: true);
    content.AddChild(Center(resume));

    Button exit = new() { Text = "SALIR DE LA PARTIDA" };
    InterfaceStyles.ApplyButton(exit);
    content.AddChild(Center(exit));

    screen.Visible = false;
    shade.Visible = false;
    return (screen, shade, resume, exit);
  }

  private static (Control Screen, VBoxContainer Content) CreateScreen(
      CanvasLayer layer,
      string name,
      Vector2 minimumSize)
  {
    CenterContainer screen = new() { Name = name };
    FillParent(screen);
    PanelContainer panel = CreateScreenPanel(minimumSize);
    VBoxContainer content = new() { Name = "Content" };
    content.AddThemeConstantOverride("separation", 14);
    panel.AddChild(CreateScreenMargin(content));
    screen.AddChild(panel);
    layer.AddChild(screen);
    return (screen, content);
  }

  private static PanelContainer CreateScreenPanel(Vector2 minimumSize)
  {
    PanelContainer panel = new() { Name = "Panel", CustomMinimumSize = minimumSize };
    InterfaceStyles.ApplyPanel(panel);
    return panel;
  }

  private static MarginContainer CreateScreenMargin(Control content)
  {
    MarginContainer margin = new() { Name = "Margin" };
    margin.AddThemeConstantOverride("margin_left", 34);
    margin.AddThemeConstantOverride("margin_top", 26);
    margin.AddThemeConstantOverride("margin_right", 34);
    margin.AddThemeConstantOverride("margin_bottom", 26);
    margin.AddChild(content);
    return margin;
  }

    private static Label AddHeader(VBoxContainer parent, string titleText, string subtitleText)
    {
        VBoxContainer header = new() { Name = "Header" };
        header.AddThemeConstantOverride("separation", 3);
        Label title = new() { Name = "Title", Text = titleText };
        InterfaceStyles.ApplyTitle(title);
        header.AddChild(title);
        Label subtitle = new()
        {
            Name = "Subtitle",
            Text = subtitleText,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        InterfaceStyles.ApplyLabel(subtitle, 14, InterfaceStyles.MutedText);
        header.AddChild(subtitle);
        parent.AddChild(header);
        return title;
    }

    private static VBoxContainer AddSection(VBoxContainer parent, string title, string name)
    {
        PanelContainer panel = new() { Name = name };
        InterfaceStyles.ApplySection(panel);
        VBoxContainer box = new() { Name = "Box" };
        box.AddThemeConstantOverride("separation", 8);
        Label heading = new() { Text = title };
        InterfaceStyles.ApplyLabel(heading, 15, InterfaceStyles.Accent);
        box.AddChild(heading);
        panel.AddChild(box);
        parent.AddChild(panel);
        return box;
    }

    private static CenterContainer Center(Control child)
    {
        CenterContainer container = new();
        container.AddChild(child);
        return container;
    }

    private static Label CreateFieldLabel(string text)
    {
        Label label = new() { Text = text };
        InterfaceStyles.ApplyLabel(label, 13, InterfaceStyles.MutedText);
        return label;
    }

    private static OptionButton CreatePlayerCountSelector()
    {
        OptionButton selector = new();
        for (int count = 1; count <= 4; count++)
        {
            selector.AddItem($"{count} JUGADOR{(count == 1 ? string.Empty : "ES")}", count);
        }

        InterfaceStyles.ApplyOptionButton(selector);
        return selector;
    }

    private static void FillParent(Control control)
    {
        control.AnchorRight = 1f;
        control.AnchorBottom = 1f;
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }

    private static string ShipDisplayName(string shipId)
    {
        return shipId switch
        {
            "blue" => "AZUL",
            "red" => "ROJA",
            "green" => "VERDE",
            "gold" => "DORADA",
            _ => shipId.ToUpperInvariant(),
        };
    }

    private void SetVisibility(bool connection, bool lobby, bool result, bool hud)
    {
        _connectionScreen.Visible = connection;
        _lobbyScreen.Visible = lobby;
        _resultScreen.Visible = result;
        _hudPanel.Visible = hud;
        _screenShade.Visible = !hud;
        _waveTransitionScreen.Visible = false;
        _waveTransitionShade.Visible = false;
        _pauseScreen.Visible = false;
        _pauseShade.Visible = false;
    }
}
