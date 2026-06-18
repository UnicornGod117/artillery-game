using FiringSolution.Core.Engine;
using FiringSolution.Core.Models;

namespace FiringSolution.Core;

/// <summary>Outcome of committing &amp; firing a kinetic solution against a mission.</summary>
public sealed record KineticResult(Trajectory Trajectory, KineticScore Score);

/// <summary>Outcome of committing &amp; firing a beam solution against a mission.</summary>
public sealed record BeamResult(BeamShot Shot, BeamScore Score);

/// <summary>
/// The Core's public surface (design §13). The shell consumes ONLY this:
///   GenerateMission(sliders) → Mission
///   FireKinetic / FireBeam(mission, solution) → simulated trajectory + score
/// No rendering, no engine/UI types leak out. Fully testable in isolation.
/// </summary>
public static class GameEngine
{
    public static Mission GenerateMission(DifficultySliders sliders)
        => MissionGenerator.Generate(sliders);

    /// <summary>
    /// Commit a kinetic firing solution: the oracle simulates the true trajectory
    /// and scores the impact against the (hidden) true target.
    /// </summary>
    public static KineticResult FireKinetic(Mission mission, KineticSolution solution)
    {
        if (mission.WeaponKind != WeaponKind.Kinetic ||
            mission.KineticWeapon is null || mission.Environment is null || mission.KineticTarget is null)
            throw new InvalidOperationException("Mission is not a kinetic mission.");

        var traj = Ballistics.SimulateKinetic(mission.KineticWeapon, mission.Environment, solution, mission.Flags);
        var score = Scoring.ScoreKinetic(traj.Impact, mission.KineticTarget.Position, mission.Splash);
        return new KineticResult(traj, score);
    }

    /// <summary>Convenience: resolve a discrete charge to muzzle velocity, then fire.</summary>
    public static KineticResult FireKinetic(Mission mission, double azimuth, double elevation, int charge)
    {
        double v0 = mission.KineticWeapon!.MuzzleVelocity(charge);
        return FireKinetic(mission, new KineticSolution(azimuth, elevation, v0));
    }

    /// <summary>Commit a beam firing solution: straight-ray SR model, scored on pointing + energy.</summary>
    public static BeamResult FireBeam(Mission mission, double azimuth, double elevation, double pulseEnergyJoules)
    {
        if (mission.WeaponKind != WeaponKind.Beam ||
            mission.BeamWeapon is null || mission.BeamTarget is null)
            throw new InvalidOperationException("Mission is not a beam mission.");

        var t = mission.BeamTarget;
        var shot = Relativistic.SimulateBeam(mission.BeamWeapon, t.SlantRange, azimuth, elevation, pulseEnergyJoules);
        var score = Scoring.ScoreBeam(
            shot, azimuth, elevation, t.Bearing, t.LosElevation, t.SlantRange,
            t.KillEnergyJoules, mission.AngularTolerance);
        return new BeamResult(shot, score);
    }
}
