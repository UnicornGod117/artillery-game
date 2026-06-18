using FiringSolution.Core;
using FiringSolution.Core.Models;
using Xunit;

namespace FiringSolution.Core.Tests;

/// <summary>
/// Regression tests for fairness/solvability bugs found while finishing the game:
/// the beam kill-energy readout must be honestly achievable, and the kinetic
/// give-up must reveal a working solution even when crosswind forces an azimuth lead.
/// </summary>
public class BeamSolvabilityTests
{
    [Fact]
    public void DisplayedKillEnergy_ExactlyMatchesTruth()
    {
        // The instrument reads out a published spec, not a noisy measurement — so the
        // shown threshold must equal the true gate, never round below it.
        for (int seed = 0; seed < 30; seed++)
        {
            var m = GameEngine.GenerateMission(new DifficultySliders(
                WeaponKind.Beam, MathFidelity: 2, Circumstance: 0.7, Seed: seed));
            Assert.Equal(m.BeamTarget!.KillEnergyJoules, m.BeamObserved!.KillEnergyGJ * 1e9, 1.0);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DeliveringDisplayedEnergy_OnAxis_IsAlwaysAKill(int tier)
    {
        // A player who points at the true LOS and delivers exactly the displayed
        // kill energy must score a kill — the readout cannot be a trap. (Beam sims
        // are O(1), so a wide seed sweep here is cheap.)
        for (int seed = 0; seed < 40; seed++)
        {
            var m = GameEngine.GenerateMission(new DifficultySliders(
                WeaponKind.Beam, MathFidelity: tier, Circumstance: 0.7, Seed: seed));
            var t = m.BeamTarget!;
            var r = GameEngine.FireBeam(m, t.Bearing, t.LosElevation, m.BeamObserved!.KillEnergyGJ * 1e9);
            Assert.True(r.Score.Hit, $"beam tier {tier} seed {seed} should be killable at the shown energy.");
        }
    }
}

public class KineticGiveUpTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void GiveUp_FindsASolution_AtLowerTiers(int tier)
    {
        // Give up is a graceful exit: the Core's reveal must always surface a working
        // solution. Tiers 0–2 are drag-free, so a wider seed sweep is cheap.
        for (int seed = 0; seed < 6; seed++)
        {
            var m = GameEngine.GenerateMission(new DifficultySliders(
                WeaponKind.Kinetic, MathFidelity: tier, Circumstance: 0.8, Seed: seed));
            var reveal = GameEngine.RevealKineticSolution(m);
            Assert.NotNull(reveal);
            // The revealed solution must genuinely land within splash.
            var r = GameEngine.FireKinetic(m, reveal!.Value.Azimuth, reveal.Value.Elevation, reveal.Value.Charge);
            Assert.True(r.Score.Hit, $"revealed solution must hit (tier {tier} seed {seed}).");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void GiveUp_FindsASolution_AtHardTier(int seed)
    {
        // Hard tier integrates drag (expensive), so keep this to a couple of seeds.
        var m = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Kinetic, MathFidelity: 3, Circumstance: 0.8, Seed: seed));
        var reveal = GameEngine.RevealKineticSolution(m);
        Assert.NotNull(reveal);
        var r = GameEngine.FireKinetic(m, reveal!.Value.Azimuth, reveal.Value.Elevation, reveal.Value.Charge);
        Assert.True(r.Score.Hit, $"revealed Hard-tier solution must hit (seed {seed}).");
    }

    [Fact]
    public void GiveUp_SolvesSteepDerivativeMission_Tier0Seed4()
    {
        // Regression: this vacuum mission's target sits far from any charge's 45° max
        // range, so dR/dθ is steep (~400 m/°). A coarse 0.5° elevation scan misses
        // every grid point by >splash; only the coarse-to-fine reveal cracks it.
        var m = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Kinetic, MathFidelity: 0, Circumstance: 0.8, Seed: 4));
        var reveal = GameEngine.RevealKineticSolution(m);
        Assert.NotNull(reveal);
    }

    [Fact]
    public void Crosswind_CanForceAnAzimuthLead_BeyondElevationAlone()
    {
        // Documents why the give-up needs an azimuth search: at Hard tier, drag couples
        // wind into the trajectory and this mission is unsolvable on the firing bearing
        // alone yet solvable once azimuth is led. (Seed found empirically.)
        var m = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Kinetic, MathFidelity: 3, Circumstance: 0.7, Seed: 7));

        bool onBearingHit = false;
        for (double el = 5; el <= 85 && !onBearingHit; el += 0.5)
            if (GameEngine.FireKinetic(m, m.KineticTarget!.Bearing, el, 7).Score.Hit) onBearingHit = true;
        Assert.False(onBearingHit, "expected the on-bearing search to miss.");

        Assert.NotNull(GameEngine.RevealKineticSolution(m)); // azimuth lead recovers it
    }
}

public class MissionGenerationCoverageTests
{
    [Theory]
    [InlineData(WeaponKind.Kinetic, 0)]
    [InlineData(WeaponKind.Kinetic, 3)]
    [InlineData(WeaponKind.Beam, 1)]
    [InlineData(WeaponKind.Beam, 3)]
    public void SameSeed_IsDeterministic_AcrossWeaponsAndTiers(WeaponKind kind, int tier)
    {
        var s = new DifficultySliders(kind, MathFidelity: tier, Circumstance: 0.7, Seed: 2024);
        var a = GameEngine.GenerateMission(s);
        var b = GameEngine.GenerateMission(s);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.World.Id, b.World.Id);
        Assert.Equal(a.TierIndex, b.TierIndex);
    }

    [Fact]
    public void HighCircumstance_CanSelectTheExoticWorld()
    {
        // Across seeds, a high circumstance load must sometimes pick the exotic planet
        // so the off-Earth gravity/atmosphere actually gets exercised.
        bool exoticSeen = false;
        for (int seed = 0; seed < 40 && !exoticSeen; seed++)
        {
            var m = GameEngine.GenerateMission(new DifficultySliders(
                WeaponKind.Kinetic, MathFidelity: 2, Circumstance: 1.0, Seed: seed));
            if (m.World.Id != "earth") exoticSeen = true;
        }
        Assert.True(exoticSeen, "high circumstance should occasionally generate the exotic world.");
    }

    [Fact]
    public void BeamMissionsAreNeverTrivialTier()
    {
        // The beam is energy/γ-led; the design forbids a trivial (Easy) beam mission.
        var m = GameEngine.GenerateMission(new DifficultySliders(WeaponKind.Beam, MathFidelity: 0, Seed: 5));
        Assert.True(m.TierIndex >= 1);
    }
}
