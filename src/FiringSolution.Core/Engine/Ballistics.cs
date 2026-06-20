// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using FiringSolution.Core.Models;

namespace FiringSolution.Core.Engine;

/// <summary>A sampled point on a trajectory (ENU position + time).</summary>
public readonly record struct TrajectoryPoint(double X, double Y, double Z, double T);

/// <summary>Where the round actually came down, and with what.</summary>
public sealed record Impact(
    Vec3 Position,   // ENU, gun at origin
    Vec3 Velocity,
    double Time,     // time of flight, s
    double Range,    // ground range from gun, m
    double Bearing,  // compass bearing of impact, deg
    double Speed,    // impact speed, m/s
    double Energy    // impact KE = ½mv², J  (work-energy theorem)
);

/// <summary>Full result of a kinetic simulation.</summary>
public sealed record Trajectory(
    IReadOnlyList<TrajectoryPoint> Points,
    Impact Impact,
    double Apex      // peak altitude above launch, m
);

/// <summary>
/// The authoritative ground-truth engine (design §13). It integrates the real
/// trajectory and reports where the round ACTUALLY lands; the player's
/// hand-derived solution is judged against this. The engine never reveals the
/// answer — it only obeys physics.
///
/// Forces (gated by tier): gravity (constant or g(h)) and quadratic aerodynamic
/// drag F = ½·ρ·v_rel²·C_d·A with 3D wind. Integrated with classical RK4; at
/// Hard tier this is genuinely non-analytic, exactly as the design intends.
/// </summary>
public static class Ballistics
{
    private const double Dt = 0.01;        // integration step, s
    private const double MaxTime = 1200.0; // ceiling so high arcs complete, s

    public static Trajectory SimulateKinetic(
        KineticWeapon weapon, EnvironmentSpec env, KineticSolution solution, TierFlags tier)
    {
        double surfaceG = Atmosphere.SurfaceGravity(env.World, env.SiteAltitude);
        double targetZ = env.TargetAltitude;

        Vec3 pos = Vec3.Zero;
        Vec3 vel = Vec3.FromAzimuthElevation(solution.Azimuth, solution.Elevation, solution.Speed);

        var points = new List<TrajectoryPoint> { new(pos.X, pos.Y, pos.Z, 0) };
        double t = 0;
        double apex = 0;
        int sampleEvery = 0;

        while (t < MaxTime)
        {
            var (nPos, nVel) = Rk4Step(pos, vel, Dt, env, weapon, tier, surfaceG);
            double nt = t + Dt;
            apex = Math.Max(apex, nPos.Z);

            // Ground/target-plane crossing on the way down.
            if (nVel.Z < 0 && pos.Z >= targetZ && nPos.Z <= targetZ)
            {
                double denom = pos.Z - nPos.Z;
                double frac = denom != 0 ? (pos.Z - targetZ) / denom : 0;
                Vec3 impactPos = pos + (nPos - pos) * frac;
                Vec3 impactVel = vel + (nVel - vel) * frac;
                double impactT = t + Dt * frac;
                points.Add(new(impactPos.X, impactPos.Y, impactPos.Z, impactT));
                return Build(points, impactPos, impactVel, impactT, apex, weapon);
            }

            pos = nPos;
            vel = nVel;
            t = nt;

            if (++sampleEvery >= 10) { points.Add(new(pos.X, pos.Y, pos.Z, t)); sampleEvery = 0; }
        }

        return Build(points, pos, vel, t, apex, weapon);
    }

    private static Vec3 Acceleration(
        Vec3 pos, Vec3 vel, EnvironmentSpec env, KineticWeapon weapon, TierFlags tier, double surfaceG)
    {
        double h = env.SiteAltitude + pos.Z;
        double g = tier.VariableG ? Atmosphere.GravityAt(env.World, h) : surfaceG;
        Vec3 a = new(0, 0, -g);

        if (tier.Drag && env.World.SeaLevelDensity > 0)
        {
            // Medium II holds density at the gun-site value (steady drag the player can
            // solve per-axis); Hard lets ρ vary with altitude (ρ(h)), coupling drag to g(h).
            double rho = tier.VariableDensity
                ? Atmosphere.DensityAt(env.World, h)
                : Atmosphere.DensityAt(env.World, env.SiteAltitude);
            Vec3 vrel = vel - env.Wind; // air-relative velocity
            double speed = vrel.Magnitude;
            if (speed > 0)
            {
                Munition m = weapon.Munition;
                // k = ½·ρ·C_d·A / mass
                double k = 0.5 * rho * m.DragCoeff * m.Area / m.Mass;
                a += vrel * (-k * speed);
            }
        }
        return a;
    }

    private static (Vec3 pos, Vec3 vel) Rk4Step(
        Vec3 pos, Vec3 vel, double dt, EnvironmentSpec env, KineticWeapon weapon, TierFlags tier, double surfaceG)
    {
        Vec3 a1 = Acceleration(pos, vel, env, weapon, tier, surfaceG);

        Vec3 p2 = pos + vel * (dt / 2), v2 = vel + a1 * (dt / 2);
        Vec3 a2 = Acceleration(p2, v2, env, weapon, tier, surfaceG);

        Vec3 p3 = pos + v2 * (dt / 2), v3 = vel + a2 * (dt / 2);
        Vec3 a3 = Acceleration(p3, v3, env, weapon, tier, surfaceG);

        Vec3 p4 = pos + v3 * dt, v4 = vel + a3 * dt;
        Vec3 a4 = Acceleration(p4, v4, env, weapon, tier, surfaceG);

        Vec3 dpos = (vel + 2 * (v2 + v3) + v4) * (dt / 6);
        Vec3 dvel = (a1 + 2 * (a2 + a3) + a4) * (dt / 6);
        return (pos + dpos, vel + dvel);
    }

    private static Trajectory Build(
        List<TrajectoryPoint> points, Vec3 impactPos, Vec3 impactVel, double impactT, double apex, KineticWeapon weapon)
    {
        double speed = impactVel.Magnitude;
        double m = weapon.Munition.Mass;
        var impact = new Impact(
            impactPos, impactVel, impactT,
            impactPos.MagnitudeXY, impactPos.Bearing, speed,
            0.5 * m * speed * speed);
        return new Trajectory(points, impact, apex);
    }
}
