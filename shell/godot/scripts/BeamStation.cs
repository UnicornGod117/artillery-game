using Godot;
using System;
using System.Collections.Generic;
using FiringSolution.Core;
using FiringSolution.Core.Models;
using FiringSolution.Core.Engine;

namespace FiringSolution.Shell;

/// <summary>
/// Direction B — cool ice-glass surveillance, relativistic particle beam. Lead
/// is ~zero (near-c); the work is energy &amp; γ. Scored on two gates: pointing
/// accuracy AND delivered pulse energy ≥ kill threshold.
/// </summary>
public partial class BeamStation : StationView
{
    private Mission _mission = null!;
    private double _az, _el, _zc = 0, _en = 4.2; // pulse energy, GJ

    private Label _gammaLabel = null!, _betaLabel = null!, _enLabel = null!;

    protected override Palette BuildPalette() => Palette.Ice;

    protected override Control BuildTopBar()
    {
        _mission = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Beam, MathFidelity: 3, Triangulation: 0.25, Circumstance: 0.6, Seed: 9120));
        _az = Math.Round(_mission.BeamObserved!.Bearing);
        _el = Math.Round(_mission.BeamObserved!.LosElevation);

        return MakeTopBar(
            "DEW-02 · STATION BETA · Relativistic beam",
            new[]
            {
                ("WPN · RELATIVISTIC BEAM", true),
                ("WORLD · " + _mission.World.Name, false),
                ("TIER · " + _mission.TierLabel, false),
                (_mission.Id, false),
            },
            "CAPACITOR CHARGE");
    }

    protected override Control BuildLeftPanel()
    {
        var panel = Ui.Panel(P.Panel, P.Border, pad: 16, borderW: 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 16);
        panel.AddChild(v);
        var o = _mission.BeamObserved!;

        v.AddChild(Ui.SectionHeader(P, "Environment", P.Accent, "MEASURED"));
        var windRow = new HBoxContainer();
        windRow.AddThemeConstantOverride("separation", 14);
        windRow.AddChild(new Compass { P = P, FromDeg = 118 });
        var wc = new VBoxContainer();
        wc.AddThemeConstantOverride("separation", 2);
        wc.AddChild(Ui.Text("WIND VECTOR", P.Faint, 9));
        wc.AddChild(Ui.Text("6.1 m/s", P.Text, 21));
        wc.AddChild(Ui.Text("FROM 118°", P.AccentDim, 11));
        windRow.AddChild(wc);
        v.AddChild(windRow);
        v.AddChild(MetricGrid(new[]
        {
            ("ALTITUDE", "+12 km"),
            ("AIR TEMP", $"{o.AirTemp:0} °C"),
            ("AIR DENSITY ρ", $"{o.AirDensity:0.000} kg/m³"),
            ("LOCAL g", $"{o.LocalG:0.00} m/s²"),
        }, P.Text));

        v.AddChild(Ui.SectionHeader(P, "Target — Observed", P.Red, "SENSOR"));
        v.AddChild(MetricGrid(new[]
        {
            ("SLANT RANGE", $"{o.SlantRange / 1000:0.0} km"),
            ("BEARING", $"{o.Bearing:0.0} °"),
            ("LOS ELEVATION", $"{o.LosElevation:0.0} °"),
            ("CLOSING", $"{o.Closing:0} m/s"),
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

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 11);
        grid.AddThemeConstantOverride("v_separation", 11);
        v.AddChild(grid);

        AddNumberField(grid, "AZIMUTH (x) · °", _az.ToString("0.0"), 1.0,
            d => { _az = d; Board.AimAzimuth = _az; Board.QueueRedraw(); });
        AddNumberField(grid, "ELEVATION (y) · °", _el.ToString("0.0"), 0.5,
            d => { _el = d; VPlane.AimElevation = _el; VPlane.QueueRedraw(); });
        AddNumberField(grid, "Z-CORR (cross) · °", _zc.ToString("+0.0;-0.0;0.0"), 0.1,
            d => { _zc = d; });
        AddNumberField(grid, "PULSE ENERGY · GJ", _en.ToString("0.0"), 0.5,
            d => { _en = d; RefreshRegime(); },
            clamp: d => Math.Max(0, d));

        var fire = Ui.PrimaryButton(P, "◆  COMMIT & FIRE");
        fire.Pressed += Fire;
        v.AddChild(fire);

        // Relativistic regime, derived from the player's input.
        var reg = Ui.Panel(P.PanelDeep, P.Border, pad: 0, borderW: 1);
        var rv = new VBoxContainer();
        var rh = Ui.Panel(P.Bg, P.BorderSoft, pad: 9, borderW: 0);
        var rhr = new HBoxContainer();
        rhr.AddChild(Ui.Text("RELATIVISTIC REGIME", P.TextDim, 10));
        rhr.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        rhr.AddChild(Ui.Text("FROM YOUR INPUT", P.AccentDim, 8));
        rh.AddChild(rhr);
        rv.AddChild(rh);
        var rg = new GridContainer { Columns = 2 };
        rg.AddThemeConstantOverride("h_separation", 1);
        rg.AddThemeConstantOverride("v_separation", 1);
        double beta = _mission.BeamWeapon!.Beta;
        double gamma = Relativistic.Lorentz(beta);
        _betaLabel = Ui.Text($"{beta:0.000}", P.Text, 14);
        _gammaLabel = Ui.Text($"{gamma:0.000}", P.Text, 14);
        _enLabel = Ui.Text($"{_en:0.0} GJ", P.Accent, 14);
        rg.AddChild(WrapMetric("β", _betaLabel));
        rg.AddChild(WrapMetric("LORENTZ γ", _gammaLabel));
        rg.AddChild(WrapMetric("PULSE ENERGY", _enLabel));
        rg.AddChild(WrapMetric("KILL THRESHOLD", Ui.Text($"≥ {_mission.BeamObserved!.KillEnergyGJ:0.0} GJ", new Color("aebecb"), 14)));
        rv.AddChild(rg);
        reg.AddChild(rv);
        v.AddChild(reg);

        // Handbook + Help / Give up.
        var hbk = Ui.Panel(P.PanelDeep, P.Border, pad: 9, borderW: 1);
        var hbr = new HBoxContainer();
        hbr.AddChild(Ui.Text("▤ HANDBOOK", P.Accent, 10));
        hbr.AddChild(Ui.Text(" · Relativity / Thermal / Vectors", P.Faint, 10));
        hbk.AddChild(hbr);
        v.AddChild(hbk);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 9);
        var help = Ui.FlatButton(P, "HELP", P.AccentDim, P.Border, 10);
        help.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var give = Ui.FlatButton(P, "GIVE UP", P.Faint, P.Border, 10);
        give.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        help.Pressed += () => SetLastShot("HELP", P.AccentDim, Array.Empty<(string, string, Color)>(),
            "Lead ≈ 0. Point on LOS bearing/elevation; deliver E ≥ kill threshold via (γ−1)m₀c².");
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
        Board.QueueRedraw();

        double el = Constants.DegToRad(_el);
        double s = _mission.BeamObserved!.SlantRange;
        VPlane.Arc = new List<Vector2>
        {
            new(0, 0),
            new((float)(s * Math.Cos(el)), (float)(s * Math.Sin(el))),
        };
        VPlane.FiredHit = r.Score.Hit;
        VPlane.QueueRedraw();

        Color acc = r.Score.Hit ? P.Accent : P.Red;
        if (r.Score.Hit) Career += 1100;
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

    private Control WrapMetric(string caption, Label value)
    {
        var box = Ui.Panel(P.PanelDeep, P.BorderSoft, pad: 9, borderW: 0);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        col.AddChild(Ui.Text(caption, P.Faint, 8));
        col.AddChild(value);
        box.AddChild(col);
        return box;
    }
}
