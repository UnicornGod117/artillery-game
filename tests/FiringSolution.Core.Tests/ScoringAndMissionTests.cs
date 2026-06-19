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
    public void Beam_RequiresPointingAndDetonationRange()
    {
        var w = Content.Munitions.ProtonFocused();
        double R = 1.2e10, fuse = 18, tol = 6e7;
        double beta = Relativistic.SpeedForFuseRange(R, fuse); // the speed the kill is tuned to

        // On axis at the solved speed → dilated fuse detonates on target → hit.
        var good = Relativistic.SimulateBeam(w, R, 100, 12, beta);
        var g = Scoring.ScoreBeam(good, 100, 12, 100, 12, R, fuse, tol, 0.18);
        Assert.True(g.Hit);

        // Too slow → less dilation, fuse fires short → miss.
        var slow = Relativistic.SimulateBeam(w, R, 100, 12, beta * 0.9);
        var s = Scoring.ScoreBeam(slow, 100, 12, 100, 12, R, fuse, tol, 0.18);
        Assert.True(s.OnAxis);
        Assert.False(s.DetonationOk);
        Assert.True(s.RangeError < 0);

        // Too fast (β maxed) → overshoots → miss: it's a window, not a floor.
        var fast = Relativistic.SimulateBeam(w, R, 100, 12, 0.999);
        var f = Scoring.ScoreBeam(fast, 100, 12, 100, 12, R, fuse, tol, 0.18);
        Assert.True(f.OnAxis);
        Assert.False(f.DetonationOk);
        Assert.True(f.RangeError > 0);

        // Off-axis but perfectly timed → still a miss (pointing is an independent gate).
        var off = Scoring.ScoreBeam(good, 101, 12, 100, 12, R, fuse, tol, 0.18);
        Assert.False(off.OnAxis);
        Assert.False(off.Hit);
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
