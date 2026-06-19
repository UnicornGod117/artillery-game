using Godot;
using System;
using System.Collections.Generic;
using FiringSolution.Core;
using FiringSolution.Core.Models;
using FiringSolution.Core.Content;

namespace FiringSolution.Shell;

/// <summary>
/// Direction B — cool ice-glass surveillance, relativistic particle beam. Lead is
/// ~zero (near-c); the work is geometry &amp; energy. The target is a battlespace
/// COORDINATE (the player works out bearing/elevation), and the player dials the beam
/// SPEED β — the delivered pulse energy N·(γ−1)m₀c² must land inside the kill WINDOW.
/// Scored on two gates: pointing accuracy AND delivered energy.
/// </summary>
public partial class BeamStation : StationView
{
    private static int _seedSeq = 9120;   // source of fresh seeds (NEW MISSION advances it)
    private static int _seed = -1;        // current mission seed (kept across tier changes)
    private static int _tier = 3;         // difficulty tier (selectable; beam min is 1)
    private Mission _mission = null!;
    private double _az, _el, _zc = 0, _beta = 90.0; // particle speed, % of c

    private Label _betaLabel = null!;

    protected override Palette BuildPalette() => Palette.Ice;

    protected override void OnNewMission() => _seed = _seedSeq++;

