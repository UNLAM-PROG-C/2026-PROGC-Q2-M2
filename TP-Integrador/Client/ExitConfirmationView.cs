using Godot;

namespace SpaceShooter.Client;

/// <summary>
/// Presents a modal confirmation before leaving an active match.
/// </summary>
internal sealed class ExitConfirmationView
{
  private readonly Control _root;

  public ExitConfirmationView(Node owner)
  {
    CanvasLayer layer = new();
    owner.AddChild(layer);
    AddShade(layer);
    (_root, ContinueButton, ExitButton) = BuildDialog(layer);
    Hide();
  }

  public Button ContinueButton { get; }

  public Button ExitButton { get; }

  public bool IsVisible => _root.Visible;

  public void Hide()
  {
    _root.Visible = false;
    _root.GetParent<CanvasLayer>().Visible = false;
  }

  public void Show()
  {
    _root.GetParent<CanvasLayer>().Visible = true;
    _root.Visible = true;
    ContinueButton.GrabFocus();
  }

  public void Toggle()
  {
    if (IsVisible)
    {
      Hide();
    }
    else
    {
      Show();
    }
  }

  private static void AddShade(CanvasLayer layer)
  {
    ColorRect shade = new()
    {
      Color = new Color(0.005f, 0.015f, 0.04f, 0.82f),
      MouseFilter = Control.MouseFilterEnum.Stop,
    };
    FillParent(shade);
    layer.AddChild(shade);
  }

  private static (Control Root, Button Continue, Button Exit) BuildDialog(CanvasLayer layer)
  {
    CenterContainer root = new();
    FillParent(root);
    PanelContainer panel = new() { CustomMinimumSize = new Vector2(520f, 260f) };
    InterfaceStyles.ApplyPanel(panel);
    VBoxContainer content = BuildContent();
    (Button continueButton, Button exitButton) = AddActions(content);
    panel.AddChild(AddMargin(content));
    root.AddChild(panel);
    layer.AddChild(root);
    return (root, continueButton, exitButton);
  }

  private static VBoxContainer BuildContent()
  {
    VBoxContainer content = new();
    content.AddThemeConstantOverride("separation", 20);
    Label title = new() { Text = "¿SALIR DE LA PARTIDA?" };
    InterfaceStyles.ApplyTitle(title, InterfaceStyles.Danger);
    Label detail = new()
    {
      Text = "La partida continuará para los demás pilotos.",
      HorizontalAlignment = HorizontalAlignment.Center,
    };
    InterfaceStyles.ApplyLabel(detail, 16, InterfaceStyles.MutedText);
    content.AddChild(title);
    content.AddChild(detail);
    return content;
  }

  private static (Button Continue, Button Exit) AddActions(VBoxContainer content)
  {
    Button continueButton = new() { Text = "CONTINUAR" };
    Button exitButton = new() { Text = "SALIR DE LA PARTIDA" };
    InterfaceStyles.ApplyButton(continueButton, primary: true);
    InterfaceStyles.ApplyButton(exitButton);
    HBoxContainer actions = new() { Alignment = BoxContainer.AlignmentMode.Center };
    actions.AddThemeConstantOverride("separation", 18);
    actions.AddChild(continueButton);
    actions.AddChild(exitButton);
    content.AddChild(actions);
    return (continueButton, exitButton);
  }

  private static MarginContainer AddMargin(Control content)
  {
    MarginContainer margin = new();
    margin.AddThemeConstantOverride("margin_left", 32);
    margin.AddThemeConstantOverride("margin_top", 28);
    margin.AddThemeConstantOverride("margin_right", 32);
    margin.AddThemeConstantOverride("margin_bottom", 28);
    margin.AddChild(content);
    return margin;
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
}
