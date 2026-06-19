using FiringSolution.Core.Models;

namespace FiringSolution.Core.Engine;

/// <summary>Result of a beam shot (lead ≈ 0 at near-c; difficulty is energy/γ).</summary>
public sealed record BeamShot(
    Vec3 Direction,
    Vec3 Endpoint,
    double FlightTime,      // s — typically sub-millisecond
    double Gamma,
    double Beta,
    double Speed,           // m/s
    double PulseEnergyJoules,// N·(γ-1)m₀c² delivered by the pulse, J
    double ParticleKE,      // (γ-1)m₀c², J
    double ParticleMomentum // γ·m₀·v, kg·m/s
);

/// <summary>
/// Special-relativity model for the particle beam (design §7). Real SR — no
/// approximations in the quantitative layer.
/// </summary>
public static class Relativistic
{
    public static double Lorentz(double beta) => 1.0 / Math.Sqrt(1.0 - beta * beta);

    public static double BetaFromGamma(double gamma) => Math.Sqrt(1.0 - 1.0 / (gamma * gamma));

    /// <summary>Relativistic kinetic energy of one particle: (γ-1)·m₀·c² (J).</summary>
    public static double ParticleKineticEnergy(double restEnergyJoules, double beta)
        => (Lorentz(beta) - 1.0) * restEnergyJoules;

    /// <summary>Relativistic momentum magnitude: γ·m₀·v (kg·m/s).</summary>
    public static double ParticleMomentum(double restMassKg, double beta)
        => Lorentz(beta) * restMassKg * (beta * Constants.C);

    /// <summary>
    /// Total energy a pulse of N particles delivers at speed β: N·(γ−1)·m₀c² (J).
    /// </summary>
    public static double PulseEnergy(BeamWeapon weapon, double beta)
        => weapon.ParticleCount * ParticleKineticEnergy(weapon.RestEnergyJoules, beta);

    /// <summary>
    /// Simulate a beam shot as a straight ray at βc; reports flight time but lead is
    /// negligible over engagement ranges. The player commits the beam SPEED β (v/c);
    /// the delivered pulse energy N·(γ−1)·m₀c² falls out of it.
    /// </summary>
    public static BeamShot SimulateBeam(
        BeamWeapon weapon, double targetSlantRange, double azimuth, double elevation, double beta)
    {
        beta = Math.Clamp(beta, 0.0, 0.999999);
        double gamma = Lorentz(beta);
        double speed = beta * Constants.C;

        Vec3 dir = Vec3.FromAzimuthElevation(azimuth, elevation, 1.0);
        Vec3 endpoint = dir * targetSlantRange;
        double flightTime = speed > 0 ? targetSlantRange / speed : 0;
        double restMass = weapon.RestEnergyJoules / (Constants.C * Constants.C);

        return new BeamShot(
            dir, endpoint, flightTime, gamma, beta, speed,
            PulseEnergy(weapon, beta),
            ParticleKineticEnergy(weapon.RestEnergyJoules, beta),
            ParticleMomentum(restMass, beta));
    }
}
