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

    // Absolute position of the gun on the battlespace grid (ENU east/north, metres). The
    // grid, the cursor readout and the markers all read in these absolute coordinates, so
    // the board never hands the player a bare "range & bearing to target" — that geometry
    // is theirs to measure or compute from the coordinates.
    public Vector2 GunOriginM = Vector2.Zero;

    // Display units for the grid/coordinates/scale: metres-per-unit and a label. Kinetic
    // works in km (1000 m); the long-range beam battlespace works in light-seconds.
    public double UnitMeters = 1000.0;
    public string UnitLabel = "km";

    // Zoom envelope — generous so the player can pull right in on a marker to plot by hand.
    private const float ZoomMin = 0.35f, ZoomMax = 24f;

    // Spotter / observation post — a known friendly position the player triangulates
    // a correction from. Set by the station; drawn so the board geometry is legible.
    public bool HasSpotter = false;
    public double SpotterRange, SpotterBearing;
    public string SpotterLabel = "OP-1";

    // Player aim (live input).
    public double AimAzimuth = 42.0;

    // Fired result (from the Core).
    public bool HasFired = false;
    public double FiredRange, FiredBearing;
    public bool FiredHit = false;
    public bool IsBeam = false;

    // Measurement tools.
    public enum Tool { Pan, Ruler, Protractor, Pen, Erase }
    public Tool ActiveTool = Tool.Pan;
    public bool ShowGrid = true;

    // Impact fly-out animation (the dashed line grows from the gun to the impact).
    private float _impactAnim = 1f;
    private bool _animating = false;

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
    // Insertion order across all three lists, so UNDO can drop the most recent thing
    // drawn (0 = ruler, 1 = angle, 2 = pen).
    private readonly List<int> _order = new();
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

    /// <summary>Gun-relative metres (E,N) → absolute battlespace coordinate (metres).</summary>
    private Vector2 AbsMetres(Vector2 relM) => GunOriginM + relM;

    /// <summary>Gun-relative polar (range m, bearing deg) → absolute coordinate (metres).</summary>
    private Vector2 AbsMetres(double rangeM, double bearingDeg)
    {
        float br = Mathf.DegToRad((float)bearingDeg);
        return AbsMetres(new Vector2((float)rangeM * Mathf.Sin(br), (float)rangeM * Mathf.Cos(br)));
    }

    private string Coord(Vector2 absM)
        => $"E {absM.X / UnitMeters:0.000} · N {absM.Y / UnitMeters:0.000} {UnitLabel}";

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
        _rulers.Clear(); _angles.Clear(); _pens.Clear(); _order.Clear();
        _pendingRuler = null; _pendingAngle.Clear(); _penCurrent = null;
        QueueRedraw();
    }

    /// <summary>Remove the most recently drawn measurement (ruler / angle / pen).</summary>
    public void Undo()
    {
        // Drop anything mid-construction first, so UNDO always has an obvious effect.
        if (_pendingRuler != null) { _pendingRuler = null; QueueRedraw(); return; }
        if (_pendingAngle.Count > 0) { _pendingAngle.Clear(); QueueRedraw(); return; }
        if (_penCurrent != null) { _penCurrent = null; QueueRedraw(); return; }

        if (_order.Count == 0) return;
        int kind = _order[^1];
        _order.RemoveAt(_order.Count - 1);
        switch (kind)
        {
            case 0 when _rulers.Count > 0: _rulers.RemoveAt(_rulers.Count - 1); break;
            case 1 when _angles.Count > 0: _angles.RemoveAt(_angles.Count - 1); break;
            case 2 when _pens.Count > 0: _pens.RemoveAt(_pens.Count - 1); break;
        }
        QueueRedraw();
    }

    private void FinishPen()
    {
        if (_penCurrent is { Count: >= 2 }) { _pens.Add(_penCurrent); _order.Add(2); }
        _penCurrent = null;
    }

    // ----- input ------------------------------------------------------------

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
                ZoomAt(mb.Position, 1.12f);
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
                ZoomAt(mb.Position, 0.89f);
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
                else { _rulers.Add((_pendingRuler.Value, m)); _order.Add(0); _pendingRuler = null; }
                break;
            case Tool.Protractor:
                _pendingAngle.Add(m);
                if (_pendingAngle.Count == 3)
                { _angles.Add((_pendingAngle[0], _pendingAngle[1], _pendingAngle[2])); _order.Add(1); _pendingAngle.Clear(); }
                break;
            case Tool.Pen:
                (_penCurrent ??= new List<Vector2>()).Add(m);
                break;
            case Tool.Erase:
                EraseNear(m);
                break;
        }
    }

    /// <summary>Delete the measurement nearest the click, within a small pick radius.</summary>
    private void EraseNear(Vector2 m)
    {
        float pick = 14f / K;             // ~14 px, expressed in metres at the current zoom
        float best = pick;
        int bestKind = -1, bestIdx = -1;

        for (int i = 0; i < _rulers.Count; i++)
        {
            float d = DistToSegment(m, _rulers[i].a, _rulers[i].b);
            if (d < best) { best = d; bestKind = 0; bestIdx = i; }
        }
        for (int i = 0; i < _angles.Count; i++)
        {
            var (v, a, b) = _angles[i];
            float d = Mathf.Min(DistToSegment(m, v, a), DistToSegment(m, v, b));
            if (d < best) { best = d; bestKind = 1; bestIdx = i; }
        }
        for (int i = 0; i < _pens.Count; i++)
        {
            var poly = _pens[i];
            for (int j = 1; j < poly.Count; j++)
            {
                float d = DistToSegment(m, poly[j - 1], poly[j]);
                if (d < best) { best = d; bestKind = 2; bestIdx = i; }
            }
        }

        if (bestKind < 0) return;
        switch (bestKind)
        {
            case 0: _rulers.RemoveAt(bestIdx); break;
            case 1: _angles.RemoveAt(bestIdx); break;
            case 2: _pens.RemoveAt(bestIdx); break;
        }
        // Forget the matching entry in the undo order (latest of that kind is closest).
        for (int i = _order.Count - 1; i >= 0; i--)
            if (_order[i] == bestKind) { _order.RemoveAt(i); break; }
        QueueRedraw();
    }

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        if (len2 < 1e-6f) return (p - a).Length();
        float t = Mathf.Clamp((p - a).Dot(ab) / len2, 0f, 1f);
        return (p - (a + ab * t)).Length();
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

    /// <summary>Zoom by a factor about the board centre (the +/− buttons).</summary>
    public void ZoomBy(float m) => ZoomAt(Size / 2f, m);

    /// <summary>Zoom by a factor while keeping the world point under <paramref name="at"/> fixed.</summary>
    private void ZoomAt(Vector2 at, float m)
    {
        float newZoom = Mathf.Clamp(_zoom * m, ZoomMin, ZoomMax);
        if (Mathf.IsEqualApprox(newZoom, _zoom)) return;
        Vector2 pm = ToMetres(at);          // world point under the cursor, before zoom
        _zoom = newZoom;
        float k = PxPerMeter * _zoom;
        _pan = at - new Vector2(pm.X, -pm.Y) * k - Gun;
        QueueRedraw();
    }

    // ----- impact animation -------------------------------------------------

    /// <summary>Arm the post-fire fly-out; the station's single sim clock drives the progress.</summary>
    public void BeginImpactAnim() { _impactAnim = 0f; _animating = true; QueueRedraw(); }

    /// <summary>Set fly-out progress (0..1). Called each frame by the station's one clock, so
    /// the on-screen travel takes the round's real (time-scaled) flight time.</summary>
    public void SetAnimProgress(float p)
    {
        _impactAnim = Mathf.Clamp(p, 0f, 1f);
        _animating = _impactAnim < 1f;
        QueueRedraw();
    }

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
            DrawString(font, gunC + new Vector2(r + 3, -2), (i * RingStepM / UnitMeters).ToString("0.###") + UnitLabel,
                HorizontalAlignment.Left, -1, 8, P.Faint);
        }

        // Bearing ring: cardinal + 30° ticks around the outermost ring, so the board
        // reads as a real azimuth dial rather than bare circles.
        DrawBearingTicks(gunC, (float)(RingCount * RingStepM) * K, font);

        // Gun emplacement.
        DrawLine(gunC + new Vector2(-8, 0), gunC + new Vector2(8, 0), P.AccentDim, 1);
        DrawLine(gunC + new Vector2(0, -8), gunC + new Vector2(0, 8), P.AccentDim, 1);
        DrawCircle(gunC, 3, P.PanelDeep);
        DrawArc(gunC, 3, 0, Mathf.Tau, 16, P.Accent, 1.5f, true);
        DrawString(font, gunC + new Vector2(-18, 22), IsBeam ? "EMITTER" : "GUN FCS-01",
            HorizontalAlignment.Left, -1, 8, P.AccentDim);
        DrawString(font, gunC + new Vector2(-18, 33), Coord(GunOriginM),
            HorizontalAlignment.Left, -1, 8, P.Faint);

        // Heading tick — a SHORT fixed-length stub showing only the bearing you dialled
        // in. Too short to reach the target, so it can never reveal alignment or whether
        // a shot would land — it echoes your input, never a prediction (design pillar 2).
        const float headingPx = 58f;
        float abr = Mathf.DegToRad((float)AimAzimuth);
        Vector2 aimDir = new(Mathf.Sin(abr), -Mathf.Cos(abr));
        Vector2 aimEnd = gunC + aimDir * headingPx;
        DrawDashed(gunC, aimEnd, P.Accent, 1.4f, 6, 4);
        DrawString(font, aimEnd + aimDir * 6 + new Vector2(-12, -6), $"BRG {AimAzimuth:0.00}°",
            HorizontalAlignment.Left, -1, 8, P.Accent);

        // Spotter / observation post.
        if (HasSpotter)
        {
            Vector2 sp = World(SpotterRange, SpotterBearing);
            DrawArc(sp, 6, 0, Mathf.Tau, 18, P.TextDim, 1.2f, true);
            DrawLine(sp + new Vector2(-3, 0), sp + new Vector2(3, 0), P.TextDim, 1);
            DrawLine(sp + new Vector2(0, -3), sp + new Vector2(0, 3), P.TextDim, 1);
            DrawString(font, sp + new Vector2(9, -2), SpotterLabel, HorizontalAlignment.Left, -1, 8, P.TextDim);
        }

        // Observed target marker — labelled with its battlespace COORDINATE, not a range
        // and bearing. Converting the coordinate into a firing solution is the player's job.
        Vector2 tgt = World(TargetRange, TargetBearing);
        DrawTargetMark(tgt, P.Red);
        DrawString(font, tgt + new Vector2(10, -2), TargetLabel,
            HorizontalAlignment.Left, -1, 8, P.Red);
        DrawString(font, tgt + new Vector2(10, 9), Coord(AbsMetres(TargetRange, TargetBearing)),
            HorizontalAlignment.Left, -1, 8, P.Red);

        // Fired result — animated fly-out from the gun, then the impact mark.
        if (HasFired)
        {
            Vector2 impact = World(FiredRange, FiredBearing);
            Color c = FiredHit ? P.Accent : P.Red;
            Vector2 cur = gunC + (impact - gunC) * _impactAnim;
            DrawDashed(gunC, cur, c, 1.6f, 9, 5);
            if (_animating)
            {
                // The round in flight.
                DrawCircle(cur, 3.5f, c);
                DrawArc(cur, 6, 0, Mathf.Tau, 16, c, 1f, true);
            }
            else
            {
                float rad = FiredHit ? 9 : 14;
                DrawArc(impact, rad, 0, Mathf.Tau, 32, c, 1.2f, true);
                DrawLine(impact + new Vector2(-6, -6), impact + new Vector2(6, 6), c, 1.8f);
                DrawLine(impact + new Vector2(-6, 6), impact + new Vector2(6, -6), c, 1.8f);
                if (!FiredHit)
                    DrawDashed(impact, tgt, P.Red, 1f, 2, 3);
            }
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
        DrawString(font, sb + new Vector2(barPx - 18, -6), $"{RingStepM / UnitMeters:0.###}{UnitLabel}",
            HorizontalAlignment.Left, -1, 8, P.Faint);

        // Active-tool hint + live cursor readout.
        string hint = ActiveTool switch
        {
            Tool.Ruler => "RULER · click 2 points · right-click cancels",
            Tool.Protractor => "PROTRACTOR · click vertex + 2 arms · right-click cancels",
            Tool.Pen => "PEN · click to add points · right-click finishes",
            Tool.Erase => "ERASE · click a measurement to delete it",
            _ => "PAN · drag to move · scroll to zoom",
        };
        DrawString(font, new Vector2(10, 16), hint, HorizontalAlignment.Left, -1, 8, P.TextDim);
        // Live cursor readout — battlespace COORDINATE only (no range/bearing-to-gun). To
        // turn coordinates into a firing solution, measure with the RULER or do the trig.
        if (_hasHover)
        {
            Vector2 abs = AbsMetres(_hoverM);
            DrawString(font, new Vector2(10, Size.Y - 8),
                $"CURSOR  E {abs.X / 1000:0.00}  ·  N {abs.Y / 1000:0.00} km",
                HorizontalAlignment.Left, -1, 8, P.Faint);
        }

        DrawString(font, new Vector2(Size.X - 96, Size.Y - 10), $"ZOOM {_zoom:0.0}×",
            HorizontalAlignment.Left, -1, 8, P.Faint);
    }

    private void DrawGrid(Vector2 gunC)
    {
        float step = (float)RingStepM * K;
        if (step < 6) return;
        var font = ThemeDB.FallbackFont;
        // Clamp the gun axis on-screen so the km labels stay visible while panning.
        float axisY = Mathf.Clamp(gunC.Y, 12, Size.Y - 4);
        float axisX = Mathf.Clamp(gunC.X, 16, Size.X - 28);
        for (int i = -24; i <= 24; i++)
        {
            float x = gunC.X + i * step;
            if (x >= 0 && x <= Size.X)
            {
                DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), P.BorderSoft, 1);
                // Absolute battlespace easting at this line (display units).
                DrawString(font, new Vector2(x + 2, axisY - 3), $"{(GunOriginM.X + i * RingStepM) / UnitMeters:0.###}",
                    HorizontalAlignment.Left, -1, 7, P.Faint);
            }
            float y = gunC.Y + i * step;
            if (y >= 0 && y <= Size.Y)
            {
                DrawLine(new Vector2(0, y), new Vector2(Size.X, y), P.BorderSoft, 1);
                // Absolute battlespace northing (screen-down is south, so subtract).
                DrawString(font, new Vector2(axisX, y - 3), $"{(GunOriginM.Y - i * RingStepM) / UnitMeters:0.###}",
                    HorizontalAlignment.Left, -1, 7, P.Faint);
            }
        }
    }

    /// <summary>Cardinal letters + 30° azimuth ticks around the outermost ring.</summary>
    private void DrawBearingTicks(Vector2 gunC, float radius, Font font)
    {
        if (radius < 24) return;
        for (int b = 0; b < 360; b += 30)
        {
            float br = Mathf.DegToRad(b);
            Vector2 dir = new(Mathf.Sin(br), -Mathf.Cos(br));
            DrawLine(gunC + dir * radius, gunC + dir * (radius + 6), P.Border, 1);
            string lbl = b switch { 0 => "N", 90 => "E", 180 => "S", 270 => "W", _ => b.ToString("000") };
            bool card = b % 90 == 0;
            DrawString(font, gunC + dir * (radius + 16) + new Vector2(-7, 4), lbl,
                HorizontalAlignment.Left, -1, card ? 9 : 7, card ? P.TextDim : P.Faint);
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
