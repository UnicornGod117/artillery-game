using FiringSolution.Core;
using FiringSolution.Core.Engine;
using FiringSolution.Core.Models;
using Xunit;

namespace FiringSolution.Core.Tests;

public class ScoringTests
{
    [Fact]
    public void PerfectImpact_IsAHit()
    {
        var target = new Vec3(0, 8000, 0);
        var impact = new Impact(target, Vec3.Zero, 0, 8000, 0, 0, 0);
        var score = Scoring.ScoreKinetic(impact, target, splash: 70);
        Assert.True(score.Hit);
        Assert.True(score.Miss < 1e-6);
    }

    [Fact]
    public void LongShot_ReportsPositiveRangeError()
    {
        var target = new Vec3(0, 8000, 0);
        var impact = new Impact(new Vec3(0, 8200, 0), Vec3.Zero, 0, 8200, 0, 0, 0);
        var score = Scoring.ScoreKinetic(impact, target, splash: 70);
        Assert.False(score.Hit);
        Assert.Equal(200, score.RangeError, 1e-6); // +200 m long
        Assert.Equal(0, score.LineError, 1e-6);
    }

    [Fact]
    public void LeftShot_ReportsPositiveLineError()
    {
        // Target due north; impact shifted West is "left" looking downrange.
        var target = new Vec3(0, 8000, 0);
        var impact = new Impact(new Vec3(-150, 8000, 0), Vec3.Zero, 0, 8001, 0, 0, 0);
        var score = Scoring.ScoreKinetic(impact, target, splash: 70);
        Assert.True(score.LineError > 0); // left
    }

    [Fact]
    public void Beam_RequiresPointingAndAnEnergyWindow()
    {
        var w = Content.Munitions.ProtonFocused();
        double requiredJ = Relativistic.PulseEnergy(w, 0.94); // the β the kill is tuned to
        const double tol = 0.1e9;

        // On axis but under-powered (β too low) → miss.
        var weak = Relativistic.SimulateBeam(w, 40000, 100, 12, 0.90);
        var weakScore = Scoring.ScoreBeam(weak, 100, 12, 100, 12, 40000, requiredJ, tol, 0.18);
        Assert.True(weakScore.OnAxis);
        Assert.False(weakScore.EnergyOk);
        Assert.False(weakScore.Hit);

        // On axis at the solved β → hit.
        var good = Relativistic.SimulateBeam(w, 40000, 100, 12, 0.94);
        var goodScore = Scoring.ScoreBeam(good, 100, 12, 100, 12, 40000, requiredJ, tol, 0.18);
        Assert.True(goodScore.Hit);

        // On axis but OVER-powered (β cranked to the limit) → miss: it's a window, not a
        // floor, so "max it out and win" overshoots.
        var hot = Relativistic.SimulateBeam(w, 40000, 100, 12, 0.98);
        var hotScore = Scoring.ScoreBeam(hot, 100, 12, 100, 12, 40000, requiredJ, tol, 0.18);
        Assert.True(hotScore.OnAxis);
        Assert.False(hotScore.EnergyOk);
        Assert.False(hotScore.Hit);
    }
}

public class MissionTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalMission()
    {
        var s = new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 2, Seed: 12345);
        var a = GameEngine.GenerateMission(s);
        var b = GameEngine.GenerateMission(s);
        Assert.Equal(a.KineticTarget!.Range, b.KineticTarget!.Range);
        Assert.Equal(a.KineticTarget!.Bearing, b.KineticTarget!.Bearing);
        Assert.Equal(a.Id, b.Id);
    }

    [Fact]
    public void EasyTier_HasNoDragNoWind()
    {
        var m = GameEngine.GenerateMission(new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 0, Seed: 7));
        Assert.False(m.Flags.Drag);
        Assert.False(m.Flags.Wind);
        Assert.Equal(0, m.KineticTarget!.Altitude);
    }

    [Fact]
    public void HardTier_EnablesDrag()
    {
        var m = GameEngine.GenerateMission(new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 3, Seed: 7));
        Assert.True(m.Flags.Drag);
        Assert.True(m.Flags.VariableG);
    }

    [Fact]
    public void GeneratedKineticMission_IsSolvable_AHitExists()
    {
        // Sanity: a competent operator can hit the generated target. We brute-force
        // a solution to prove the mission isn't impossible (the oracle is consistent).
        var m = GameEngine.GenerateMission(new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 1, Seed: 42));
        bool hitFound = false;
        for (int charge = 1; charge <= m.KineticWeapon!.MaxCharge && !hitFound; charge++)
            for (double el = 5; el <= 85 && !hitFound; el += 0.25)
            {
                var r = GameEngine.FireKinetic(m, m.KineticTarget!.Bearing, el, charge);
                if (r.Score.Hit) hitFound = true;
            }
        Assert.True(hitFound, "Generated mission must be solvable.");
    }
}
