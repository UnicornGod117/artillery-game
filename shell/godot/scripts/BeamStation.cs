using Godot;
using System;
using System.Collections.Generic;
using FiringSolution.Core;
using FiringSolution.Core.Models;
using FiringSolution.Core.Content;

namespace FiringSolution.Shell;

/// <summary>
/// Direction B — cool ice-glass surveillance, relativistic particle beam. Lead
/// is ~zero (near-c); the work is energy &amp; γ. Scored on two gates: pointing
/// accuracy AND delivered pulse energy ≥ kill threshold.
/// </summary>
public partial class BeamStation : StationView
{
    private static int _seedSeq = 9120;   // source of fresh seeds (NEW MISSION advances it)
    private static int _seed = -1;        // current mission seed (kept across tier changes)
    private static int _tier = 3;         // difficulty tier (selectable; beam min is 1)
    private Mission _mission = null!;
    private double _az, _el, _zc = 0, _en = 4.2; // pulse energy, GJ

    private Label _enLabel = null!;

    protected override Palette BuildPalette() => Palette.Ice;

    protected override void OnNewMission() => _seed = _seedSeq++;

    protected override Control BuildTopBar()
    {
        if (_seed < 0) _seed = _seedSeq++;

        _mission = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Beam, MathFidelity: _tier, Triangulation: 0.25, Circumstance: 0.6, Seed: _seed));
        _az = Math.Round(_mission.BeamObserved!.Bearing);
        _el = Math.Round(_mission.BeamObserved!.LosElevation);

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

    protected override Control BuildLeftPanel()
    {
        var panel = Ui.Panel(P.Panel, P.Border, pad: 16, borderW: 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 16);
        panel.AddChild(v);
        var o = _mission.BeamObserved!;

        // Real mission geometry: engagement altitude from the line-of-sight triangle.
        double losRad = Constants.DegToRad(o.LosElevation);
        double tgtAltKm = o.SlantRange * Math.Sin(losRad) / 1000.0;

        v.AddChild(Ui.SectionHeader(P, "Environment", P.Accent, "MEASURED"));
        var windRow = new HBoxContainer();
        windRow.AddThemeConstantOverride("separation", 14);
        windRow.AddChild(new Compass { P = P, FromDeg = o.Bearing });
        var wc = new VBoxContainer();
        wc.AddThemeConstantOverride("separation", 2);
        wc.AddChild(Ui.Text("CLOSING VELOCITY", P.Faint, 9));
        wc.AddChild(Ui.Text($"{o.Closing:0} m/s", P.Text, 21));
        wc.AddChild(Ui.Text($"BEARING {o.Bearing:000.0}°", P.AccentDim, 11));
        windRow.AddChild(wc);
        v.AddChild(windRow);
        // Only the data the engagement actually turns on — pointing is set by the LOS
        // geometry; thermal/air figures the model doesn't use are left off the panel.
        v.AddChild(MetricGrid(new[]
        {
            ("TGT ALTITUDE", $"{tgtAltKm:0.0} km"),
            ("LOCAL g", $"{o.LocalG:0.00} m/s²"),
        }, P.Text));

        v.AddChild(Ui.SectionHeader(P, "Target — Observed", P.Red, "SENSOR"));
        v.AddChild(MetricGrid(new[]
        {
            ("SLANT RANGE · 0.1 km", $"{o.SlantRange / 1000:0.0} km"),
            ("BEARING · 0.1°", $"{o.Bearing:0.0} °"),
            ("LOS ELEVATION · 0.1°", $"{o.LosElevation:0.0} °"),
            ("CLOSING · 1 m/s", $"{o.Closing:0} m/s"),
        }, P.Text));
        v.AddChild(Ui.Text("↳ Near-c flight makes lead negligible — the work is energy & γ.", P.Faint, 9));

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
            ("BEAM β", $"{_mission.BeamWeapon!.Beta:0.000} c"),
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
        v.AddChild(Ui.Text("↳ point the emitter & set pulse energy. The board never solves it.", P.Faint, 9));
        v.AddChild(Ui.Text("↳ required precision: pointing within the on-axis tolerance; deliver E ≥ the shown kill threshold (0.1 GJ).", P.AccentDim, 9));

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
        AddNumberField(grid, "PULSE ENERGY · GJ · ±0.1", _en.ToString("0.0"), 0.5,
            d => { _en = d; RefreshRegime(); },
            clamp: d => Math.Max(0, d));

        var fire = Ui.PrimaryButton(P, "◆  COMMIT & FIRE");
        fire.Pressed += Fire;
        FireButton = fire;
        v.AddChild(fire);

        // Beam parameters. Only the published specs are shown: the beam's β (a weapon
        // figure) and the kill threshold. γ and the rest are the player's to derive —
        // the panel no longer states what to solve for or pre-computes the Lorentz factor.
        double beta = _mission.BeamWeapon!.Beta;
        var reg = Ui.Panel(P.PanelDeep, P.Border, pad: 12, borderW: 1);
        var rv = new VBoxContainer();
        rv.AddThemeConstantOverride("separation", 8);
        rv.AddChild(Ui.Text("BEAM PARAMETERS", P.TextDim, 10));
        rv.AddChild(MetricGrid(new[]
        {
            ("BEAM β", $"{beta:0.000} c"),
            ("KILL THRESHOLD · GJ", $"≥ {_mission.BeamObserved!.KillEnergyGJ:0.0}"),
        }, P.Text, 14));
        // Live pulse-energy readout (echoes the player's input).
        var pr = new HBoxContainer();
        pr.AddChild(Ui.Text("PULSE ENERGY (set)", P.Faint, 9));
        pr.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _enLabel = Ui.Text($"{_en:0.0} GJ", P.Accent, 14);
        pr.AddChild(_enLabel);
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
        _enLabel.Text = $"{_en:0.0} GJ";
    }

    private void Fire()
    {
        BeamResult r = GameEngine.FireBeam(_mission, _az, _el, _en * 1e9);
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
        StartCooldown(4.0);
        if (r.Score.Hit) AwardCareer(1100);
        string heading = r.Score.Hit ? "◆ TARGET NEUTRALISED" : (r.Score.OnAxis ? "△ UNDER-POWERED" : "△ OFF-AXIS");
        SetLastShot(heading, acc,
            new[]
            {
                ("SHOT NO.", ShotNo.ToString("00"), P.Text),
                ("LATERAL MISS", $"{Math.Round(r.Score.LateralMiss)} m", acc),
                ("ANG. ERROR", $"{r.Score.AngError:0.00}°", P.Text),
                ("DELIVERED", $"{_en:0.0} GJ", r.Score.EnergyOk ? new Color("aebecb") : P.Red),
            },
            r.Score.Hit ? "thermal kill confirmed." : (r.Score.OnAxis ? "on-axis but flux too low." : "re-point the emitter."));
    }

    private void RevealSolution()
    {
        var t = _mission.BeamTarget!;
        SetLastShot("GIVE UP — SOLUTION", P.AccentDim,
            new[]
            {
                ("AZIMUTH", $"{t.Bearing:0.0}°", P.Text),
                ("ELEVATION", $"{t.LosElevation:0.0}°", P.Text),
                ("PULSE ENERGY", $"≥ {t.KillEnergyJoules / 1e9:0.0} GJ", P.Text),
            },
            "one valid firing solution shown.");
    }
}
