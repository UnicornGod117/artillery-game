using Godot;
using System;
using System.Collections.Generic;
using FiringSolution.Core;
using FiringSolution.Core.Models;
using FiringSolution.Core.Content;

namespace FiringSolution.Shell;

/// <summary>
/// Direction A — amber-phosphor gunnery, kinetic artillery. The player reads the
/// situation, derives azimuth / elevation / charge by hand, commits, and the
/// Core simulates the true arc and stamps the impact.
/// </summary>
public partial class KineticStation : StationView
{
    private static int _seedSeq = 4471;   // advances each new mission
    private Mission _mission = null!;
    private double _az, _el = 45, _zc = 0;
    private int _charge = 5;

    private LineEdit _azField = null!, _elField = null!, _zcField = null!, _chargeField = null!;
    private Label _v0Label = null!;
    private readonly List<ColorRect> _pips = new();

    protected override Palette BuildPalette() => Palette.Amber;

    protected override Control BuildTopBar()
    {
        // Mission is needed for chip text — generate it up front.
        _mission = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Kinetic, MathFidelity: 1, Triangulation: 0.3, Circumstance: 0.3, Seed: _seedSeq++));
        _az = Math.Round(_mission.KineticObserved!.Bearing);

        return MakeTopBar(
            "FCS-01 · STATION ALPHA · Kinetic artillery",
            new[]
            {
                ("WPN · KINETIC ARTILLERY", true),
                ("WORLD · " + _mission.World.Name, false),
                ("TIER · " + _mission.TierLabel, false),
                (_mission.Id, false),
            },
            "RELOAD CYCLE");
    }

    protected override Control BuildLeftPanel()
    {
        var panel = Ui.Panel(P.Panel, P.Border, pad: 16, borderW: 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 16);
        panel.AddChild(v);

        var o = _mission.KineticObserved!;

        // --- Environment ---
        v.AddChild(Ui.SectionHeader(P, "Environment", P.Accent, "MEASURED"));
        var windRow = new HBoxContainer();
        windRow.AddThemeConstantOverride("separation", 14);
        var compass = new Compass { P = P, FromDeg = o.WindFrom };
        windRow.AddChild(compass);
        var windCol = new VBoxContainer();
        windCol.AddThemeConstantOverride("separation", 2);
        windCol.AddChild(Ui.Text("WIND VECTOR", P.Faint, 9));
        windCol.AddChild(Ui.Text($"{o.WindSpeed:0.0} m/s", P.Text, 21));
        windCol.AddChild(Ui.Text($"FROM {o.WindFrom:000}°", P.AccentDim, 11));
        windRow.AddChild(windCol);
        v.AddChild(windRow);
        v.AddChild(MetricGrid(new[]
        {
            ("ALTITUDE", $"{_mission.Environment!.SiteAltitude:0} m"),
            ("AIR TEMP", $"{o.AirTemp:0.0} °C"),
            ("AIR DENSITY ρ", $"{o.AirDensity:0.000} kg/m³"),
            ("LOCAL g", $"{o.LocalG:0.000} m/s²"),
        }, P.Text));

        // --- Target observed ---
        v.AddChild(Ui.SectionHeader(P, "Target — Observed", P.Red, "SPOTTER"));
        v.AddChild(MetricGrid(new[]
        {
            ("GROUND RANGE · 0.01 km", $"{o.Range / 1000:0.00} km"),
            ("BEARING · 0.1°", $"{o.Bearing:0.0} °"),
            ("TGT ALTITUDE · 1 m", $"{o.Altitude:+0;-0;0} m"),
            ("MOTION", "STATIC"),
        }, new Color("e9ddc6")));
        v.AddChild(Ui.Text("↳ Localised from OP-1 / OP-2. Range & drop are yours to solve.", P.Faint, 9));

        // --- Weapon configuration ---
        v.AddChild(Ui.SectionHeader(P, "Weapon Configuration", P.Accent));
        var munBox = Ui.Panel(P.PanelDeep, P.Border, pad: 9, borderW: 1);
        var munRow = new HBoxContainer();
        munRow.AddChild(Ui.Text(_mission.KineticWeapon!.Munition.Name, P.Text, 12));
        munRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        munRow.AddChild(Ui.Text("▾", P.Faint, 9));
        munBox.AddChild(munRow);
        v.AddChild(munBox);
        var m = _mission.KineticWeapon!.Munition;
        v.AddChild(MetricGrid(new[]
        {
            ("SHELL MASS", $"{m.Mass:0.0} kg"),
            ("BALLISTIC COEF", $"{m.DragCoeff:0.000}"),
        }, new Color("cdbf9f"), 13));

        return panel;
    }

    protected override Control BuildRightPanel()
    {
        var panel = Ui.Panel(P.Panel, P.Border, pad: 15, borderW: 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        panel.AddChild(v);

        v.AddChild(Ui.SectionHeader(P, "Firing Solution — Your Input", P.Accent));
        v.AddChild(Ui.Text("↳ type, or use the steppers. Nothing here is computed for you.", P.Faint, 9));
        v.AddChild(Ui.Text("↳ required precision: azimuth & elevation to 0.1°, charge is an integer 1–7.", P.AccentDim, 9));
        v.AddChild(Ui.Text("   near a charge's max range the arc is steep — type elevation for full 0.1° control.", P.Faint, 8));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 11);
        grid.AddThemeConstantOverride("v_separation", 11);
        v.AddChild(grid);

        _azField = AddNumberField(grid, "AZIMUTH (x) · ° · ±0.1°", _az.ToString("0.0"), 0.1,
            d => { _az = d; Board.AimAzimuth = _az; Board.QueueRedraw(); });
        _elField = AddNumberField(grid, "ELEVATION (y) · ° · ±0.1°", _el.ToString("0.0"), 0.1,
            d => { _el = d; VPlane.AimElevation = _el; VPlane.QueueRedraw(); });
        _zcField = AddNumberField(grid, "Z-CORR (cross) · ° · ±0.1°", _zc.ToString("+0.0;-0.0;0.0"), 0.1,
            d => { _zc = d; });
        _chargeField = AddNumberField(grid, "PROPELLANT CHARGE · 1–7", _charge.ToString(), 1.0,
            d => { _charge = (int)Math.Round(d); UpdatePips(); RefreshV0(); },
            isInt: true,
            clamp: d => Math.Clamp(Math.Round(d), 1, _mission.KineticWeapon!.MaxCharge));

        // Charge pips.
        var pipRow = new HBoxContainer();
        pipRow.AddThemeConstantOverride("separation", 5);
        for (int i = 0; i < 7; i++)
        {
            var pip = new ColorRect { CustomMinimumSize = new Vector2(0, 15), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _pips.Add(pip);
            pipRow.AddChild(pip);
        }
        v.AddChild(pipRow);

        var v0Row = new HBoxContainer();
        v0Row.AddChild(Ui.Text("MUZZLE VELOCITY v₀ (from charge)", P.TextDim, 10));
        v0Row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _v0Label = Ui.Text("", P.Text, 10);
        v0Row.AddChild(_v0Label);
        v.AddChild(v0Row);

        var fire = Ui.PrimaryButton(P, "◆  COMMIT & FIRE");
        fire.Pressed += Fire;
        v.AddChild(fire);

        // Calculator (arithmetic only — opens a pop-up keypad, design §4).
        var calcBtn = Ui.FlatButton(P, "▦  SCIENTIFIC CALCULATOR · keypad", P.Accent, P.Border, 10);
        calcBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        calcBtn.Pressed += () => CalculatorView.Open(this, P);
        v.AddChild(calcBtn);

        // Handbook (opens the Core's formula reference) + Help / Give up.
        var hbk = Ui.FlatButton(P, "▤  HANDBOOK · Ballistics / Trig / Relativity", P.Accent, P.Border, 10);
        hbk.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbk.Pressed += () => HandbookView.Open(this, P, Handbook.HelpHint(_mission.TierName));
        v.AddChild(hbk);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 9);
        var help = Ui.FlatButton(P, "HELP", P.AccentDim, P.Border, 10);
        help.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var give = Ui.FlatButton(P, "GIVE UP", P.Faint, P.Border, 10);
        give.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        help.Pressed += () => SetLastShot("HELP — WHICH EQUATIONS APPLY", P.AccentDim,
            Array.Empty<(string, string, Color)>(),
            Handbook.HelpHint(_mission.TierName));
        give.Pressed += RevealSolution;
        actions.AddChild(help);
        actions.AddChild(give);
        v.AddChild(actions);

        return panel;
    }

    protected override void Configure()
    {
        Board.P = P; Board.IsBeam = false;
        Board.PxPerMeter = 0.038f;
        Board.RingStepM = 2000; Board.RingCount = 4;
        Board.TargetRange = _mission.KineticObserved!.Range;
        Board.TargetBearing = _mission.KineticObserved!.Bearing;
        Board.TargetLabel = "TGT · ARMOR";
        Board.AimAzimuth = _az;

        VPlane.P = P; VPlane.IsBeam = false;
        VPlane.AimElevation = _el;
        VPlane.TargetRange = _mission.KineticObserved!.Range;
        VPlane.TargetAltitude = _mission.KineticObserved!.Altitude;
        // If the target sits below the gun, open the view below the gun-level line.
        double tAlt = _mission.KineticObserved!.Altitude;
        double floor = tAlt < 0 ? tAlt - System.Math.Abs(tAlt) * 0.25 - 100 : 0;
        VPlane.SetScale(_mission.KineticObserved!.Range * 1.25, 4000, floor);
    }

    protected override void Refresh()
    {
        UpdatePips();
        RefreshV0();
    }

    private void RefreshV0()
        => _v0Label.Text = $"{_mission.KineticWeapon!.MuzzleVelocity(_charge):0} m/s";

    private void UpdatePips()
    {
        for (int i = 0; i < _pips.Count; i++)
            _pips[i].Color = i < _charge ? P.Accent : P.BorderSoft;
    }

    private void Fire()
    {
        KineticResult r = GameEngine.FireKinetic(_mission, _az, _el, _charge);
        ShotNo++;

        Board.HasFired = true;
        Board.FiredRange = r.Trajectory.Impact.Range;
        Board.FiredBearing = r.Trajectory.Impact.Bearing;
        Board.FiredHit = r.Score.Hit;
        Board.QueueRedraw();

        var arc = new List<Vector2>();
        double arcMinAlt = 0;
        foreach (var pt in r.Trajectory.Points)
        {
            arc.Add(new Vector2((float)Math.Sqrt(pt.X * pt.X + pt.Y * pt.Y), (float)pt.Z));
            arcMinAlt = Math.Min(arcMinAlt, pt.Z);
        }
        VPlane.Arc = arc;
        VPlane.FiredHit = r.Score.Hit;
        double lo = Math.Min(arcMinAlt, Math.Min(0, _mission.KineticTarget!.Altitude));
        double floor = lo < 0 ? lo - Math.Abs(lo) * 0.1 - 50 : 0;
        VPlane.SetScale(Math.Max(_mission.KineticTarget!.Range, r.Trajectory.Impact.Range) * 1.1,
                        Math.Max(r.Trajectory.Apex * 1.2, 500), floor);
        VPlane.QueueRedraw();

        string rng = $"{Math.Abs(r.Score.RangeError):0} m {(r.Score.RangeError > 1 ? "LONG" : r.Score.RangeError < -1 ? "SHORT" : "ON RANGE")}";
        string line = $"{Math.Abs(r.Score.LineError):0} m {(r.Score.LineError > 1 ? "LEFT" : r.Score.LineError < -1 ? "RIGHT" : "ON LINE")}";
        Color acc = r.Score.Hit ? P.Accent : P.Red;
        if (r.Score.Hit) AwardCareer(850);

        SetLastShot(
            r.Score.Hit ? "◆ TARGET DESTROYED" : "△ CALIBRATION SHOT", acc,
            new[]
            {
                ("SHOT NO.", ShotNo.ToString("00"), P.Text),
                ("MISS DIST.", $"{Math.Round(r.Score.Miss)} m", acc),
                ("RANGE", rng, P.Text),
                ("LINE", line, P.Text),
            },
            r.Score.Hit ? "within splash tolerance." : "apply correction & re-fire.");
    }

    private void RevealSolution()
    {
        // Give up: brute-force the engine to surface a working solution (graceful exit).
        var sol = GameEngine.RevealKineticSolution(_mission);
        if (sol is { } s)
            SetLastShot("GIVE UP — SOLUTION", P.AccentDim,
                new[]
                {
                    ("AZIMUTH", $"{s.Azimuth:0.0}°", P.Text),
                    ("ELEVATION", $"{s.Elevation:0.0}°", P.Text),
                    ("CHARGE", s.Charge.ToString(), P.Text),
                },
                "one valid firing solution shown.");
        else
            SetLastShot("GIVE UP", P.Red, Array.Empty<(string, string, Color)>(), "no closed solution found.");
    }

}
