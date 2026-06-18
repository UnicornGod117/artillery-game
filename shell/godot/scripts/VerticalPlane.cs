using Godot;
using System.Collections.Generic;

namespace FiringSolution.Shell;

/// <summary>
/// The vertical-plane view (range vs altitude). Shows your barrel angle and,
/// after firing, the real arc the oracle integrated (kinetic) or the straight
/// ray (beam). Situation, not prediction — the arc only appears once committed.
///
/// Altitude is measured relative to the GUN (0 = gun level). The view spans
/// [_minAlt, _maxAlt], so a target above the gun sits above the gun-level line and
/// a target BELOW the gun (negative altitude) sits below it — no longer clamped.
/// </summary>
public partial class VerticalPlane : Control
{
    public Palette P = Palette.Amber;
    public bool IsBeam = false;

    public double AimElevation = 38.0;
    public double TargetRange = 8600, TargetAltitude = 40;

    // Fired arc as (range, altitude) metre pairs from the Core trajectory.
    public List<Vector2>? Arc = null; // x = range m, y = altitude m (relative to gun)
    public bool FiredHit = false;

    private float _maxRange = 12000, _maxAlt = 4000, _minAlt = 0;

    /// <summary>Set the plotted window. <paramref name="minAlt"/> &lt; 0 shows below-gun ground.</summary>
    public void SetScale(double maxRange, double maxAlt, double minAlt = 0)
    {
        _maxRange = (float)System.Math.Max(maxRange, 1000);
        _maxAlt = (float)System.Math.Max(maxAlt, 500);
        _minAlt = (float)System.Math.Min(minAlt, 0);
        // Guarantee a non-degenerate span.
        if (_maxAlt - _minAlt < 500) _maxAlt = _minAlt + 500;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, Size), P.PanelDeep);

        float left = 44, bottom = Size.Y - 22, top = 14;
        float plotW = Size.X - left - 16;
        float plotH = bottom - top;

        Vector2 ToScreen(double rangeM, double altM) =>
            new(left + (float)(rangeM / _maxRange) * plotW,
                bottom - (float)((altM - _minAlt) / (_maxAlt - _minAlt)) * plotH);

        float gunY = ToScreen(0, 0).Y;   // screen y of gun / 0 m level

        // Frame.
        DrawLine(new Vector2(left, bottom), new Vector2(left + plotW, bottom), P.Border, 1);
        DrawLine(new Vector2(left, bottom), new Vector2(left, top), P.Border, 1);
        DrawString(font, new Vector2(12, top + 26), "ALT", HorizontalAlignment.Left, -1, 8, P.Faint);

        // Range gridlines + labels.
        for (int i = 1; i <= 4; i++)
        {
            float x = left + plotW * (i / 4f);
            DrawLine(new Vector2(x, top + 6), new Vector2(x, bottom), P.BorderSoft, 1);
            DrawString(font, new Vector2(x - 8, bottom + 12),
                $"{_maxRange * (i / 4f) / 1000:0}", HorizontalAlignment.Left, -1, 8, P.Faint);
        }

        // Altitude ticks: gun level (0), the top, and the floor if it is below the gun.
        DrawString(font, new Vector2(6, top + 4), $"+{_maxAlt / 1000:0.0}km", HorizontalAlignment.Left, -1, 8, P.Faint);
        if (_minAlt < 0)
            DrawString(font, new Vector2(6, bottom - 12), $"{_minAlt:0} m", HorizontalAlignment.Left, -1, 8, P.Faint);

        // Gun-level (horizon) reference line at 0 m — what the target altitude is measured against.
        DrawDashed(new Vector2(left, gunY), new Vector2(left + plotW, gunY), P.BorderSoft, 1f, 6, 5);
        DrawString(font, new Vector2(left + plotW - 70, gunY - 12), "GUN LEVEL 0 m",
            HorizontalAlignment.Left, -1, 8, P.Faint);

        // Gun marker + barrel lay angle — a FIXED-LENGTH stub showing only the elevation
        // you dialled in. Input echo, NOT a predicted arc (design pillar 2). A trajectory
        // is drawn only AFTER you commit, and it is the real one the oracle simulated.
        Vector2 gun = new(left, gunY);
        DrawCircle(gun, 3, P.Accent);
        float el = Mathf.DegToRad((float)AimElevation);
        Vector2 aimEnd = gun + new Vector2(Mathf.Cos(el), -Mathf.Sin(el)) * 110;
        DrawLine(gun, aimEnd, P.Accent, 2);
        DrawString(font, gun + new Vector2(40, -14), $"LAY {AimElevation:0.0}°",
            HorizontalAlignment.Left, -1, 9, P.Accent);

        // Target marker — at its true altitude relative to the gun (may be below it).
        Vector2 tgt = ToScreen(TargetRange, TargetAltitude);
        DrawRect(new Rect2(tgt - new Vector2(5, 5), new Vector2(10, 10)), P.Red, false, 1.5f);
        // A faint drop/lift line from gun level makes the altitude difference legible.
        DrawDashed(new Vector2(tgt.X, gunY), tgt, P.Red, 1f, 3, 3);
        string altTag = TargetAltitude >= 0 ? $"+{TargetAltitude:0} m" : $"{TargetAltitude:0} m";
        DrawString(font, tgt + new Vector2(-34, tgt.Y <= gunY ? -10 : 22),
            $"TGT {TargetRange / 1000:0.0}km · {altTag}", HorizontalAlignment.Left, -1, 8, P.Red);

        // Fired arc / ray — the ONLY trajectory ever drawn, and only after firing.
        // `Arc` is populated solely from the committed shot's real simulated points
        // (KineticStation/BeamStation.Fire); there is no pre-fire prediction path.
        if (Arc is { Count: > 1 })
        {
            var pts = new Vector2[Arc.Count];
            for (int i = 0; i < Arc.Count; i++)
                pts[i] = ToScreen(Arc[i].X, Arc[i].Y);
            Color c = FiredHit ? P.Accent : P.TextDim;
            DrawPolyline(pts, c, 1.8f, true);
            Vector2 lastp = pts[^1];
            Color ic = FiredHit ? P.Accent : P.Red;
            DrawLine(lastp + new Vector2(-5, -5), lastp + new Vector2(5, 5), ic, 1.6f);
            DrawLine(lastp + new Vector2(-5, 5), lastp + new Vector2(5, -5), ic, 1.6f);
        }
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
