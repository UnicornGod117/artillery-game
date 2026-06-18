using Godot;
using System.Collections.Generic;

namespace FiringSolution.Shell;

/// <summary>
/// The tactical plotting board (design §11). Shows the SITUATION — gun, range
/// rings, observed target, your bearing tick, and (after firing) the real impact
/// the oracle simulated. Drag to pan, scroll to zoom. It never draws a solution
/// and never predicts a path.
///
/// Measurement instruments (design §4 — "ruler, protractor, compass, and pencil
/// for triangulation and geometry"): a cartesian km GRID, a RULER (distance +
/// bearing between two points), a PROTRACTOR (angle at a vertex), and a PEN for
/// freehand construction lines. These are measuring aids the player uses to work
/// distances out by hand — they yield measurements, never the firing solution.
/// </summary>
public partial class PlottingBoard : Control
{
    public Palette P = Palette.Amber;

    // Layout / scale.
    public float GunFracX = 0.20f, GunFracY = 0.82f;
    public float PxPerMeter = 0.038f / 1.0f; // base px per metre at zoom 1
    public double RingStepM = 2000;
    public int RingCount = 4;

    // Situation (observed; what the player is given).
    public double TargetRange = 8600, TargetBearing = 41.7;
    public string TargetLabel = "TGT";

    // Player aim (live input).
    public double AimAzimuth = 42.0;

    // Fired result (from the Core).
    public bool HasFired = false;
    public double FiredRange, FiredBearing;
    public bool FiredHit = false;
    public bool IsBeam = false;

    // Measurement tools.
    public enum Tool { Pan, Ruler, Protractor, Pen }
    public Tool ActiveTool = Tool.Pan;
    public bool ShowGrid = false;

    // Pan / zoom state.
    private Vector2 _pan = Vector2.Zero;
    private float _zoom = 1.0f;
    private bool _dragging = false;
    private Vector2 _dragStart, _panStart;

    // Measurements are stored in METRES (E, N) from the gun so they stay anchored
    // to the map as you pan and zoom.
    private readonly List<(Vector2 a, Vector2 b)> _rulers = new();
    private readonly List<(Vector2 v, Vector2 a, Vector2 b)> _angles = new();
    private readonly List<List<Vector2>> _pens = new();
    private Vector2? _pendingRuler;
    private readonly List<Vector2> _pendingAngle = new();
    private List<Vector2>? _penCurrent;
    private Vector2 _hoverM;
    private bool _hasHover;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    private Vector2 Gun => new(Size.X * GunFracX, Size.Y * GunFracY);
    private float K => PxPerMeter * _zoom;
    private Vector2 GunC => Gun + _pan;
    private Vector2 ToScreenM(Vector2 m) => GunC + new Vector2(m.X, -m.Y) * K;
    private Vector2 ToMetres(Vector2 s) { Vector2 d = (s - GunC) / K; return new Vector2(d.X, -d.Y); }

    private Vector2 World(double rangeM, double bearingDeg)
    {
        float br = Mathf.DegToRad((float)bearingDeg);
        return ToScreenM(new Vector2((float)rangeM * Mathf.Sin(br), (float)rangeM * Mathf.Cos(br)));
    }

    private static double Bearing(Vector2 m)
    {
        double b = Mathf.RadToDeg(Mathf.Atan2(m.X, m.Y));
        return b < 0 ? b + 360 : b;
    }

    // ----- tool control (called by the station's board toolbar) -------------

    public void SetTool(Tool t)
    {
        FinishPen();
        _pendingRuler = null;
        _pendingAngle.Clear();
        ActiveTool = t;
        QueueRedraw();
    }

    public void ToggleGrid() { ShowGrid = !ShowGrid; QueueRedraw(); }

    public void ClearMeasurements()
    {
        _rulers.Clear(); _angles.Clear(); _pens.Clear();
        _pendingRuler = null; _pendingAngle.Clear(); _penCurrent = null;
        QueueRedraw();
    }

    private void FinishPen()
    {
        if (_penCurrent is { Count: >= 2 }) _pens.Add(_penCurrent);
        _penCurrent = null;
    }

