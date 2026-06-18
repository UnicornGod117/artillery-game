using FiringSolution.Core;
using FiringSolution.Core.Content;
using FiringSolution.Core.Engine;
using FiringSolution.Core.Models;
using Xunit;

namespace FiringSolution.Core.Tests;

public class BallisticsTests
{
    private static EnvironmentSpec FlatEnv() =>
        new(Worlds.Earth, SiteAltitude: 0, TargetAltitude: 0, Wind: Vec3.Zero);

    [Theory]
    [InlineData(30, 300)]
    [InlineData(45, 400)]
    [InlineData(60, 500)]
    public void VacuumRange_MatchesClosedForm(double elevationDeg, double speed)
    {
        // No drag, constant g: the RK4 oracle must agree with R = v₀²·sin(2θ)/g.
        var weapon = Munitions.KineticArtillery();
        var env = FlatEnv();
        var tier = new TierFlags(Drag: false, VariableG: false, Wind: false);
        double g = Atmosphere.SurfaceGravity(Worlds.Earth, 0);

        var traj = Ballistics.SimulateKinetic(
            weapon, env, new KineticSolution(0, elevationDeg, speed), tier);

        double el = Constants.DegToRad(elevationDeg);
        double expectedRange = speed * speed * Math.Sin(2 * el) / g;
        double expectedTof = 2 * speed * Math.Sin(el) / g;
        double expectedApex = speed * speed * Math.Sin(el) * Math.Sin(el) / (2 * g);

        Assert.Equal(expectedRange, traj.Impact.Range, expectedRange * 0.005);
        Assert.Equal(expectedTof, traj.Impact.Time, expectedTof * 0.005);
        Assert.Equal(expectedApex, traj.Apex, expectedApex * 0.01);
    }

    [Fact]
    public void FiredDueNorth_ImpactsOnNorthAxis()
    {
        var weapon = Munitions.KineticArtillery();
        var traj = Ballistics.SimulateKinetic(
            weapon, FlatEnv(), new KineticSolution(0, 45, 400),
            new TierFlags(false, false, false));

        Assert.Equal(0, traj.Impact.Bearing, 0.5); // ~due north
        Assert.True(traj.Impact.Position.Y > 0);
        Assert.True(Math.Abs(traj.Impact.Position.X) < 1.0);
    }

    [Fact]
    public void Drag_ShortensRange()
    {
        var weapon = Munitions.KineticArtillery();
        var env = FlatEnv();
        var sol = new KineticSolution(0, 45, 600);

        var vacuum = Ballistics.SimulateKinetic(weapon, env, sol, new TierFlags(false, false, false));
        var withDrag = Ballistics.SimulateKinetic(weapon, env, sol, new TierFlags(true, false, false));

        Assert.True(withDrag.Impact.Range < vacuum.Impact.Range,
            "Quadratic drag must reduce range.");
    }

    [Fact]
    public void ImpactEnergy_IsPositiveAndReasonable()
    {
        var weapon = Munitions.KineticArtillery();
        var traj = Ballistics.SimulateKinetic(
            weapon, FlatEnv(), new KineticSolution(0, 45, 400),
            new TierFlags(false, false, false));

        // Vacuum: impact speed equals muzzle speed (energy conserved).
        double expected = 0.5 * weapon.Munition.Mass * 400 * 400;
        Assert.Equal(expected, traj.Impact.Energy, expected * 0.01);
    }
}