    protected override Control BuildTopBar()
    {
        if (_seed < 0) _seed = _seedSeq++;

        _mission = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Beam, MathFidelity: _tier, Triangulation: 0.25, Circumstance: 0.6, Seed: _seed));
        // Do NOT pre-aim at the target. The bearing & elevation are the player's to work
        // out from the coordinates; start the emitter pointing due north, flat.
        _az = 0;
        _el = 0;

        return MakeTopBar(
            "DEW-02 · STATION BETA · Relativistic beam",
            new[]
            {
                ("WPN · RELATIVISTIC BEAM", true),
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
        // Rest energy carried by one pulse, N·m₀c², published in GJ so the whole speed
        // solve stays in GJ: γ = 1 + E_required / (N·m₀c²), β = √(1 − 1/γ²).
        double restPulseGJ = _mission.BeamWeapon!.ParticleCount * _mission.BeamWeapon!.RestEnergyJoules / 1e9;

        // Environment — only the figures the engagement actually uses. No bearing compass:
        // the bearing is exactly what the player must derive from the coordinates.
        v.AddChild(Ui.SectionHeader(P, "Environment", P.Accent, "MEASURED"));
        v.AddChild(MetricGrid(new[]
        {
            ("CLOSING VELOCITY", $"{o.Closing:0} m/s"),
            ("LOCAL g", $"{o.LocalG:0.00} m/s²"),
        }, P.Text));

        // Your position (emitter) — an absolute grid coordinate; the emitter is not the origin.
        v.AddChild(Ui.SectionHeader(P, "Your Position — Emitter", P.Accent, "GRID"));
        v.AddChild(MetricGrid(new[]
        {
            ("EASTING · km", $"{g.X / 1000:0.00}"),
            ("NORTHING · km", $"{g.Y / 1000:0.00}"),
            ("ALTITUDE · km", $"{g.Z / 1000:0.00}"),
        }, new Color("aebecb"), 14));

        // Target — a COORDINATE, not a bearing/range/elevation. Work the geometry yourself.
        v.AddChild(Ui.SectionHeader(P, "Target — Observed", P.Red, "SENSOR"));
        v.AddChild(MetricGrid(new[]
        {
            ("EASTING · km", $"{tgt.X / 1000:0.00}"),
            ("NORTHING · km", $"{tgt.Y / 1000:0.00}"),
            ("ALTITUDE · km", $"{tgt.Z / 1000:0.00}"),
            ("MOTION", "TRACKING"),
        }, new Color("e9dcdc")));
        v.AddChild(Ui.Text("↳ Near-c flight makes lead negligible — derive bearing & elevation from the coordinates.", P.Faint, 9));

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
            ("REST ENERGY N·m₀c² · GJ", $"{restPulseGJ:0.000}"),
        }, new Color("aebecb"), 13));

        return panel;
    }

    protected override Control BuildRightPanel()
    {
        var panel = Ui.Panel(P.Panel, P.Border, pad: 15, borderW: 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        panel.AddChild(v);

        v.AddChild(Ui.SectionHeader(P, "Firing Solution — Your Input", P.Accent));
        v.AddChild(Ui.Text("↳ point the emitter & dial the beam SPEED. The board never solves it.", P.Faint, 9));
        v.AddChild(Ui.Text("↳ required precision: pointing within the on-axis tolerance; the protons must land INSIDE the energy window.", P.AccentDim, 9));
        v.AddChild(Ui.Text("   solve the speed: γ = 1 + E/(N·m₀c²), then β = √(1 − 1/γ²). Too fast over-penetrates — don't just max it.", P.Faint, 8));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 11);
        grid.AddThemeConstantOverride("v_separation", 11);
        v.AddChild(grid);

        AddNumberField(grid, "AZIMUTH (x) · ° · ±0.1°", _az.ToString("0.0"), 0.1,
            d => { _az = d; Board.AimAzimuth = _az; Board.QueueRedraw(); });
        AddNumberField(grid, "ELEVATION (y) · ° · ±0.1°", _el.ToString("0.0"), 0.1,
            d => { _el = d; VPlane.AimElevation = _el; VPlane.QueueRedraw(); });
        AddNumberField(grid, "Z-CORR (cross) · ° · ±0.1°", _zc.ToString("+0.0;-0.0;0.0"), 0.1,
            d => { _zc = d; });
        AddNumberField(grid, "PARTICLE SPEED · % c · ±0.01", _beta.ToString("0.00"), 0.1,
            d => { _beta = d; RefreshRegime(); },
            clamp: d => Math.Clamp(d, 0, 99.9), format: "0.00");

        var fire = Ui.PrimaryButton(P, "◆  COMMIT & FIRE");
        fire.Pressed += Fire;
        FireButton = fire;
        v.AddChild(fire);

        // Beam parameters. Only published specs are shown: the kill ENERGY WINDOW the
        // protons must land in. β is now the player's input (echoed back), and γ stays
        // theirs to derive — the panel never pre-computes the Lorentz factor or the speed.
        var reg = Ui.Panel(P.PanelDeep, P.Border, pad: 12, borderW: 1);
        var rv = new VBoxContainer();
        rv.AddThemeConstantOverride("separation", 8);
        rv.AddChild(Ui.Text("KILL WINDOW", P.TextDim, 10));
        rv.AddChild(MetricGrid(new[]
        {
            ("REQUIRED ENERGY · GJ", $"{_mission.BeamObserved!.RequiredEnergyGJ:0.00}"),
            ("TOLERANCE · ± GJ", $"{_mission.BeamObserved!.ToleranceGJ:0.00}"),
        }, P.Text, 14));
        // Live β readout (echoes the player's input — NOT the resulting energy or γ).
        var pr = new HBoxContainer();
        pr.AddChild(Ui.Text("PARTICLE SPEED (set)", P.Faint, 9));
        pr.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _betaLabel = Ui.Text($"{_beta:0.00} % c", P.Accent, 14);
        pr.AddChild(_betaLabel);
        rv.AddChild(pr);
        reg.AddChild(rv);
        v.AddChild(reg);

        // Calculator (arithmetic only — opens a pop-up keypad, design §4).
        var calcBtn = Ui.FlatButton(P, "▦  SCIENTIFIC CALCULATOR · keypad", P.Accent, P.Border, 10);
        calcBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        calcBtn.Pressed += () => CalculatorView.Open(this, P);
        v.AddChild(calcBtn);

        // Handbook (opens the Core's formula reference) + Help / Give up.
        var hbk = Ui.FlatButton(P, "▤  HANDBOOK · Relativity / Thermal / Vectors", P.Accent, P.Border, 10);
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

    protected override void Configure()
    {
        Board.P = P; Board.IsBeam = true;
        Board.PxPerMeter = 0.0098f;
        Board.RingStepM = 10000; Board.RingCount = 4;
        Board.TargetRange = _mission.BeamObserved!.SlantRange;
        Board.TargetBearing = _mission.BeamObserved!.Bearing;
        Board.TargetLabel = "TGT · AIRCRAFT";
        Board.AimAzimuth = _az;
        Board.GunOriginM = new Vector2((float)_mission.GunOrigin.X, (float)_mission.GunOrigin.Y);

        VPlane.P = P; VPlane.IsBeam = true;
        VPlane.AimElevation = _el;
        double s = _mission.BeamObserved!.SlantRange;
        double losEl = Constants.DegToRad(_mission.BeamObserved!.LosElevation);
        VPlane.TargetRange = s * Math.Cos(losEl);
        VPlane.TargetAltitude = s * Math.Sin(losEl);
        VPlane.SetScale(s * 1.05, s * Math.Sin(losEl) * 1.6 + 1000);
    }

    protected override void Refresh() => RefreshRegime();

    private void RefreshRegime()
    {
        _betaLabel.Text = $"{_beta:0.00} % c";
    }

    private void Fire()
    {
        BeamResult r = GameEngine.FireBeam(_mission, _az, _el, _beta / 100.0);
        ShotNo++;

        Board.HasFired = true;
        Board.FiredRange = _mission.BeamObserved!.SlantRange;
        Board.FiredBearing = _az;
        Board.FiredHit = r.Score.Hit;
        Board.BeginImpactAnim();

        double el = Constants.DegToRad(_el);
        double s = _mission.BeamObserved!.SlantRange;
        VPlane.Arc = new List<Vector2>
        {
            new(0, 0),
            new((float)(s * Math.Cos(el)), (float)(s * Math.Sin(el))),
        };
        VPlane.FiredHit = r.Score.Hit;
        VPlane.BeginArcAnim();

        Color acc = r.Score.Hit ? P.Accent : P.Red;
        StartCooldown(45.0);
        if (r.Score.Hit) AwardCareer(1100);

        string heading = r.Score.Hit
            ? "◆ TARGET NEUTRALISED"
            : r.Score.OnAxis
                ? (r.Score.EnergyError < 0 ? "△ UNDER-POWERED" : "△ OVER-POWERED")
                : "△ OFF-AXIS";
        double deliveredGJ = r.Shot.PulseEnergyJoules / 1e9;
        SetLastShot(heading, acc,
            new[]
            {
                ("SHOT NO.", ShotNo.ToString("00"), P.Text),
                ("LATERAL MISS", $"{Math.Round(r.Score.LateralMiss)} m", acc),
                ("ANG. ERROR", $"{r.Score.AngError:0.00}°", P.Text),
                ("DELIVERED", $"{deliveredGJ:0.00} GJ", r.Score.EnergyOk ? new Color("aebecb") : P.Red),
            },
            r.Score.Hit ? "thermal kill confirmed."
                : r.Score.OnAxis
                    ? (r.Score.EnergyError < 0 ? "on-axis but flux too low — raise β." : "on-axis but over-penetrating — lower β.")
                    : "re-point the emitter.");
    }

    private void RevealSolution()
    {
        var t = _mission.BeamTarget!;
        double beta = GameEngine.RevealBeamBeta(_mission) * 100.0;
        SetLastShot("GIVE UP — SOLUTION", P.AccentDim,
            new[]
            {
                ("AZIMUTH", $"{t.Bearing:0.0}°", P.Text),
                ("ELEVATION", $"{t.LosElevation:0.0}°", P.Text),
                ("PARTICLE SPEED", $"{beta:0.00} % c", P.Text),
            },
            "one valid firing solution shown.");
    }
}
