// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using Godot;

namespace FiringSolution.Shell;

/// <summary>
/// A station's colour language. The two visual directions from the study:
/// amber-phosphor gunnery (kinetic) and cool ice-glass surveillance (beam).
/// </summary>
public sealed class Palette
{
    public Color Bg, Panel, PanelDeep, Accent, AccentDim, Red, Text, TextDim, Faint, Border, BorderSoft;

    public static readonly Palette Amber = new()
    {
        Bg = new Color("1c1812"),
        Panel = new Color("0e0c0a"),
        PanelDeep = new Color("0a0907"),
        Accent = new Color("f4ad3c"),
        AccentDim = new Color("b9892f"),
        Red = new Color("e24a30"),
        Text = new Color("ece3d0"),
        TextDim = new Color("9a8f78"),
        Faint = new Color("6b6151"),
        Border = new Color("2c2519"),
        BorderSoft = new Color("221d15"),
    };

    public static readonly Palette Ice = new()
    {
        Bg = new Color("161e26"),
        Panel = new Color("0b0f14"),
        PanelDeep = new Color("080c10"),
        Accent = new Color("3cc6e8"),
        AccentDim = new Color("5fb8d0"),
        Red = new Color("ff5a47"),
        Text = new Color("dce8f2"),
        TextDim = new Color("8195a3"),
        Faint = new Color("5d6b78"),
        Border = new Color("243039"),
        BorderSoft = new Color("16202a"),
    };
}

/// <summary>Factory helpers for the retained-mode instrument chrome.</summary>
public static class Ui
{
    public static Label Text(string text, Color color, int size = 11, bool bold = false)
    {
        var l = new Label { Text = text };
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeFontSizeOverride("font_size", size);
        return l;
    }

    /// <summary>A bordered panel with interior padding, used for every readout box.</summary>
    public static PanelContainer Panel(Color bg, Color border, int pad = 10, int borderW = 1)
    {
        var p = new PanelContainer();
        var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        sb.SetBorderWidthAll(borderW);
        sb.ContentMarginLeft = pad;
        sb.ContentMarginRight = pad;
        sb.ContentMarginTop = pad;
        sb.ContentMarginBottom = pad;
        p.AddThemeStyleboxOverride("panel", sb);
        return p;
    }

