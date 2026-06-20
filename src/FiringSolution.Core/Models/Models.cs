// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
namespace FiringSolution.Core.Models;

/// <summary>A world/planet: mass &amp; radius (give g) and an exponential atmosphere.</summary>
public sealed record World(
    string Id,
    string Name,
    double Radius,      // m
    double Mass,        // kg
    double SeaLevelDensity, // rho0, kg/m^3 (0 => no atmosphere)
    double ScaleHeight  // H, m
);

/// <summary>A kinetic munition: mass, drag area and ballistic behaviour.</summary>
public sealed record Munition(
    string Id,
    string Name,
    double Mass,        // kg
    double DragCoeff,   // dimensionless
    double Diameter,    // m
    double Splash       // splash radius, m (absorbs rounding error)
)
{
    /// <summary>Frontal reference area for drag, m^2.</summary>
    public double Area => Math.PI * (Diameter / 2.0) * (Diameter / 2.0);
}

/// <summary>A kinetic artillery weapon and its charge→muzzle-velocity curve.</summary>
public sealed record KineticWeapon(string Name, Munition Munition)
{
    /// <summary>Discrete propellant charge (1..MaxCharge) → muzzle velocity, m/s.</summary>
    public double MuzzleVelocity(int charge) => 120.0 + charge * 90.0;
    public int MaxCharge => 7;
}

/// <summary>
/// A relativistic particle-beam weapon. The beam SPEED is no longer fixed — the
/// player dials β at the station — so the weapon only carries what's invariant: the
/// particle's rest energy and how many of them ride in one pulse. The delivered pulse
/// energy is then ParticleCount·(γ−1)·m₀c².
/// </summary>
public sealed record BeamWeapon(
    string Name,
    string ProfileName,
    double RestEnergyJoules, // m0·c^2 of the particle, J
    double ParticleCount     // protons per pulse, N
);

/// <summary>The environment a kinetic shot flies through.</summary>
public sealed record EnvironmentSpec(
    World World,
    double SiteAltitude,    // gun altitude above surface, m
    double TargetAltitude,  // target altitude relative to gun, m
    Vec3 Wind               // ENU wind velocity, m/s (air's velocity)
);

/// <summary>The kinematic solution the player commits for a kinetic shot.</summary>
public readonly record struct KineticSolution(double Azimuth, double Elevation, double Speed);

/// <summary>
/// Which physics the oracle integrates (gated by mission tier, §7).
///   Drag            — aerodynamic drag is modelled (this is what couples WIND in).
///   VariableG       — g(h) = GM/(R+h)² instead of a constant g.
///   Wind            — a non-zero air velocity is present.
///   VariableDensity — ρ varies with altitude along the arc (ρ(h)); when false the
///                     air density is held at the gun-site value, so drag is steady.
/// </summary>
public readonly record struct TierFlags(bool Drag, bool VariableG, bool Wind, bool VariableDensity = false);
