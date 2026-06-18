using FiringSolution.Core.Models;

namespace FiringSolution.Core.Engine;

/// <summary>
/// Atmosphere &amp; gravity models (design §7):
///   - Altitude-dependent gravity  g(h) = G·M / (R+h)^2     (Medium I+)
///   - Exponential-atmosphere density  ρ(h) = ρ₀·e^(-h/H)   (Hard)
/// </summary>
public static class Atmosphere
{
    /// <summary>Local gravitational acceleration at altitude h (m) above surface.</summary>
    public static double GravityAt(World world, double h)
        => Constants.G * world.Mass / Math.Pow(world.Radius + h, 2);

    /// <summary>Air density at altitude h (m). Worlds with no atmosphere → 0.</summary>
    public static double DensityAt(World world, double h)
        => world.SeaLevelDensity <= 0 ? 0 : world.SeaLevelDensity * Math.Exp(-h / world.ScaleHeight);

    /// <summary>Surface gravity at the gun site (what a sensor would read).</summary>
    public static double SurfaceGravity(World world, double siteAltitude = 0)
        => GravityAt(world, siteAltitude);
}
