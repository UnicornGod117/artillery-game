using Godot;
using System.Collections.Generic;

namespace FiringSolution.Shell;

/// <summary>
/// Shared chrome for a fire-control station: top bar, the three-column layout
/// (left instruments · centre board + vertical plane · right firing-solution
/// input), built programmatically. Concrete stations fill the panels and wire
/// the Core. Mirrors the layout of both directions in the visual study.
/// </summary>
public abstract partial class StationView : Control
{
    protected Palette P = null!;
    protected PlottingBoard Board = null!;
    protected VerticalPlane VPlane = null!;
    protected VBoxContainer LastShot = null!;
    protected Label CareerLabel = null!;

    protected int ShotNo = 0;

    private Dictionary<PlottingBoard.Tool, Button> _toolButtons = new();

    /// <summary>Activate a board tool and highlight its toolbar button.</summary>
    private void SelectTool(PlottingBoard.Tool t)
    {
        Board.SetTool(t);
        foreach (var (kind, btn) in _toolButtons)
            btn.AddThemeColorOverride("font_color", kind == t ? P.Accent : P.AccentDim);
    }

    public override void _Ready()
    {
        P = BuildPalette();
        SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = P.Bg };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 1);
        AddChild(root);

        root.AddChild(BuildTopBar());

        var mid = new HBoxContainer();
        mid.SizeFlagsVertical = SizeFlags.ExpandFill;
        mid.AddThemeConstantOverride("separation", 1);
        root.AddChild(mid);

        var left = BuildLeftPanel();
        left.CustomMinimumSize = new Vector2(312, 0);
        mid.AddChild(left);

        var center = new VBoxContainer();
        center.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        center.SizeFlagsVertical = SizeFlags.ExpandFill;
        center.AddThemeConstantOverride("separation", 1);
        mid.AddChild(center);
        center.AddChild(BuildBoardSection());
        center.AddChild(BuildVerticalSection());

        var right = BuildRightPanel();
        right.CustomMinimumSize = new Vector2(372, 0);
        mid.AddChild(right);

        Configure();
        Refresh();
    }

    protected abstract Palette BuildPalette();
    protected abstract Control BuildTopBar();
    protected abstract Control BuildLeftPanel();
    protected abstract Control BuildRightPanel();
    protected abstract void Configure();
    protected abstract void Refresh();

    // ----- shared builders --------------------------------------------------

    protected Control MakeTopBar(string subtitle, (string text, bool hi)[] chips, string reloadLabel)
    {
        var bar = Ui.Panel(P.PanelDeep, P.Border, pad: 0, borderW: 0);
        bar.CustomMinimumSize = new Vector2(0, 46);
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 12);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddChild(h);
        bar.AddChild(margin);

        var diamond = new ColorRect { Color = P.Accent, CustomMinimumSize = new Vector2(9, 9) };
        diamond.RotationDegrees = 45;
        diamond.PivotOffset = new Vector2(4.5f, 4.5f);
        diamond.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(diamond);
        h.AddChild(Ui.Text("FIRING SOLUTION", P.Text, 16, true));
        h.AddChild(Ui.Text(subtitle, P.Faint, 10));

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        h.AddChild(spacer);

        foreach (var (text, hi) in chips)
            h.AddChild(Ui.Chip(P, text, hi));

        var reloadBox = new VBoxContainer();
        reloadBox.AddThemeConstantOverride("separation", 3);
        reloadBox.AddChild(Ui.Text(reloadLabel, P.Faint, 8));
        var pb = new ProgressBar { Value = 73, ShowPercentage = false, CustomMinimumSize = new Vector2(128, 5) };
        var pbg = new StyleBoxFlat { BgColor = P.PanelDeep, BorderColor = P.Border };
        pbg.SetBorderWidthAll(1);
        var pfill = new StyleBoxFlat { BgColor = P.Accent };
        pb.AddThemeStyleboxOverride("background", pbg);
        pb.AddThemeStyleboxOverride("fill", pfill);
        reloadBox.AddChild(pb);
        h.AddChild(reloadBox);

        var newMission = Ui.FlatButton(P, "↻ NEW MISSION", P.AccentDim, P.Border, 10);
        newMission.Pressed += () => (GetParent() as Main)?.ReloadStation();
        h.AddChild(newMission);

        CareerLabel = Ui.Text($"CAREER {Career.Points:N0} PTS", P.TextDim, 11);
        h.AddChild(CareerLabel);
        return bar;
    }

    /// <summary>Award career points, persist them, and refresh the top-bar readout.</summary>
    protected void AwardCareer(int points)
    {
        Career.Add(points);
        CareerLabel.Text = $"CAREER {Career.Points:N0} PTS";
    }

    private Control BuildBoardSection()
    {
        var v = new VBoxContainer();
        v.SizeFlagsVertical = SizeFlags.ExpandFill;
        v.AddThemeConstantOverride("separation", 0);

        var header = Ui.Panel(P.PanelDeep, P.BorderSoft, pad: 8, borderW: 0);
        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        hb.AddChild(new ColorRect { Color = P.Accent, CustomMinimumSize = new Vector2(5, 5), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        hb.AddChild(Ui.Text("PLOTTING BOARD", P.Text, 12));
        hb.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // Measurement toolbar (design §4): pan · ruler · protractor · pen · grid · clear.
        Board = new PlottingBoard { P = P };
        var tools = new List<Button>();
        Button Tool(string label, System.Action onPress, bool dim = false)
        {
            var b = Ui.FlatButton(P, label, dim ? P.Faint : P.AccentDim, P.Border, 10);
            b.Pressed += () => onPress();
            tools.Add(b);
            hb.AddChild(b);
            return b;
        }
        var bPan = Tool("PAN", () => SelectTool(PlottingBoard.Tool.Pan));
        var bRuler = Tool("RULER", () => SelectTool(PlottingBoard.Tool.Ruler));
        var bAngle = Tool("ANGLE", () => SelectTool(PlottingBoard.Tool.Protractor));
        var bPen = Tool("PEN", () => SelectTool(PlottingBoard.Tool.Pen));
        _toolButtons = new() {
            [PlottingBoard.Tool.Pan] = bPan, [PlottingBoard.Tool.Ruler] = bRuler,
            [PlottingBoard.Tool.Protractor] = bAngle, [PlottingBoard.Tool.Pen] = bPen,
        };
        Tool("GRID", () => { Board.ToggleGrid(); }, dim: true);
        Tool("CLR", () => Board.ClearMeasurements(), dim: true);
        hb.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) });

        var zin = Ui.FlatButton(P, "+", P.Text, P.Border, 13);
        var zout = Ui.FlatButton(P, "−", P.Text, P.Border, 13);
        var zr = Ui.FlatButton(P, "⌖", P.TextDim, P.Border, 11);
        hb.AddChild(zin); hb.AddChild(zout); hb.AddChild(zr);
        header.AddChild(hb);
        v.AddChild(header);

        Board.SizeFlagsVertical = SizeFlags.ExpandFill;
        Board.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.AddChild(Board);
        SelectTool(PlottingBoard.Tool.Pan);

        zin.Pressed += () => Board.ZoomBy(1.25f);
        zout.Pressed += () => Board.ZoomBy(0.8f);
        zr.Pressed += () => Board.ResetView();
        return v;
    }

    private Control BuildVerticalSection()
    {
        var wrap = new VBoxContainer();
        wrap.CustomMinimumSize = new Vector2(0, 214);
        wrap.AddThemeConstantOverride("separation", 0);

        var header = Ui.Panel(P.PanelDeep, P.BorderSoft, pad: 8, borderW: 0);
        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        hb.AddChild(new ColorRect { Color = P.Accent, CustomMinimumSize = new Vector2(5, 5), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        hb.AddChild(Ui.Text("VERTICAL PLANE — RANGE / ALTITUDE", P.Text, 11));
        header.AddChild(hb);
        wrap.AddChild(header);

        var row = new HBoxContainer();
        row.SizeFlagsVertical = SizeFlags.ExpandFill;
        row.AddThemeConstantOverride("separation", 1);

        VPlane = new VerticalPlane { P = P };
        VPlane.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        VPlane.SizeFlagsVertical = SizeFlags.ExpandFill;
        row.AddChild(VPlane);

        var sidePanel = Ui.Panel(P.PanelDeep, P.BorderSoft, pad: 12, borderW: 0);
        sidePanel.CustomMinimumSize = new Vector2(184, 0);
        LastShot = new VBoxContainer();
        LastShot.AddThemeConstantOverride("separation", 8);
        LastShot.AddChild(Ui.Text("LAST SHOT — OBSERVED", P.Faint, 8));
        LastShot.AddChild(Ui.Text("— NO SHOT FIRED —", P.Faint, 10));
        sidePanel.AddChild(LastShot);
        row.AddChild(sidePanel);

        wrap.AddChild(row);
        return wrap;
    }

    // ----- shared field / grid helpers --------------------------------------

    protected Control MetricGrid((string cap, string val)[] cells, Color valueColor, int valueSize = 15)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 1);
        grid.AddThemeConstantOverride("v_separation", 1);
        foreach (var (cap, val) in cells)
        {
            var cell = Ui.MetricCell(P, cap, val, valueColor, valueSize);
            cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            grid.AddChild(cell);
        }
        return grid;
    }

    protected static double Parse(LineEdit f) => double.TryParse(f.Text, out var n) ? n : 0;

    protected LineEdit AddNumberField(GridContainer grid, string label, string val, double step,
        System.Action<double> apply, bool isInt = false, System.Func<double, double>? clamp = null)
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 5);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.AddChild(Ui.FieldLabel(P, label));
        var (row, field, up, dn) = Ui.Stepper(P, val);
        col.AddChild(row);
        grid.AddChild(col);

        void Commit(double next)
        {
            if (clamp != null) next = clamp(next);
            field.Text = isInt ? ((int)System.Math.Round(next)).ToString() : next.ToString("0.0");
            apply(next);
        }
        up.Pressed += () => Commit(Parse(field) + step);
        dn.Pressed += () => Commit(Parse(field) - step);
        field.TextSubmitted += _ => apply(clamp != null ? clamp(Parse(field)) : Parse(field));
        field.FocusExited += () => apply(clamp != null ? clamp(Parse(field)) : Parse(field));
        return field;
    }

    /// <summary>Rebuild the last-shot readout column.</summary>
    protected void SetLastShot(string heading, Color headingColor, (string l, string v, Color c)[] rows, string footnote)
    {
        foreach (var child in LastShot.GetChildren()) child.QueueFree();
        LastShot.AddChild(Ui.Text("LAST SHOT — OBSERVED", P.Faint, 8));
        LastShot.AddChild(Ui.Text(heading, headingColor, 12, true));
        foreach (var (l, vv, c) in rows)
        {
            var hb = new HBoxContainer();
            hb.AddChild(Ui.Text(l, P.Faint, 10));
            hb.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            hb.AddChild(Ui.Text(vv, c, 10));
            LastShot.AddChild(hb);
        }
        LastShot.AddChild(Ui.Text(footnote, P.Faint, 8));
    }
}
