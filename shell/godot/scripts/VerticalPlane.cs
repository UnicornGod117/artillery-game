using Godot;
using System.Collections.Generic;

namespace FiringSolution.Shell;

/// <summary>
/// The vertical-plane view (range vs altitude). Shows your barrel angle and,
/// after firing, the real arc the oracle integrated (kinetic) or the straight
/// ray (beam). Situation, not prediction — the arc only appears once committed.
/// </summary>
public partial class VerticalPlane : Control
{
    public Palette P = Palette.Amber;
    public bool IsBeam = false;

    public double AimElevation = 38.0;
    public double TargetRange = 8600, TargetAltitude = 40;

    // Fired arc as (range, altitude) metre pairs from the Core trajectory.
    public List<Vector2>? Arc = null; // x = range m, y = altitude m
    public bool FiredHit = false;

    private float _maxRange = 12000, _maxAlt = 4000;

    public void SetScale(double maxRange, double maxAlt)
    {
        _maxRange = (float)System.Math.Max(maxRange, 1000);
        _maxAlt = (float)System.Math.Max(maxAlt, 500);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, Size), P.PanelDeep);

        float left = 40, bottom = Size.Y - 22, top = 14;
        float plotW = Size.X - left - 16;
        float plotH = bottom - top;

        // Axes.
        DrawLine(new Vector2(left, bottom), new Vector2(left + plotW, bottom), P.Border, 1);
        DrawLine(new Vector2(left, bottom), new Vector2(left, top), P.Border, 1);
        DrawString(font, new Vector2(14, top + 26), "ALT", HorizontalAlignment.Left, -1, 8, P.Faint);

        // Range gridlines.
        for (int i = 1; i <= 4; i++)
        {
            float fr = i / 4f;
            float x = left + plotW * fr;
            DrawLine(new Vector2(x, top + 6), new Vector2(x, bottom), P.BorderSoft, 1);
            DrawString(font, new Vector2(x - 8, bottom + 12),
                $"{_maxRange * fr / 1000:0}", HorizontalAlignment.Left, -1, 8, P.Faint);
        }

        Vector2 ToScreen(double rangeM, double altM) =>
            new(left + (float)(rangeM / _maxRange) * plotW,
                bottom - (float)(altM / _maxAlt) * plotH);

        // Barrel lay angle — a FIXED-LENGTH stub showing only the elevation you dialled
        // in. This is your input echoed back, NOT a predicted arc: the program never
        // forecasts where a round will go (design pillar 2). A trajectory is drawn only
        // AFTER you commit, and it is the real one the oracle simulated (see below).
        float el = Mathf.DegToRad((float)AimElevation);
        Vector2 gun = new(left, bottom);
        Vector2 aimEnd = gun + new Vector2(Mathf.Cos(el), -Mathf.Sin(el)) * 120;
        DrawLine(gun, aimEnd, P.Accent, 2);
        DrawString(font, gun + new Vector2(44, -12), $"LAY {AimElevation:0.0}°",
            HorizontalAlignment.Left, -1, 9, P.Accent);

        // Target marker.
        Vector2 tgt = ToScreen(TargetRange, System.Math.Max(0, TargetAltitude));
        DrawRect(new Rect2(tgt - new Vector2(5, 5), new Vector2(10, 10)), P.Red, false, 1.5f);
        DrawString(font, tgt + new Vector2(-30, -9), $"TGT {TargetRange / 1000:0.0}km",
            HorizontalAlignment.Left, -1, 8, P.Red);

        // Fired arc / ray — the ONLY trajectory ever drawn, and only after firing.
        // `Arc` is populated solely from the committed shot's real simulated points
        // (KineticStation/BeamStation.Fire); there is no pre-fire prediction path.
        if (Arc is { Count: > 1 })
        {
            var pts = new Vector2[Arc.Count];
            for (int i = 0; i < Arc.Count; i++)
                pts[i] = ToScreen(Arc[i].X, Mathf.Max(0, Arc[i].Y));
            Color c = FiredHit ? P.Accent : P.TextDim;
            DrawPolyline(pts, c, 1.8f, true);
            Vector2 last = pts[^1];
            Color ic = FiredHit ? P.Accent : P.Red;
            DrawLine(last + new Vector2(-5, -5), last + new Vector2(5, 5), ic, 1.6f);
            DrawLine(last + new Vector2(-5, 5), last + new Vector2(5, -5), ic, 1.6f);
        }
    }
}
