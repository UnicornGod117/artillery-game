using Godot;

namespace FiringSolution.Shell;

/// <summary>
/// The tactical plotting board (design §11). Shows the SITUATION — gun, range
/// rings, observed target, your aim line, and (after firing) the real impact
/// the oracle simulated. Drag to pan, scroll to zoom. It never draws a solution.
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

    // Pan / zoom state.
    private Vector2 _pan = Vector2.Zero;
    private float _zoom = 1.0f;
    private bool _dragging = false;
    private Vector2 _dragStart, _panStart;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    private Vector2 Gun => new(Size.X * GunFracX, Size.Y * GunFracY);

    private Vector2 World(double rangeM, double bearingDeg)
    {
        double br = Mathf.DegToRad((float)bearingDeg);
        double e = rangeM * Mathf.Sin((float)br);
        double n = rangeM * Mathf.Cos((float)br);
        return Gun + _pan + new Vector2((float)e, -(float)n) * PxPerMeter * _zoom;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
                _zoom = Mathf.Clamp(_zoom * 1.12f, 0.6f, 3f);
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
                _zoom = Mathf.Clamp(_zoom * 0.89f, 0.6f, 3f);
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                _dragging = mb.Pressed;
                _dragStart = mb.Position;
                _panStart = _pan;
            }
            QueueRedraw();
        }
        else if (@event is InputEventMouseMotion mm && _dragging)
        {
            _pan = _panStart + (mm.Position - _dragStart);
            QueueRedraw();
        }
    }

    public void ResetView() { _pan = Vector2.Zero; _zoom = 1f; QueueRedraw(); }
    public void ZoomBy(float m) { _zoom = Mathf.Clamp(_zoom * m, 0.6f, 3f); QueueRedraw(); }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, Size), P.PanelDeep);

        Vector2 gunC = Gun + _pan;

        // Range rings centred on the gun.
        for (int i = 1; i <= RingCount; i++)
        {
            float r = (float)(i * RingStepM) * PxPerMeter * _zoom;
            DrawArc(gunC, r, 0, Mathf.Tau, 96, P.Border, 1.0f, true);
            string km = (i * RingStepM / 1000).ToString("0") + "km";
            DrawString(font, gunC + new Vector2(r + 3, -2), km,
                HorizontalAlignment.Left, -1, 8, P.Faint);
        }

        // Gun emplacement.
        DrawLine(gunC + new Vector2(-8, 0), gunC + new Vector2(8, 0), P.AccentDim, 1);
        DrawLine(gunC + new Vector2(0, -8), gunC + new Vector2(0, 8), P.AccentDim, 1);
        DrawCircle(gunC, 3, P.PanelDeep);
        DrawArc(gunC, 3, 0, Mathf.Tau, 16, P.Accent, 1.5f, true);
        DrawString(font, gunC + new Vector2(-18, 22), IsBeam ? "EMITTER" : "GUN FCS-01",
            HorizontalAlignment.Left, -1, 8, P.AccentDim);

        // Aim line (dashed) along current azimuth input.
        Vector2 aimEnd = World(TargetRange * 1.15, AimAzimuth);
        DrawDashed(gunC, aimEnd, P.Accent, 1.4f, 7, 5);
        DrawString(font, aimEnd + new Vector2(4, 0), $"AIM {AimAzimuth:0.0}°",
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

        // Fixed chrome: north arrow + scale bar.
        Vector2 n = new(Size.X - 40, 56);
        DrawLine(n + new Vector2(0, 22), n + new Vector2(0, -8), P.TextDim, 1);
        DrawString(font, n + new Vector2(-4, 36), "N", HorizontalAlignment.Left, -1, 9, P.TextDim);

        float barPx = (float)RingStepM * PxPerMeter * _zoom;
        Vector2 sb = new(28, Size.Y - 22);
        DrawLine(sb, sb + new Vector2(barPx, 0), P.Faint, 1.5f);
        DrawString(font, sb + new Vector2(0, -6), $"0", HorizontalAlignment.Left, -1, 8, P.Faint);
        DrawString(font, sb + new Vector2(barPx - 14, -6), $"{RingStepM / 1000:0}km",
            HorizontalAlignment.Left, -1, 8, P.Faint);

        // Zoom readout.
        DrawString(font, new Vector2(Size.X - 96, Size.Y - 10), $"ZOOM {_zoom:0.0}×",
            HorizontalAlignment.Left, -1, 8, P.Faint);
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
