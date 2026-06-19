using FiringSolution.Core.Engine;
using FiringSolution.Core.Models;

namespace FiringSolution.Core;

/// <summary>Outcome of committing &amp; firing a kinetic solution against a mission.</summary>
public sealed record KineticResult(Trajectory Trajectory, KineticScore Score);

/// <summary>Outcome of committing &amp; firing a beam solution against a mission.</summary>
public sealed record BeamResult(BeamShot Shot, BeamScore Score);

/// <summary>A revealed kinetic firing solution (the "give up" escape hatch, design §9).</summary>
public readonly record struct KineticReveal(double Azimuth, double Elevation, int Charge);

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

    /// <summary>
    /// Commit a beam firing solution: straight-ray SR model, scored on pointing + energy.
    /// The player supplies the beam SPEED β (v/c); the delivered pulse energy is derived
    /// and checked against the kill window.
    /// </summary>
    public static BeamResult FireBeam(Mission mission, double azimuth, double elevation, double beta)
    {
        if (mission.WeaponKind != WeaponKind.Beam ||
            mission.BeamWeapon is null || mission.BeamTarget is null)
            throw new InvalidOperationException("Mission is not a beam mission.");

        var t = mission.BeamTarget;
        var shot = Relativistic.SimulateBeam(mission.BeamWeapon, t.SlantRange, azimuth, elevation, beta);
        var score = Scoring.ScoreBeam(
            shot, azimuth, elevation, t.Bearing, t.LosElevation, t.SlantRange,
            t.RequiredPulseEnergyJoules, t.EnergyToleranceJoules, mission.AngularTolerance);
        return new BeamResult(shot, score);
    }

    /// <summary>
    /// The beam "give up" escape hatch: solve the relativistic KE equation for the β that
    /// lands the pulse at the centre of the kill window. γ = 1 + E_req/(N·m₀c²), β = √(1−1/γ²).
    /// </summary>
    public static double RevealBeamBeta(Mission mission)
    {
        if (mission.WeaponKind != WeaponKind.Beam ||
            mission.BeamWeapon is null || mission.BeamTarget is null)
            throw new InvalidOperationException("Mission is not a beam mission.");

        var w = mission.BeamWeapon;
        double gamma = 1.0 + mission.BeamTarget.RequiredPulseEnergyJoules / (w.ParticleCount * w.RestEnergyJoules);
        return Relativistic.BetaFromGamma(gamma);
    }

    /// <summary>
    /// The "give up" escape hatch (design §9): brute-forces the oracle to surface ONE
    /// firing solution that lands within splash, or null if the mission is unwinnable.
    /// This is deliberately NOT an in-play autosolver (design pillar 2) — it exists only
    /// to gracefully end a mission the player has abandoned.
    ///
    /// Search is coarse-to-fine per charge: a coarse az×elevation scan locates each
    /// charge's best basin, then a local refine resolves it. The refine is essential —
    /// when the target sits far from a charge's 45° max range, dR/dθ is steep and hitting
    /// splash needs ~0.1° elevation precision that a coarse grid alone would skip over.
    /// Crosswind (Hard tier) is corrected by the azimuth search.
    /// </summary>
    public static KineticReveal? RevealKineticSolution(Mission mission)
    {
        if (mission.WeaponKind != WeaponKind.Kinetic ||
            mission.KineticWeapon is null || mission.Environment is null || mission.KineticTarget is null)
            throw new InvalidOperationException("Mission is not a kinetic mission.");

        double bearing = mission.KineticTarget.Bearing;
        double targetRange = mission.KineticTarget.Range;
        int maxCharge = mission.KineticWeapon.MaxCharge;

        KineticReveal? RefineAround(int charge, double el0, double az0)
        {
            for (double el = Math.Max(1, el0 - 2.5); el <= Math.Min(89, el0 + 2.5); el += 0.05)
                for (double az = az0 - 1.5; az <= az0 + 1.5; az += 0.2)
                    if (FireKinetic(mission, az, el, charge).Score.Hit)
                        return new KineticReveal(az, el, charge);
            return null;
        }

        for (int charge = 1; charge <= maxCharge; charge++)
        {
            // Reachability prune: a shot near the max-range angle (~38°, allowing for the
            // drag-lowered optimum) is roughly the charge's longest reach. If even that
            // undershoots the target, no elevation on this charge can reach it — skip the
            // whole (expensive, RK4) coarse scan. The first charge that DOES reach has its
            // solution near its max-range angle, a flat-derivative spot that's easy to hit.
            double reach = FireKinetic(mission, bearing, 38, charge).Trajectory.Impact.Range;
            if (reach < targetRange * 0.98) continue;

            double bestMiss = double.MaxValue, bestEl = 45, bestAz = bearing;
            bool any = false;
            for (double el = 5; el <= 85; el += 2.0)
                for (double az = bearing - 6; az <= bearing + 6; az += 1.0)
                {
                    var r = FireKinetic(mission, az, el, charge);
                    if (r.Score.Hit) return new KineticReveal(az, el, charge);
                    if (r.Score.Miss < bestMiss) { bestMiss = r.Score.Miss; bestEl = el; bestAz = az; any = true; }
                }
            // Refine this charge's best basin — the true optimum may sit between coarse
            // samples in a steep-derivative region.
            if (any && RefineAround(charge, bestEl, bestAz) is { } hit) return hit;
        }
        return null;
    }
}
