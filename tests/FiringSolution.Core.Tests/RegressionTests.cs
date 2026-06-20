// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using FiringSolution.Core;
using FiringSolution.Core.Models;
using Xunit;

namespace FiringSolution.Core.Tests;

/// <summary>
/// Regression tests for fairness/solvability bugs found while finishing the game:
/// the beam kill-energy window must be honestly achievable, and the kinetic
/// give-up must reveal a working solution even when crosswind forces an azimuth lead.
/// </summary>
public class BeamSolvabilityTests
{
    [Fact]
    public void DisplayedGeometry_ExactlyMatchesTruth()
    {
        // The instrument reads out the true geometry (no spotter loop to correct against),
        // so the shown slant range, fuse and detonation window must equal the truth.
        for (int seed = 0; seed < 30; seed++)
        {
            var m = GameEngine.GenerateMission(new DifficultySliders(
                WeaponKind.Beam, MathFidelity: 2, Circumstance: 0.7, Seed: seed));
            Assert.Equal(m.BeamTarget!.SlantRange, m.BeamObserved!.SlantRange, 1.0);
            Assert.Equal(m.BeamTarget!.FuseProperTime, m.BeamObserved!.FuseSeconds, 1e-9);
            Assert.Equal(m.BeamTarget!.DetonationToleranceMeters, m.BeamObserved!.DetonationToleranceMeters, 1.0);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SolvingTheRequiredSpeed_OnAxis_IsAlwaysAKill(int tier)
    {
        // A player who points at the true LOS and dials the β whose dilated fuse detonates
        // at the target must score a kill. The reveal does exactly that inversion:
        // k = R/(c·τ), β = k/√(1 + k²).
        for (int seed = 0; seed < 40; seed++)
        {
            var m = GameEngine.GenerateMission(new DifficultySliders(
                WeaponKind.Beam, MathFidelity: tier, Circumstance: 0.7, Seed: seed));
            var t = m.BeamTarget!;
            double beta = GameEngine.RevealBeamBeta(m);
            var r = GameEngine.FireBeam(m, t.Bearing, t.LosElevation, beta);
            Assert.True(r.Score.Hit, $"beam tier {tier} seed {seed} should be killable at the solved speed.");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MaxingTheSpeed_Overshoots_AndMisses(int tier)
    {
        // Detonation range is a window, not a floor: cranking β to the ceiling dilates the
        // fuse far too long, so the warhead overshoots the target — a miss even on-axis.
        for (int seed = 0; seed < 40; seed++)
        {
            var m = GameEngine.GenerateMission(new DifficultySliders(
                WeaponKind.Beam, MathFidelity: tier, Circumstance: 0.7, Seed: seed));
            var t = m.BeamTarget!;
            var r = GameEngine.FireBeam(m, t.Bearing, t.LosElevation, 0.999);
            Assert.False(r.Score.Hit, $"beam tier {tier} seed {seed} must not be winnable by maxing β.");
            Assert.True(r.Score.RangeError > 0, "maxed β overshoots the detonation range.");
        }
    }

    [Fact]
    public void LongRangeBeam_HasMinutesScaleFlightTime()
    {
        // The whole point of the long-range battlespace: at the solved speed the flight is
        // seconds-to-minutes (γ·τ), not sub-millisecond — long enough to simulate & animate.
        for (int seed = 0; seed < 20; seed++)
        {
            var m = GameEngine.GenerateMission(new DifficultySliders(
                WeaponKind.Beam, MathFidelity: 2, Circumstance: 0.5, Seed: seed));
            var t = m.BeamTarget!;
            var shot = Engine.Relativistic.SimulateBeam(
                m.BeamWeapon!, t.SlantRange, t.Bearing, t.LosElevation, GameEngine.RevealBeamBeta(m));
            Assert.InRange(shot.FlightTime, 5.0, 400.0);
        }
    }
}

public class KineticGiveUpTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void GiveUp_FindsASolution_AtDragFreeTiers(int tier)
    {
        // Give up is a graceful exit: the Core's reveal must always surface a working
        // solution. Tiers 0–1 are drag-free, so a wider seed sweep is cheap.
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
    [InlineData(2, 0)]  // Medium II — drag + crosswind
    [InlineData(2, 1)]
    [InlineData(3, 0)]  // Hard — drag + ρ(h)
    [InlineData(3, 1)]
    public void GiveUp_FindsASolution_AtDragTiers(int tier, int seed)
    {
        // Drag tiers integrate the RK4 trajectory (expensive), so keep to a few seeds.
        var m = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Kinetic, MathFidelity: tier, Circumstance: 0.8, Seed: seed));
        var reveal = GameEngine.RevealKineticSolution(m);
        Assert.NotNull(reveal);
        var r = GameEngine.FireKinetic(m, reveal!.Value.Azimuth, reveal.Value.Elevation, reveal.Value.Charge);
        Assert.True(r.Score.Hit, $"revealed solution must hit (tier {tier} seed {seed}).");
    }

    [Fact]
    public void MediumII_Wind_MateriallyDeflectsTheImpact()
    {
        // Item 5: at Medium II, drag is on so wind genuinely couples — the same shot
        // must land in a different place with the wind present vs zeroed. (Below this
        // tier a drag-free round is physically unaffected by wind.)
        var m = GameEngine.GenerateMission(new DifficultySliders(
            WeaponKind.Kinetic, MathFidelity: 2, Circumstance: 0.8, Seed: 3));
        Assert.True(m.Flags.Drag);
        Assert.False(m.Flags.VariableDensity);            // steady density at Medium II
        Assert.True(m.Environment!.Wind.Magnitude > 0);

        var sol = new KineticSolution(m.KineticTarget!.Bearing, 40, m.KineticWeapon!.MuzzleVelocity(6));
        var windy = Engine.Ballistics.SimulateKinetic(m.KineticWeapon!, m.Environment!, sol, m.Flags);
        var still = Engine.Ballistics.SimulateKinetic(
            m.KineticWeapon!, m.Environment! with { Wind = Vec3.Zero }, sol, m.Flags);
        double shift = (windy.Impact.Position - still.Impact.Position).MagnitudeXY;
        Assert.True(shift > 25, $"wind should move the impact meaningfully (was {shift:0} m).");
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
