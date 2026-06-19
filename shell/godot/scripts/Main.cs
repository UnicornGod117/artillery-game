using FiringSolution.Core;
using Godot;

namespace FiringSolution.Shell;

/// <summary>
/// Entry point. Hosts one fire-control station at a time and lets you switch
/// between the two MVP weapons / visual directions (kinetic ↔ beam) — Tab, or
/// the corner button. Each station is self-contained and consumes the Core.
/// </summary>
public partial class Main : Control
{
    private Control? _station;
    private bool _beam = false;
    private Button _switch = null!;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        _switch = new Button { Text = "⟳ SWITCH STATION  (Tab)" };
        var sb = new StyleBoxFlat { BgColor = new Color("000000", 0.55f), BorderColor = new Color("ffffff", 0.18f) };
        sb.SetBorderWidthAll(1);
        sb.ContentMarginLeft = 12; sb.ContentMarginRight = 12;
        sb.ContentMarginTop = 7; sb.ContentMarginBottom = 7;
        _switch.AddThemeStyleboxOverride("normal", sb);
        _switch.AddThemeStyleboxOverride("hover", sb);
        _switch.AddThemeStyleboxOverride("pressed", sb);
        _switch.AddThemeColorOverride("font_color", new Color("e7e5df"));
        _switch.AddThemeFontSizeOverride("font_size", 11);
        _switch.Pressed += Toggle;

        var versionLabel = new Label { Text = Constants.Version };
        versionLabel.AddThemeColorOverride("font_color", new Color("ffffff", 0.35f));
        versionLabel.AddThemeFontSizeOverride("font_size", 10);
        AddChild(versionLabel);
        versionLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize, 8);

        Swap();
        AddChild(_switch);
        _switch.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 16);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Tab })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Toggle() { _beam = !_beam; Swap(); }

    /// <summary>Rebuild the current station in place — generates a fresh mission.</summary>
    public void ReloadStation() => Swap();

    private void Swap()
    {
        _station?.QueueFree();
        _station = _beam ? new BeamStation() : new KineticStation();
        _station.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_station);
        MoveChild(_station, 0); // keep the switch button on top
    }
}
