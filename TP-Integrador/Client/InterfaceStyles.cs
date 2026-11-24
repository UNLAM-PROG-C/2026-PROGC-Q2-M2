using Godot;

namespace SpaceShooter.Client;

/// <summary>
/// Centralizes the visual language used by every client screen.
/// </summary>
internal static class InterfaceStyles
{
    public static readonly Color Accent = new("5de4ff");
    public static readonly Color Danger = new("ff6b70");
    public static readonly Color MutedText = new("aabbd0");
    public static readonly Color Success = new("74f5a7");
    public static readonly Color Text = new("f4f8ff");

    private static readonly Color ButtonBackground = new("17304f");
    private static readonly Color ButtonHover = new("234d73");
    private static readonly Color FieldBackground = new("0a1729");
    private static readonly Color PanelBackground = new(0.035f, 0.075f, 0.14f, 0.96f);
    private static readonly Color SectionBackground = new(0.055f, 0.12f, 0.21f, 0.92f);

    public static void ApplyButton(Button button, bool primary = false)
    {
        Color normal = primary ? new Color("146887") : ButtonBackground;
        Color hover = primary ? new Color("1c88aa") : ButtonHover;
        button.AddThemeFontOverride("font", AssetCatalog.InterfaceFont);
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeStyleboxOverride("normal", CreateBox(normal, primary ? Accent : new Color("31506f"), 8));
        button.AddThemeStyleboxOverride("hover", CreateBox(hover, Accent, 8));
        button.AddThemeStyleboxOverride("pressed", CreateBox(new Color("0e415c"), Accent, 8));
        button.AddThemeStyleboxOverride("disabled", CreateBox(new Color("172131"), new Color("344052"), 8));
        button.CustomMinimumSize = new Vector2(190f, 48f);
    }

    public static void ApplyField(LineEdit field)
    {
        field.AddThemeFontOverride("font", AssetCatalog.InterfaceFont);
        field.AddThemeFontSizeOverride("font_size", 16);
        field.AddThemeColorOverride("font_color", Text);
        field.AddThemeColorOverride("font_placeholder_color", MutedText);
        field.AddThemeStyleboxOverride("normal", CreateBox(FieldBackground, new Color("365875"), 7));
        field.AddThemeStyleboxOverride("focus", CreateBox(FieldBackground, Accent, 7));
        field.CustomMinimumSize = new Vector2(0f, 46f);
    }

    public static void ApplyLabel(Label label, int size = 16, Color? color = null)
    {
        label.AddThemeFontOverride("font", AssetCatalog.InterfaceFont);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Text);
    }

    public static void ApplyOptionButton(OptionButton selector)
    {
        selector.AddThemeFontOverride("font", AssetCatalog.InterfaceFont);
        selector.AddThemeFontSizeOverride("font_size", 16);
        selector.AddThemeColorOverride("font_color", Text);
        selector.AddThemeStyleboxOverride("normal", CreateBox(FieldBackground, new Color("365875"), 7));
        selector.AddThemeStyleboxOverride("hover", CreateBox(ButtonHover, Accent, 7));
        selector.CustomMinimumSize = new Vector2(0f, 46f);
    }

    public static void ApplyPanel(PanelContainer panel)
    {
        StyleBoxFlat style = CreateBox(PanelBackground, new Color("2d6688"), 14);
        style.ShadowColor = new Color(0f, 0f, 0f, 0.55f);
        style.ShadowSize = 18;
        panel.AddThemeStyleboxOverride("panel", style);
    }

    public static void ApplySection(PanelContainer panel, Color? border = null)
    {
        panel.AddThemeStyleboxOverride("panel", CreateBox(
            SectionBackground,
            border ?? new Color("294c69"),
            9));
    }

    public static void ApplyTitle(Label label, Color? color = null)
    {
        ApplyLabel(label, 34, color ?? Accent);
        label.HorizontalAlignment = HorizontalAlignment.Center;
    }

  private static StyleBoxFlat CreateBox(Color background, Color border, int radius)
  {
    StyleBoxFlat box = new() { BgColor = background, BorderColor = border };
    SetBorder(box);
    SetCorners(box, radius);
    SetMargins(box);
    return box;
  }

  private static void SetBorder(StyleBoxFlat box)
  {
    box.BorderWidthLeft = 2;
    box.BorderWidthTop = 2;
    box.BorderWidthRight = 2;
    box.BorderWidthBottom = 2;
  }

  private static void SetCorners(StyleBoxFlat box, int radius)
  {
    box.CornerRadiusTopLeft = radius;
    box.CornerRadiusTopRight = radius;
    box.CornerRadiusBottomLeft = radius;
    box.CornerRadiusBottomRight = radius;
  }

  private static void SetMargins(StyleBoxFlat box)
  {
    box.ContentMarginLeft = 14f;
    box.ContentMarginTop = 10f;
    box.ContentMarginRight = 14f;
    box.ContentMarginBottom = 10f;
  }
}
