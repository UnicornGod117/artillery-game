// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
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
        // Score against where the target IS when the round arrives, not where it launched.
        // For a stationary target this is its fixed position (unchanged behaviour); for a
        // mover it's the lead point p(t) at the true time of flight, so a hit demands lead.
        Vec3 aimPoint = KineticTargetPositionAt(mission, traj.Impact.Time);
        var score = Scoring.ScoreKinetic(traj.Impact, aimPoint, mission.Splash);
        return new KineticResult(traj, score);
    }

    /// <summary>
    /// The kinetic target's ENU position (gun-relative) <paramref name="t"/> seconds after
    /// the shot is fired: <c>p(t) = Position + Velocity·t</c>. Stationary targets (zero
    /// velocity) return their fixed position, so nothing changes for them. The shell renders
    /// the moving glyph from this, and scoring evaluates it at the round's true time of flight.
    /// </summary>
    public static Vec3 KineticTargetPositionAt(Mission mission, double t)
    {
        if (mission.KineticTarget is null)
            throw new InvalidOperationException("Mission is not a kinetic mission.");
        var tgt = mission.KineticTarget;
        return tgt.Position + tgt.Velocity * t;
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
            t.FuseProperTime, t.DetonationToleranceMeters, mission.AngularTolerance);
        return new BeamResult(shot, score);
    }

    /// <summary>
    /// The beam "give up" escape hatch: solve the launch speed whose time-dilated fuse
    /// detonates exactly on the target. k = R/(c·τ), β = k/√(1 + k²).
    /// </summary>
    public static double RevealBeamBeta(Mission mission)
    {
        if (mission.WeaponKind != WeaponKind.Beam ||
            mission.BeamWeapon is null || mission.BeamTarget is null)
            throw new InvalidOperationException("Mission is not a beam mission.");

        return Relativistic.SpeedForFuseRange(
            mission.BeamTarget.SlantRange, mission.BeamTarget.FuseProperTime);
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

        // A moving target needs an intercept lead, not a fixed-point solve — hand it off.
        if (mission.KineticTarget.IsMoving)
            return RevealMovingSolution(mission);

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

    /// <summary>
    /// Intercept solver for a MOVING target (the "give up" reveal for a tracker). A fixed
    /// point converges the lead: aim where the target will be after the round's flight time,
    /// fire, read the true flight time, move the aim to where the target now is at that time,
    /// and repeat. It settles in a few passes because flight time is a smooth function of
    /// range. A final local refine — scored against the ACTUAL mover — nails the hit within
    /// splash. Returns null only if the intercept is genuinely unreachable.
    /// </summary>
    private static KineticReveal? RevealMovingSolution(Mission mission)
    {
        var tgt = mission.KineticTarget!;
        Vec3 p0 = tgt.Position, v = tgt.Velocity;

        // Phase 1 — converge the lead CHEAPLY. Each pass estimates only the flight time to the
        // current aim (fixed bearing, coarse elevation — no azimuth sweep, no refine), then
        // advances the aim to where the target will be after that flight. A few passes settle
        // it, and each is a fraction of a full solve, so the reveal stays inexpensive.
        Vec3 aim = p0;
        for (int iter = 0; iter < 8; iter++)
        {
            double? tof = CoarseTofToPoint(mission, aim);
            if (tof is null) return null;                 // even at max charge, unreachable
            Vec3 next = p0 + v * tof.Value;
            bool converged = (next - aim).MagnitudeXY < 2.0;
            aim = next;
            if (converged) break;
        }

        // Phase 2 — one full, refined solve at the converged lead point.
        var probe = SolveToStaticPoint(mission, aim);
        if (probe is null) return null;
        var c = probe.Value;

        // Verify against the real mover; refine locally if the lead is a hair off.
        if (FireKinetic(mission, c.az, c.el, c.charge).Score.Hit)
            return new KineticReveal(c.az, c.el, c.charge);
        for (double el = Math.Max(1, c.el - 3); el <= Math.Min(89, c.el + 3); el += 0.05)
            for (double az = c.az - 3; az <= c.az + 3; az += 0.15)
                if (FireKinetic(mission, az, el, c.charge).Score.Hit)
                    return new KineticReveal(az, el, c.charge);
        return null;
    }

    /// <summary>
    /// Cheap lead-iteration probe: the flight time of the shot (fired along the aim's bearing,
    /// coarse elevation grid) whose ground RANGE best matches the aim's range, or null if no
    /// charge reaches. It's judged on range error — not total miss — because at the drag tiers
    /// a crosswind pushes an on-bearing shot sideways past the splash radius, so a miss-based
    /// test would spuriously find nothing; the flight time is governed by range regardless, and
    /// that's all this needs to converge the lead. The azimuth (wind) and precision are the full
    /// solve's job. Preferring the flattest reaching arc keeps the lead small and reachable.
    /// </summary>
    private static double? CoarseTofToPoint(Mission mission, Vec3 aim)
    {
        double range = aim.MagnitudeXY, bearing = aim.Bearing;
        int maxCharge = mission.KineticWeapon!.MaxCharge;

        double bestErr = double.MaxValue, bestTof = 0;
        bool found = false;
        for (int charge = 1; charge <= maxCharge; charge++)
        {
            if (FireKinetic(mission, bearing, 38, charge).Trajectory.Impact.Range < range * 0.98) continue;
            for (double el = 5; el <= 85; el += 2.0)
            {
                var impact = FireKinetic(mission, bearing, el, charge).Trajectory.Impact;
                double err = Math.Abs(impact.Range - range);
                if (err < bestErr) { bestErr = err; bestTof = impact.Time; found = true; }
            }
        }
        return found ? bestTof : null;
    }

    /// <summary>
    /// Best (az, el, charge, time-of-flight) whose impact lands nearest the STATIC point
    /// <paramref name="aim"/> (gun-relative ENU) — the per-iteration workhorse of the moving
    /// solver. Coarse-to-fine per charge, mirroring the stationary reveal, but scored on the
    /// geometric miss to <paramref name="aim"/> rather than the mover. Null if no charge reaches.
    /// </summary>
    private static (double az, double el, int charge, double tof)? SolveToStaticPoint(Mission mission, Vec3 aim)
    {
        double range = aim.MagnitudeXY;
        double bearing = aim.Bearing;
        int maxCharge = mission.KineticWeapon!.MaxCharge;
        double splash = mission.Splash;

        (double az, double el, int charge, double tof)? best = null;
        double bestMiss = double.MaxValue, bestTof = double.MaxValue;
        bool anyReaches = false;

        // Consider a candidate. Among shots that already land within splash of the aim we
        // keep the one with the SHORTEST flight time; otherwise we keep the smallest miss.
        // Preferring the flattest reaching arc keeps the lead small, so the outer fixed point
        // stays contractive and the intercept point stays inside the reachable envelope.
        void Consider(double az, double el, int charge)
        {
            var traj = FireKinetic(mission, az, el, charge).Trajectory;
            double tof = traj.Impact.Time;
            double miss = Scoring.ScoreKinetic(traj.Impact, aim, splash).Miss;
            bool candOk = miss <= splash, bestOk = best is not null && bestMiss <= splash;
            bool take = best is null
                || (candOk && !bestOk)
                || (candOk && bestOk && tof < bestTof)
                || (!candOk && !bestOk && miss < bestMiss);
            if (take) { best = (az, el, charge, tof); bestMiss = miss; bestTof = tof; }
        }

        for (int charge = 1; charge <= maxCharge; charge++)
        {
            // Same reachability prune as the stationary reveal: if the ~max-range angle
            // undershoots this aim, no elevation on this charge can reach it.
            if (FireKinetic(mission, bearing, 38, charge).Trajectory.Impact.Range < range * 0.98) continue;
            anyReaches = true;

            for (double el = 5; el <= 85; el += 2.0)
                for (double az = bearing - 6; az <= bearing + 6; az += 1.0)
                    Consider(az, el, charge);
        }
        if (!anyReaches || best is null) return null;

        // Refine the best basin to ~0.05° — steep dR/dθ near a charge's max range.
        var b = best.Value;
        for (double el = Math.Max(1, b.el - 2.5); el <= Math.Min(89, b.el + 2.5); el += 0.05)
            for (double az = b.az - 1.5; az <= b.az + 1.5; az += 0.2)
                Consider(az, el, b.charge);
        return best;
    }
}
