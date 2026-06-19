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
    private static int _seedSeq = 4471;   // source of fresh seeds (NEW MISSION advances it)
    private static int _seed = -1;        // current mission seed (kept across tier/weapon changes)
    private static int _tier = 1;         // difficulty tier (selectable in the top bar)
    private static int _munIdx = 0;       // selected munition (index into Munitions.All)

    private Mission _mission = null!;
    private double _az, _el = 45, _zc = 0;
    private int _charge = 5;

    // Spotter / observation post (gun-relative polar), used for calibration feedback.
    private double _spotRange, _spotBearing;

    private LineEdit _azField = null!, _elField = null!, _zcField = null!, _chargeField = null!;
    private Label _v0Label = null!;
    private readonly List<ColorRect> _pips = new();

    protected override Palette BuildPalette() => Palette.Amber;

    protected override void OnNewMission() => _seed = _seedSeq++;

    protected override Control BuildTopBar()
    {
        if (_seed < 0) _seed = _seedSeq++;

        // Mission is needed for chip text — generate it up front.
        _mission = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Kinetic, MathFidelity: _tier, Triangulation: 0.3, Circumstance: 0.3, Seed: _seed));

        // Apply the chosen munition (the generator bakes in the default; swapping the
        // weapon only changes the round's ballistics, never the hidden target).
        var mun = Munitions.All[_munIdx];
        _mission = _mission with { KineticWeapon = new KineticWeapon("KINETIC ARTILLERY", mun), Splash = mun.Splash };

        // Do NOT pre-aim at the target — the player reads the bearing and lays the gun
        // themselves (that's half the game). Start pointing due north.
        _az = 0;
        ComputeSpotter();

        return MakeTopBar(
            "FCS-01 · STATION ALPHA · Kinetic artillery",
            new[]
            {
                ("WPN · KINETIC ARTILLERY", true),
                ("WORLD · " + _mission.World.Name, false),
                (_mission.Id, false),
            },
            "RELOAD CYCLE",
            _tier, 0, t => { _tier = t; (GetParent() as Main)?.ReloadStation(); });
    }

    /// <summary>Place a spotter off to one side of the target, deterministically from the seed.</summary>
    private void ComputeSpotter()
    {
        var o = _mission.KineticObserved!;
        int side = (_mission.Seed & 1) == 0 ? +1 : -1;
        double off = 35 + _mission.Seed % 35;                 // 35°..69° off the target line
        _spotBearing = ((o.Bearing + side * off) % 360 + 360) % 360;
        _spotRange = o.Range * (0.5 + (_mission.Seed >> 3) % 30 / 100.0); // 0.50..0.79 of target range
    }

    protected override Control BuildLeftPanel()
    {
        var panel = Ui.Panel(P.Panel, P.Border, pad: 16, borderW: 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 16);
        panel.AddChild(v);

        var o = _mission.KineticObserved!;
        var flags = _mission.Flags;

        // --- Environment --- only show what this tier's physics actually use, so the
        // panel never hands the player irrelevant data (e.g. air density before drag).
        v.AddChild(Ui.SectionHeader(P, "Environment", P.Accent, "MEASURED"));
        if (flags.Wind)
        {
            var windRow = new HBoxContainer();
            windRow.AddThemeConstantOverride("separation", 14);
            windRow.AddChild(new Compass { P = P, FromDeg = o.WindFrom });
            var windCol = new VBoxContainer();
            windCol.AddThemeConstantOverride("separation", 2);
            windCol.AddChild(Ui.Text("WIND VECTOR", P.Faint, 9));
            windCol.AddChild(Ui.Text($"{o.WindSpeed:0.0} m/s", P.Text, 21));
            windCol.AddChild(Ui.Text($"FROM {o.WindFrom:000}°", P.AccentDim, 11));
            windRow.AddChild(windCol);
            v.AddChild(windRow);
        }

        var envCells = new List<(string, string)>();
        if (_mission.TierIndex >= 1) envCells.Add(("ALTITUDE", $"{_mission.Environment!.SiteAltitude:0} m"));
        if (flags.VariableG) envCells.Add(("LOCAL g", $"{o.LocalG:0.000} m/s²"));
        if (flags.Drag) envCells.Add(("AIR TEMP", $"{o.AirTemp:0.0} °C"));
        if (flags.Drag) envCells.Add(("AIR DENSITY ρ", $"{o.AirDensity:0.000} kg/m³"));
        if (envCells.Count > 0)
            v.AddChild(MetricGrid(envCells.ToArray(), P.Text));
        else
            v.AddChild(Ui.Text("Vacuum · flat ground · constant g — no air, no wind.", P.Faint, 9));

        // --- Target observed ---
        v.AddChild(Ui.SectionHeader(P, "Target — Observed", P.Red, "SPOTTER"));
        var tgtCells = new List<(string, string)>
        {
            ("GROUND RANGE · 0.01 km", $"{o.Range / 1000:0.00} km"),
            ("BEARING · 0.1°", $"{o.Bearing:0.0} °"),
        };
        if (_mission.TierIndex >= 1) tgtCells.Add(("TGT ALTITUDE · 1 m", $"{o.Altitude:+0;-0;0} m"));
        tgtCells.Add(("MOTION", "STATIC"));
        v.AddChild(MetricGrid(tgtCells.ToArray(), new Color("e9ddc6")));
        v.AddChild(Ui.Text($"↳ OP-1 at {_spotRange / 1000:0.00} km · brg {_spotBearing:0.0}°. Range & drop are yours to solve.", P.Faint, 9));

        // --- Weapon configuration --- click the munition to cycle the loaded round.
        v.AddChild(Ui.SectionHeader(P, "Weapon Configuration", P.Accent));
        var m = _mission.KineticWeapon!.Munition;
        var munBtn = Ui.FlatButton(P, m.Name + "   ▾  (tap to change)", P.Text, P.Border, 12);
        munBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        munBtn.Pressed += () => { _munIdx = (_munIdx + 1) % Munitions.All.Count; (GetParent() as Main)?.ReloadStation(); };
        v.AddChild(munBtn);
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
        FireButton = fire;
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

        // Scale the rings to the engagement (now up to ~40 km), keeping the board's
        // plotted radius roughly constant so distant targets still fit on screen.
        double tr = _mission.KineticObserved!.Range;
        double ringStep = tr > 24000 ? 10000 : tr > 12000 ? 5000 : 2000;
        int ringCount = (int)Math.Clamp(Math.Ceiling(tr * 1.15 / ringStep) + 1, 4, 8);
        double coverage = ringStep * ringCount;
        Board.RingStepM = ringStep; Board.RingCount = ringCount;
        Board.PxPerMeter = (float)(300.0 / coverage);
        Board.TargetRange = tr;
        Board.TargetBearing = _mission.KineticObserved!.Bearing;
        Board.TargetLabel = "TGT · ARMOR";
        Board.AimAzimuth = _az;
        Board.HasSpotter = true;
        Board.SpotterRange = _spotRange;
        Board.SpotterBearing = _spotBearing;
        Board.SpotterLabel = "OP-1";

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
        Board.BeginImpactAnim();

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
        VPlane.BeginArcAnim();

        Color acc = r.Score.Hit ? P.Accent : P.Red;
        StartCooldown(3.5);

        if (r.Score.Hit)
        {
            AwardCareer(850);
            SetLastShot("◆ TARGET DESTROYED", acc,
                new[]
                {
                    ("SHOT NO.", ShotNo.ToString("00"), P.Text),
                    ("RESULT", "DIRECT HIT", acc),
                },
                "within splash tolerance.");
            return;
        }

        // Calibration: don't hand over the exact range/line error. Report what the
        // spotter at OP-1 sees instead — the round's range & bearing FROM the OP and how
        // far off the target it looked from there — and let the player triangulate.
        var o = _mission.KineticObserved!;
        Vector2 spot = Polar(_spotRange, _spotBearing);
        Vector2 impact = Polar(r.Trajectory.Impact.Range, r.Trajectory.Impact.Bearing);
        Vector2 tgt = Polar(o.Range, o.Bearing);
        Vector2 sToImpact = impact - spot;
        Vector2 sToTgt = tgt - spot;

        double opRange = sToImpact.Length();
        double opBrg = Bearing(sToImpact);
        double angOff = AngleBetween(sToTgt, sToImpact);
        // Sign: which side of the OP→target line the round fell (spotter's view).
        double cross = sToTgt.X * sToImpact.Y - sToTgt.Y * sToImpact.X;
        string side = cross > 0 ? "RIGHT of tgt" : cross < 0 ? "LEFT of tgt" : "on tgt line";

        SetLastShot("△ CALIBRATION SHOT", acc,
            new[]
            {
                ("SHOT NO.", ShotNo.ToString("00"), P.Text),
                ("OP-1 → IMPACT", $"{opRange / 1000:0.00} km", P.Text),
                ("OP-1 BEARING", $"{opBrg:0.0}°", P.Text),
                ("OFF TARGET", $"{angOff:0.0}° {side}", acc),
            },
            "spotter report — triangulate the correction from OP-1.");
    }

    /// <summary>Gun-relative polar (range m, compass bearing deg) → ENU east/north metres.</summary>
    private static Vector2 Polar(double range, double bearingDeg)
    {
        double br = Constants.DegToRad(bearingDeg);
        return new Vector2((float)(range * Math.Sin(br)), (float)(range * Math.Cos(br)));
    }

    private static double Bearing(Vector2 v)
    {
        double b = Mathf.RadToDeg(Mathf.Atan2(v.X, v.Y));
        return b < 0 ? b + 360 : b;
    }

    private static double AngleBetween(Vector2 a, Vector2 b)
    {
        double la = a.Length(), lb = b.Length();
        if (la < 1e-3 || lb < 1e-3) return 0;
        double cos = Math.Clamp((double)a.Dot(b) / (la * lb), -1.0, 1.0);
        return Math.Acos(cos) * 180.0 / Math.PI;
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
