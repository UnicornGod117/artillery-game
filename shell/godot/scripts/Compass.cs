using Godot;

namespace FiringSolution.Shell;

/// <summary>A small wind compass dial: rings, N tick, and a needle pointing FROM.</summary>
public partial class Compass : Control
{
    public Palette P = Palette.Amber;
    public double FromDeg = 295;

    public Compass() { CustomMinimumSize = new Vector2(78, 78); }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        Vector2 c = Size / 2;
        float r = Mathf.Min(Size.X, Size.Y) / 2 - 4;
        DrawArc(c, r, 0, Mathf.Tau, 48, P.Border, 1);
        DrawArc(c, r * 0.74f, 0, Mathf.Tau, 48, P.BorderSoft, 1);
        DrawLine(c + new Vector2(0, -r), c + new Vector2(0, -r + 7), P.Faint, 1);
        DrawString(font, c + new Vector2(-3, -r + 6), "N", HorizontalAlignment.Left, -1, 7, P.TextDim);

        // Needle points FROM the wind source direction.
        float a = Mathf.DegToRad((float)FromDeg);
        Vector2 dir = new(Mathf.Sin(a), -Mathf.Cos(a));
        DrawLine(c - dir * (r * 0.5f), c + dir * (r * 0.5f), P.Accent, 2);
        DrawCircle(c + dir * (r * 0.5f), 3, P.Accent);
        DrawCircle(c, 2.5f, P.PanelDeep);
        DrawArc(c, 2.5f, 0, Mathf.Tau, 12, P.Accent, 1);
    }
}