    /// <summary>A small labelled metric cell: caption above value (the grid tiles).</summary>
    public static Control MetricCell(Palette p, string caption, string value, Color valueColor, int valueSize = 15)
    {
        var box = Panel(p.PanelDeep, p.BorderSoft, pad: 9, borderW: 1);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 3);
        v.AddChild(Text(caption, p.Faint, 9));
        v.AddChild(Text(value, valueColor, valueSize));
        box.AddChild(v);
        return box;
    }

    /// <summary>Section header: bullet + caption + rule, as in the study.</summary>
    public static Control SectionHeader(Palette p, string caption, Color bullet, string? tag = null)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 8);
        var dot = new ColorRect { Color = bullet, CustomMinimumSize = new Vector2(5, 5) };
        dot.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        h.AddChild(dot);
        h.AddChild(Text(caption.ToUpper(), p.TextDim, 11));
        var rule = new ColorRect { Color = p.BorderSoft, CustomMinimumSize = new Vector2(0, 1) };
        rule.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rule.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        h.AddChild(rule);
        if (tag != null) h.AddChild(Text(tag, p.Faint, 8));
        return h;
    }

    /// <summary>A "chip" used in the top bar (WPN · ..., TIER · ...).</summary>
    public static Control Chip(Palette p, string text, bool highlight = false)
    {
        var box = Panel(highlight ? p.Bg : p.Panel, highlight ? p.AccentDim : p.Border, pad: 6, borderW: 1);
        box.AddChild(Text(text, highlight ? p.Accent : p.TextDim, 10));
        return box;
    }

    public static Button FlatButton(Palette p, string text, Color color, Color border, int size = 10)
    {
        var b = new Button { Text = text, Flat = false };
        var normal = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = border };
        normal.SetBorderWidthAll(1);
        normal.ContentMarginTop = 9; normal.ContentMarginBottom = 9;
        normal.ContentMarginLeft = 10; normal.ContentMarginRight = 10;
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(color.R, color.G, color.B, 0.08f);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", hover);
        b.AddThemeColorOverride("font_color", color);
        b.AddThemeColorOverride("font_hover_color", color);
        b.AddThemeFontSizeOverride("font_size", size);
        return b;
    }

    /// <summary>The big accent "Commit &amp; Fire" button.</summary>
    public static Button PrimaryButton(Palette p, string text)
    {
        var b = new Button { Text = text };
        var normal = new StyleBoxFlat { BgColor = p.Accent };
        normal.ContentMarginTop = 13; normal.ContentMarginBottom = 13;
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = p.Accent.Lightened(0.12f);
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BgColor = p.Accent.Darkened(0.55f);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", normal);
        b.AddThemeStyleboxOverride("disabled", disabled);
        b.AddThemeColorOverride("font_color", p.Bg);
        b.AddThemeColorOverride("font_hover_color", p.Bg);
        b.AddThemeColorOverride("font_disabled_color", new Color(p.Bg.R, p.Bg.G, p.Bg.B, 0.7f));
        b.AddThemeFontSizeOverride("font_size", 16);
        return b;
    }

    /// <summary>
    /// Make <paramref name="panel"/> draggable by grabbing <paramref name="handle"/>
    /// (its title bar). Used so pop-up windows can be moved out of the way.
    /// </summary>
    public static void MakeDraggable(Control handle, Control panel)
    {
        bool dragging = false;
        Vector2 grabOffset = Vector2.Zero;
        handle.MouseFilter = Control.MouseFilterEnum.Stop;
        handle.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
            {
                dragging = mb.Pressed;
                if (mb.Pressed) grabOffset = panel.GetGlobalMousePosition() - panel.GlobalPosition;
            }
            else if (e is InputEventMouseMotion && dragging)
            {
                Vector2 target = panel.GetGlobalMousePosition() - grabOffset;
                Vector2 max = panel.GetViewportRect().Size - panel.Size;
                panel.GlobalPosition = new Vector2(
                    Mathf.Clamp(target.X, 0, Mathf.Max(0, max.X)),
                    Mathf.Clamp(target.Y, 0, Mathf.Max(0, max.Y)));
            }
        };
    }

    /// <summary>A numeric input field with up/down steppers.</summary>
    public static (Control row, LineEdit field, Button up, Button down) Stepper(Palette p, string value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);

        var le = new LineEdit { Text = value };
        le.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        le.AddThemeColorOverride("font_color", p.Accent);
        le.AddThemeFontSizeOverride("font_size", 20);
        var nb = new StyleBoxFlat { BgColor = p.PanelDeep, BorderColor = p.Border };
        nb.SetBorderWidthAll(1);
        nb.BorderWidthBottom = 2;
        nb.ContentMarginLeft = 9; nb.ContentMarginRight = 9;
        nb.ContentMarginTop = 7; nb.ContentMarginBottom = 7;
        var fb = (StyleBoxFlat)nb.Duplicate();
        fb.BorderColor = p.Accent;
        le.AddThemeStyleboxOverride("normal", nb);
        le.AddThemeStyleboxOverride("focus", fb);

        var steppers = new VBoxContainer();
        steppers.AddThemeConstantOverride("separation", 2);
        var up = FlatButton(p, "▲", p.AccentDim, p.Border, 9);
        var dn = FlatButton(p, "▼", p.AccentDim, p.Border, 9);
        up.CustomMinimumSize = new Vector2(26, 20);
        dn.CustomMinimumSize = new Vector2(26, 20);
        steppers.AddChild(up);
        steppers.AddChild(dn);

        row.AddChild(le);
        row.AddChild(steppers);
        return (row, le, up, dn);
    }

    /// <summary>A field label (small, dim, tracked).</summary>
    public static Label FieldLabel(Palette p, string text) => Text(text, p.Faint, 9);
}