    // ----- input ------------------------------------------------------------

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
                _zoom = Mathf.Clamp(_zoom * 1.12f, 0.6f, 3f);
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
                _zoom = Mathf.Clamp(_zoom * 0.89f, 0.6f, 3f);
            else if (mb.ButtonIndex == MouseButton.Middle)
                StartStopPan(mb.Pressed, mb.Position);                // middle-drag pans in any tool
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                if (ActiveTool == Tool.Pan) StartStopPan(mb.Pressed, mb.Position);
                else if (mb.Pressed) ToolClick(ToMetres(mb.Position));
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                ToolCancel();
            QueueRedraw();
        }
        else if (@event is InputEventMouseMotion mm)
        {
            _hoverM = ToMetres(mm.Position);
            _hasHover = true;
            if (_dragging) _pan = _panStart + (mm.Position - _dragStart);
            QueueRedraw();
        }
    }

    private void StartStopPan(bool pressed, Vector2 at)
    {
        _dragging = pressed;
        _dragStart = at;
        _panStart = _pan;
    }

    private void ToolClick(Vector2 m)
    {
        switch (ActiveTool)
        {
            case Tool.Ruler:
                if (_pendingRuler is null) _pendingRuler = m;
                else { _rulers.Add((_pendingRuler.Value, m)); _pendingRuler = null; }
                break;
            case Tool.Protractor:
                _pendingAngle.Add(m);
                if (_pendingAngle.Count == 3)
                { _angles.Add((_pendingAngle[0], _pendingAngle[1], _pendingAngle[2])); _pendingAngle.Clear(); }
                break;
            case Tool.Pen:
                (_penCurrent ??= new List<Vector2>()).Add(m);
                break;
        }
    }

    private void ToolCancel()
    {
        switch (ActiveTool)
        {
            case Tool.Ruler: _pendingRuler = null; break;
            case Tool.Protractor: _pendingAngle.Clear(); break;
            case Tool.Pen: FinishPen(); break;
        }
    }

    public void ResetView() { _pan = Vector2.Zero; _zoom = 1f; QueueRedraw(); }
    public void ZoomBy(float m) { _zoom = Mathf.Clamp(_zoom * m, 0.6f, 3f); QueueRedraw(); }

    // ----- drawing ----------------------------------------------------------

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, Size), P.PanelDeep);

        Vector2 gunC = GunC;

        if (ShowGrid) DrawGrid(gunC);

        // Range rings centred on the gun.
        for (int i = 1; i <= RingCount; i++)
        {
            float r = (float)(i * RingStepM) * K;
            DrawArc(gunC, r, 0, Mathf.Tau, 96, P.Border, 1.0f, true);
            DrawString(font, gunC + new Vector2(r + 3, -2), (i * RingStepM / 1000).ToString("0") + "km",
                HorizontalAlignment.Left, -1, 8, P.Faint);
        }

        // Gun emplacement.
        DrawLine(gunC + new Vector2(-8, 0), gunC + new Vector2(8, 0), P.AccentDim, 1);
        DrawLine(gunC + new Vector2(0, -8), gunC + new Vector2(0, 8), P.AccentDim, 1);
        DrawCircle(gunC, 3, P.PanelDeep);
        DrawArc(gunC, 3, 0, Mathf.Tau, 16, P.Accent, 1.5f, true);
        DrawString(font, gunC + new Vector2(-18, 22), IsBeam ? "EMITTER" : "GUN FCS-01",
            HorizontalAlignment.Left, -1, 8, P.AccentDim);

        // Heading tick — a SHORT fixed-length stub showing only the bearing you dialled
        // in. Too short to reach the target, so it can never reveal alignment or whether
        // a shot would land — it echoes your input, never a prediction (design pillar 2).
        const float headingPx = 58f;
        float abr = Mathf.DegToRad((float)AimAzimuth);
        Vector2 aimDir = new(Mathf.Sin(abr), -Mathf.Cos(abr));
        Vector2 aimEnd = gunC + aimDir * headingPx;
        DrawDashed(gunC, aimEnd, P.Accent, 1.4f, 6, 4);
        DrawString(font, aimEnd + aimDir * 6 + new Vector2(-12, -6), $"BRG {AimAzimuth:0.0}°",
            HorizontalAlignment.Left, -1, 8, P.Accent);

        // Observed target marker.
        Vector2 tgt = World(TargetRange, TargetBearing);
        DrawTargetMark(tgt, P.Red);
        DrawString(font, tgt + new Vector2(10, -2), $"{TargetLabel} · {TargetRange / 1000:0.00}km",
            HorizontalAlignment.Left, -1, 8, P.Red);

        // Fired result.
        if (HasFired)
        {
            Vector2 impact = World(FiredRange, FiredBearing);
            Color c = FiredHit ? P.Accent : P.Red;
            DrawDashed(gunC, impact, c, 1.6f, 9, 5);
            float rad = FiredHit ? 9 : 14;
            DrawArc(impact, rad, 0, Mathf.Tau, 32, c, 1.2f, true);
            DrawLine(impact + new Vector2(-6, -6), impact + new Vector2(6, 6), c, 1.8f);
            DrawLine(impact + new Vector2(-6, 6), impact + new Vector2(6, -6), c, 1.8f);
            if (!FiredHit)
                DrawDashed(impact, tgt, P.Red, 1f, 2, 3);
        }

        DrawMeasurements(font);

        // Fixed chrome: north arrow + scale bar.
        Vector2 n = new(Size.X - 40, 56);
        DrawLine(n + new Vector2(0, 22), n + new Vector2(0, -8), P.TextDim, 1);
        DrawString(font, n + new Vector2(-4, 36), "N", HorizontalAlignment.Left, -1, 9, P.TextDim);

        float barPx = (float)RingStepM * K;
        Vector2 sb = new(28, Size.Y - 22);
        DrawLine(sb, sb + new Vector2(barPx, 0), P.Faint, 1.5f);
        DrawString(font, sb + new Vector2(0, -6), "0", HorizontalAlignment.Left, -1, 8, P.Faint);
        DrawString(font, sb + new Vector2(barPx - 14, -6), $"{RingStepM / 1000:0}km",
            HorizontalAlignment.Left, -1, 8, P.Faint);

        // Active-tool hint + live cursor readout.
        string hint = ActiveTool switch
        {
            Tool.Ruler => "RULER · click 2 points · right-click cancels",
            Tool.Protractor => "PROTRACTOR · click vertex + 2 arms · right-click cancels",
            Tool.Pen => "PEN · click to add points · right-click finishes",
            _ => "PAN · drag to move · scroll to zoom",
        };
        DrawString(font, new Vector2(10, 16), hint, HorizontalAlignment.Left, -1, 8, P.TextDim);
        if (_hasHover && ActiveTool != Tool.Pan)
        {
            double cr = _hoverM.Length();
            DrawString(font, new Vector2(10, Size.Y - 8),
                $"CURSOR  {cr / 1000:0.00} km  ·  {Bearing(_hoverM):0.0}° from gun",
                HorizontalAlignment.Left, -1, 8, P.Faint);
        }

        DrawString(font, new Vector2(Size.X - 96, Size.Y - 10), $"ZOOM {_zoom:0.0}×",
            HorizontalAlignment.Left, -1, 8, P.Faint);
    }

    private void DrawGrid(Vector2 gunC)
    {
        float step = (float)RingStepM * K;
        if (step < 6) return;
        for (int i = -24; i <= 24; i++)
        {
            float x = gunC.X + i * step;
            if (x >= 0 && x <= Size.X) DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), P.BorderSoft, 1);
            float y = gunC.Y + i * step;
            if (y >= 0 && y <= Size.Y) DrawLine(new Vector2(0, y), new Vector2(Size.X, y), P.BorderSoft, 1);
        }
    }

    private void DrawMeasurements(Font font)
    {
        // Pen construction lines.
        foreach (var poly in _pens) DrawPen(poly, P.TextDim);
        if (_penCurrent is { Count: >= 1 })
        {
            DrawPen(_penCurrent, P.AccentDim);
            if (_hasHover)
                DrawLine(ToScreenM(_penCurrent[^1]), ToScreenM(_hoverM), P.AccentDim, 1f);
        }

        // Rulers.
        foreach (var (a, b) in _rulers) DrawRuler(font, a, b, P.Accent);
        if (_pendingRuler is { } pr && _hasHover) DrawRuler(font, pr, _hoverM, P.AccentDim);

        // Protractors.
        foreach (var (v, a, b) in _angles) DrawAngle(font, v, a, b, P.Accent);
        if (_pendingAngle.Count > 0)
        {
            foreach (var pt in _pendingAngle) DrawCircle(ToScreenM(pt), 2.5f, P.AccentDim);
            if (_pendingAngle.Count == 1 && _hasHover)
                DrawLine(ToScreenM(_pendingAngle[0]), ToScreenM(_hoverM), P.AccentDim, 1f);
            else if (_pendingAngle.Count == 2)
            {
                DrawLine(ToScreenM(_pendingAngle[0]), ToScreenM(_pendingAngle[1]), P.AccentDim, 1f);
                if (_hasHover) DrawAngle(font, _pendingAngle[0], _pendingAngle[1], _hoverM, P.AccentDim);
            }
        }
    }

    private void DrawPen(List<Vector2> poly, Color c)
    {
        for (int i = 1; i < poly.Count; i++)
            DrawLine(ToScreenM(poly[i - 1]), ToScreenM(poly[i]), c, 1.2f);
        foreach (var p in poly) DrawCircle(ToScreenM(p), 2f, c);
    }

    private void DrawRuler(Font font, Vector2 am, Vector2 bm, Color c)
    {
        Vector2 a = ToScreenM(am), b = ToScreenM(bm);
        DrawLine(a, b, c, 1.3f);
        DrawCircle(a, 2.6f, c);
        DrawCircle(b, 2.6f, c);
        double dist = (bm - am).Length();
        string d = dist < 1000 ? $"{dist:0} m" : $"{dist / 1000:0.00} km";
        DrawString(font, (a + b) / 2 + new Vector2(6, -6), $"{d} · {Bearing(bm - am):0.0}°",
            HorizontalAlignment.Left, -1, 9, P.Text);
    }

    private void DrawAngle(Font font, Vector2 vm, Vector2 am, Vector2 bm, Color c)
    {
        Vector2 v = ToScreenM(vm), a = ToScreenM(am), b = ToScreenM(bm);
        DrawLine(v, a, c, 1.3f);
        DrawLine(v, b, c, 1.3f);
        DrawCircle(v, 2.8f, c);
        Vector2 u1 = am - vm, u2 = bm - vm;
        float l1 = u1.Length(), l2 = u2.Length();
        if (l1 < 1e-3f || l2 < 1e-3f) return;
        double cos = Mathf.Clamp(u1.Dot(u2) / (l1 * l2), -1f, 1f);
        double ang = Mathf.RadToDeg((float)System.Math.Acos(cos));
        DrawString(font, v + new Vector2(8, -8), $"{ang:0.0}°", HorizontalAlignment.Left, -1, 10, P.Text);
    }

    private void DrawTargetMark(Vector2 c, Color col)
    {
        DrawRect(new Rect2(c - new Vector2(6.5f, 6.5f), new Vector2(13, 13)), col, false, 1.5f);
        DrawLine(c + new Vector2(-11, 0), c + new Vector2(11, 0), col, 0.8f);
        DrawLine(c + new Vector2(0, -11), c + new Vector2(0, 11), col, 0.8f);
    }

    private void DrawDashed(Vector2 a, Vector2 b, Color col, float width, float dash, float gap)
    {
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 0.001f) return;
        Vector2 dir = d / len;
        float pos = 0;
        while (pos < len)
        {
            float end = Mathf.Min(pos + dash, len);
            DrawLine(a + dir * pos, a + dir * end, col, width);
            pos = end + gap;
        }
    }
}
