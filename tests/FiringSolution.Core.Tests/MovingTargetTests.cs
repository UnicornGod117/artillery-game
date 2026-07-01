// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using FiringSolution.Core;
using FiringSolution.Core.Engine;
using FiringSolution.Core.Models;
using Xunit;

namespace FiringSolution.Core.Tests;

/// <summary>
/// Moving targets (the Predictability slider, design §6). A tracker must be LED: the impact
/// is scored against where the target will be at the round's true time of flight, not where
/// it launched. These tests pin that the feature is opt-in (default missions are unchanged),
/// deterministic, that the intercept exists (the give-up reveal finds and lands it), and that
/// the lead genuinely matters (the same impact would miss the launch-instant position).
/// </summary>
public class MovingTargetTests
{
    private static DifficultySliders Moving(int seed, double predictability = 0.7, int tier = 2)
        => new(WeaponKind.Kinetic, MathFidelity: tier, Triangulation: 0.3,
               Circumstance: 0.3, Predictability: predictability, Seed: seed);

    [Fact]
    public void Predictability_Zero_LeavesTheTargetStationary()
    {
        // The feature is gated on the slider, not the tier, so a default Medium II mission
        // (Predictability 0) is still a static target — exactly as before movers existed.
        var m = GameEngine.GenerateMission(
            new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 2, Seed: 7));
        Assert.False(m.KineticTarget!.IsMoving);
        Assert.Equal(Vec3.Zero, m.KineticTarget!.Velocity);
        Assert.Equal(0, m.KineticObserved!.TargetSpeed);
    }

    [Fact]
    public void Predictability_BelowMediumII_StaysStationary()
    {
        // The lead puzzle needs a drag tier; below Medium II the slider does not move the target.
        var m = GameEngine.GenerateMission(
            new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 1, Predictability: 1.0, Seed: 7));
        Assert.False(m.KineticTarget!.IsMoving);
    }

    [Fact]
    public void Predictability_AtMediumII_ProducesATrack()
    {
        var m = GameEngine.GenerateMission(Moving(3));
        Assert.True(m.KineticTarget!.IsMoving);
        Assert.True(m.KineticTarget!.Velocity.MagnitudeXY > 0);
        Assert.True(m.KineticObserved!.TargetSpeed > 0);          // observed track handed to the player
        Assert.InRange(m.KineticObserved!.TargetHeading, 0, 360);
    }

    [Fact]
    public void TargetPosition_AdvancesLinearlyWithTime()
    {
        var m = GameEngine.GenerateMission(Moving(3));
        var t = m.KineticTarget!;
        Assert.Equal(t.Position, GameEngine.KineticTargetPositionAt(m, 0));      // p(0) = p0
        Vec3 at10 = GameEngine.KineticTargetPositionAt(m, 10);
        Assert.Equal(t.Position + t.Velocity * 10, at10);                        // p(t) = p0 + v·t
        Assert.Equal(t.Velocity.MagnitudeXY * 10, (at10 - t.Position).MagnitudeXY, 1e-6);
    }

    [Fact]
    public void SameSeed_ProducesTheIdenticalTrack()
    {
        var a = GameEngine.GenerateMission(Moving(11));
        var b = GameEngine.GenerateMission(Moving(11));
        Assert.Equal(a.KineticTarget!.Velocity, b.KineticTarget!.Velocity);
        Assert.Equal(a.KineticObserved!.TargetSpeed, b.KineticObserved!.TargetSpeed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MovingMission_IsSolvable_TheRevealLeadsAndHits(int seed)
    {
        // The give-up reveal must intercept a mover: converge the lead, fire, and land within
        // splash — scored against the target's position at the true time of flight.
        var m = GameEngine.GenerateMission(Moving(seed));
        Assert.True(m.KineticTarget!.IsMoving);

        var reveal = GameEngine.RevealKineticSolution(m);
        Assert.NotNull(reveal);
        var r = GameEngine.FireKinetic(m, reveal!.Value.Azimuth, reveal.Value.Elevation, reveal.Value.Charge);
        Assert.True(r.Score.Hit, $"revealed intercept must hit the mover (seed {seed}).");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheLeadIsReal_TheSameImpactWouldMissTheLaunchInstantPosition(int seed)
    {
        // Prove the score tracks the mover, not the launch-instant coordinate: the intercept
        // that hits p(t_flight) is, when re-scored against the original position p0, a miss —
        // whenever the target has travelled more than a splash radius during the flight.
        var m = GameEngine.GenerateMission(Moving(seed));
        var reveal = GameEngine.RevealKineticSolution(m);
        Assert.NotNull(reveal);
        var r = GameEngine.FireKinetic(m, reveal!.Value.Azimuth, reveal.Value.Elevation, reveal.Value.Charge);
        Assert.True(r.Score.Hit);

        // The impact sits within a splash radius of the lead point p(t_flight), so it is at
        // least (lead − splash) from the launch position p0. Only when that exceeds splash is
        // a static-miss guaranteed — i.e. when the target has moved more than 2·splash.
        double lead = m.KineticTarget!.Velocity.MagnitudeXY * r.Trajectory.Impact.Time;
        if (lead > 2 * m.Splash)
        {
            var againstStart = Scoring.ScoreKinetic(r.Trajectory.Impact, m.KineticTarget!.Position, m.Splash);
            Assert.False(againstStart.Hit,
                $"the intercept should NOT hit the launch-instant position (lead {lead:0} m ≫ splash {m.Splash:0} m).");
        }
    }
}
