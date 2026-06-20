// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using Godot;
using System;
using System.Collections.Generic;
using FiringSolution.Core;
using FiringSolution.Core.Models;
using FiringSolution.Core.Content;

namespace FiringSolution.Shell;

/// <summary>
/// Direction B — cool ice-glass surveillance, relativistic particle WARHEAD. A
/// long-range (light-second) intercept: the warhead carries a fuse that fires after
/// τ seconds on its OWN clock, and time dilation stretches that to γ·τ for us, so it
/// detonates at d = βγ·c·τ. The player works bearing/elevation/range out of the target
/// COORDINATE, then solves the launch SPEED β whose dilation lands the blast on target.
/// Scored on two gates: pointing accuracy AND detonation range.
/// </summary>
public partial class BeamStation : StationView
{
    private static int _seedSeq = 9120;   // source of fresh seeds (NEW MISSION advances it)
    private static int _seed = -1;        // current mission seed (kept across tier changes)
    private static int _tier = 3;         // difficulty tier (selectable; beam min is 1)
    private Mission _mission = null!;
    private double _az, _el, _zc = 0, _beta = 90.0; // particle speed, % of c

    private Label _betaLabel = null!;

    private double Ls => Constants.C; // metres per light-second

    protected override Palette BuildPalette() => Palette.Ice;

    protected override void OnNewMission() => _seed = _seedSeq++;

    protected override Control BuildTopBar()
    {
        if (_seed < 0) _seed = _seedSeq++;

        _mission = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Beam, MathFidelity: _tier, Triangulation: 0.25, Circumstance: 0.6, Seed: _seed));
        // Do NOT pre-aim. Bearing, elevation, range and speed are all the player's to work
        // out from the coordinates; start the emitter pointing due north, flat, slow.
        _az = 0;
        _el = 0;

        return MakeTopBar(
            "DEW-02 · STATION BETA · Relativistic warhead",
            new[]
            {
                ("WPN · RELATIVISTIC WARHEAD", true),
                ("WORLD · " + _mission.World.Name, false),
                (_mission.Id, false),
            },
            "CAPACITOR CHARGE",
            _tier, 1, t => { _tier = t; (GetParent() as Main)?.ReloadStation(); });
    }

    /// <summary>Observed gun-relative geometry → absolute battlespace coordinate (ENU metres).</summary>
    private Vec3 TargetAbs()
    {
        var o = _mission.BeamObserved!;
        double losR = Constants.DegToRad(o.LosElevation);
        double horiz = o.SlantRange * Math.Cos(losR);
        double up = o.SlantRange * Math.Sin(losR);
        double br = Constants.DegToRad(o.Bearing);
        var g = _mission.GunOrigin;
        return new Vec3(g.X + horiz * Math.Sin(br), g.Y + horiz * Math.Cos(br), g.Z + up);
    }

    protected override Control BuildLeftPanel()
    {
        var panel = Ui.Panel(P.Panel, P.Border, pad: 16, borderW: 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 16);
        panel.AddChild(v);
        var o = _mission.BeamObserved!;
        Vec3 g = _mission.GunOrigin;
        Vec3 tgt = TargetAbs();

        // Environment — closing velocity of the intercept (flavour). No bearing compass:
        // the bearing is exactly what the player must derive from the coordinates.
        v.AddChild(Ui.SectionHeader(P, "Track", P.Accent, "MEASURED"));
        v.AddChild(MetricGrid(new[]
        {
            ("CLOSING VELOCITY", $"{o.Closing / 1000:0.00} km/s"),
            ("WARHEAD FUSE τ", $"{o.FuseSeconds:0} s"),
        }, P.Text));

        // Your position (emitter) — an absolute grid coordinate in light-seconds.
        v.AddChild(Ui.SectionHeader(P, "Your Position — Emitter", P.Accent, "GRID · ls"));
        v.AddChild(MetricGrid(new[]
        {
            ("EASTING · ls", $"{g.X / Ls:0.000}"),
            ("NORTHING · ls", $"{g.Y / Ls:0.000}"),
            ("ALTITUDE · ls", $"{g.Z / Ls:0.000}"),
        }, new Color("aebecb"), 14));

        // Target — a COORDINATE, not a bearing/range/elevation. Work the geometry yourself:
        // bearing & elevation to point, and the slant range R for the speed solve.
        v.AddChild(Ui.SectionHeader(P, "Target — Observed", P.Red, "SENSOR · ls"));
        v.AddChild(MetricGrid(new[]
        {
            ("EASTING · ls", $"{tgt.X / Ls:0.000}"),
            ("NORTHING · ls", $"{tgt.Y / Ls:0.000}"),
            ("ALTITUDE · ls", $"{tgt.Z / Ls:0.000}"),
            ("MOTION", "TRACKING"),
        }, new Color("e9dcdc")));
        v.AddChild(Ui.Text("↳ Lead ≈ 0. Derive bearing, elevation & the slant range R from the coordinates.", P.Faint, 9));

        v.AddChild(Ui.SectionHeader(P, "Weapon Configuration", P.Accent));
        var profBox = Ui.Panel(P.PanelDeep, P.Border, pad: 9, borderW: 1);
        var pr = new HBoxContainer();
        pr.AddChild(Ui.Text(_mission.BeamWeapon!.ProfileName, P.Text, 12));
        pr.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        pr.AddChild(Ui.Text("▾", P.Faint, 9));
        profBox.AddChild(pr);
        v.AddChild(profBox);
        v.AddChild(MetricGrid(new[]
        {
            ("REST MASS m₀", "938 MeV/c²"),
            ("PROTONS / PULSE N", $"{_mission.BeamWeapon!.ParticleCount:0.0e0}"),
        }, new Color("aebecb"), 13));

        return panel;
    }

    protected override Control BuildRightPanel()
    {
        var panel = Ui.Panel(P.Panel, P.Border, pad: 15, borderW: 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        panel.AddChild(v);
        var o = _mission.BeamObserved!;

        v.AddChild(Ui.SectionHeader(P, "Firing Solution — Your Input", P.Accent));
        v.AddChild(Ui.Text("↳ point the emitter & dial the launch SPEED. The board never solves it.", P.Faint, 9));
        v.AddChild(Ui.Text("↳ the fuse fires after τ on the warhead's clock; dilation stretches it to γτ, so it detonates at d = βγcτ.", P.AccentDim, 9));
        v.AddChild(Ui.Text("   solve β to land d on the target: k = R/(c·τ) = (R in light-seconds)/τ,  β = k/√(1 + k²). Too fast overshoots.", P.Faint, 8));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 11);
        grid.AddThemeConstantOverride("v_separation", 11);
        v.AddChild(grid);

        AddNumberField(grid, "AZIMUTH (x) · ° · ±0.01°", _az.ToString("0.00"), 0.1,
            d => { _az = d; Board.AimAzimuth = _az; Board.QueueRedraw(); }, format: "0.00");
        AddNumberField(grid, "ELEVATION (y) · ° · ±0.01°", _el.ToString("0.00"), 0.1,
            d => { _el = d; VPlane.AimElevation = _el; VPlane.QueueRedraw(); }, format: "0.00");
        AddNumberField(grid, "Z-CORR (cross) · ° · ±0.01°", _zc.ToString("+0.00;-0.00;0.00"), 0.1,
            d => { _zc = d; }, format: "+0.00;-0.00;0.00");
        AddNumberField(grid, "LAUNCH SPEED · % c · ±0.0001", _beta.ToString("0.0000"), 0.01,
            d => { _beta = d; RefreshRegime(); },
            clamp: d => Math.Clamp(d, 0, 99.9999), format: "0.0000");

        var fire = Ui.PrimaryButton(P, "◆  COMMIT & FIRE");
        fire.Pressed += Fire;
        FireButton = fire;
        v.AddChild(fire);

        // Intercept parameters. Only published specs are shown: the fuse τ and the
        // detonation window the warhead must land in. β is the player's input (echoed back);
        // γ and R stay theirs to derive — the panel never pre-computes them.
        var reg = Ui.Panel(P.PanelDeep, P.Border, pad: 12, borderW: 1);
        var rv = new VBoxContainer();
        rv.AddThemeConstantOverride("separation", 8);
        rv.AddChild(Ui.Text("INTERCEPT", P.TextDim, 10));
        rv.AddChild(MetricGrid(new[]
        {
            ("FUSE τ · s", $"{o.FuseSeconds:0}"),
            ("DET. WINDOW · ± ls", $"{o.DetonationToleranceMeters / Ls:0.000}"),
        }, P.Text, 14));
        var betaRow = new HBoxContainer();
        betaRow.AddChild(Ui.Text("LAUNCH SPEED (set)", P.Faint, 9));
        betaRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _betaLabel = Ui.Text($"{_beta:0.0000} % c", P.Accent, 14);
        betaRow.AddChild(_betaLabel);
        rv.AddChild(betaRow);
        reg.AddChild(rv);
        v.AddChild(reg);

        // Calculator (arithmetic only — opens a pop-up keypad, design §4).
        var calcBtn = Ui.FlatButton(P, "▦  SCIENTIFIC CALCULATOR · keypad", P.Accent, P.Border, 10);
        calcBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        calcBtn.Pressed += () => CalculatorView.Open(this, P);
        v.AddChild(calcBtn);

        // Handbook (opens the Core's formula reference) + Help / Give up.
        var hbk = Ui.FlatButton(P, "▤  HANDBOOK · Relativity / Dilation / Vectors", P.Accent, P.Border, 10);
        hbk.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbk.Pressed += () => HandbookView.Open(this, P, Handbook.HelpHint("beam"));
        v.AddChild(hbk);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 9);
        var help = Ui.FlatButton(P, "HELP", P.AccentDim, P.Border, 10);
        help.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var give = Ui.FlatButton(P, "GIVE UP", P.Faint, P.Border, 10);
        give.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        help.Pressed += () => SetLastShot("HELP — WHICH EQUATIONS APPLY", P.AccentDim,
            Array.Empty<(string, string, Color)>(), Handbook.HelpHint("beam"));
        give.Pressed += RevealSolution;
        actions.AddChild(help);
        actions.AddChild(give);
        v.AddChild(actions);

        return panel;
    }

    /// <summary>Round up to a tidy 1/2/5×10ⁿ step for the range rings.</summary>
    private static double NiceStep(double x)
    {
        if (x <= 0) return 1;
        double pow = Math.Pow(10, Math.Floor(Math.Log10(x)));
        double m = x / pow;
        double nice = m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10;
        return nice * pow;
    }

    protected override void Configure()
    {
        double R = _mission.BeamObserved!.SlantRange;

        Board.P = P; Board.IsBeam = true;
        Board.UnitMeters = Ls; Board.UnitLabel = "ls";
        // Auto-scale the rings to the (light-second) engagement, keeping the plotted radius
        // roughly constant so the long-range box still fits on screen.
        double ringStep = NiceStep(R / Ls / 4) * Ls;
        int ringCount = (int)Math.Clamp(Math.Ceiling(R * 1.15 / ringStep) + 1, 4, 8);
        double coverage = ringStep * ringCount;
        Board.RingStepM = ringStep; Board.RingCount = ringCount;
        Board.PxPerMeter = (float)(300.0 / coverage);
        Board.TargetRange = R;
        Board.TargetBearing = _mission.BeamObserved!.Bearing;
        Board.TargetLabel = "TGT · ORBITAL ASSET";
        Board.AimAzimuth = _az;
        Board.GunOriginM = new Vector2((float)_mission.GunOrigin.X, (float)_mission.GunOrigin.Y);

        VPlane.P = P; VPlane.IsBeam = true;
        VPlane.UnitMeters = Ls; VPlane.UnitLabel = "ls";
        VPlane.AimElevation = _el;
        double losEl = Constants.DegToRad(_mission.BeamObserved!.LosElevation);
        VPlane.TargetRange = R * Math.Cos(losEl);
        VPlane.TargetAltitude = R * Math.Sin(losEl);
        VPlane.SetScale(R * 1.05, R * Math.Sin(losEl) * 1.6 + Ls * 0.05);
    }

    protected override void Refresh() => RefreshRegime();

    private void RefreshRegime()
    {
        _betaLabel.Text = $"{_beta:0.0000} % c";
    }

    private void Fire()
    {
        double beta = _beta / 100.0;
        BeamResult r = GameEngine.FireBeam(_mission, _az, _el, beta);
        ShotNo++;

        double d = r.Score.DetonationDistance;                 // where the dilated fuse fired
        double tDet = beta > 1e-9 ? d / (beta * Constants.C) : 0.01; // = γ·τ, the real flight time

        Board.HasFired = true;
        Board.FiredRange = d;
        Board.FiredBearing = _az;
        Board.FiredHit = r.Score.Hit;

        double el = Constants.DegToRad(_el);
        VPlane.Arc = new List<Vector2>
        {
            new(0, 0),
            new((float)(d * Math.Cos(el)), (float)(d * Math.Sin(el))),
        };
        VPlane.FiredHit = r.Score.Hit;

        // One sim clock drives both views over the warhead's REAL (time-dilated) flight time.
        BeginShotAnimation(tDet);

        Color acc = r.Score.Hit ? P.Accent : P.Red;
        StartCooldown(45.0);
        if (r.Score.Hit) AwardCareer(1400);

        string heading = r.Score.Hit ? "◆ TARGET NEUTRALISED"
            : !r.Score.OnAxis ? "△ OFF-AXIS"
            : r.Score.RangeError < 0 ? "△ DETONATED SHORT" : "△ OVERSHOT";
        double errLs = r.Score.RangeError / Ls;
        SetLastShot(heading, acc,
            new[]
            {
                ("SHOT NO.", ShotNo.ToString("00"), P.Text),
                ("ANG. ERROR", $"{r.Score.AngError:0.000}°", P.Text),
                ("DET. RANGE ERR", $"{errLs:+0.000;-0.000} ls", acc),
                ("FLIGHT TIME", $"{tDet:0.0} s", P.Text),
            },
            r.Score.Hit ? "warhead detonated on target."
                : !r.Score.OnAxis ? "re-point the emitter."
                    : r.Score.RangeError < 0 ? "fuse fired short — raise β for more dilation."
                        : "overshot — lower β.");
    }

    private void RevealSolution()
    {
        var t = _mission.BeamTarget!;
        double beta = GameEngine.RevealBeamBeta(_mission) * 100.0;
        SetLastShot("GIVE UP — SOLUTION", P.AccentDim,
            new[]
            {
                ("AZIMUTH", $"{t.Bearing:0.00}°", P.Text),
                ("ELEVATION", $"{t.LosElevation:0.00}°", P.Text),
                ("LAUNCH SPEED", $"{beta:0.0000} % c", P.Text),
            },
            "one valid firing solution shown.");
    }
}
