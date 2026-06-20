// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
namespace FiringSolution.Core;

/// <summary>
/// Physical constants — all SI, textbook-accurate (design pillar 3:
/// "Physically honest"). Nothing here is invented; these are real constants
/// of nature, shared by every model in the Core.
/// </summary>
public static class Constants
{
    public const string Version = "V1.0";


    // Mechanics
    public const double G0 = 9.80665;          // standard gravity, m/s^2
    public const double G = 6.67430e-11;       // gravitational constant, m^3 kg^-1 s^-2

    // Earth
    public const double EarthRadius = 6.371e6; // mean radius, m
    public const double EarthMass = 5.972e24;  // mass, kg

    // Relativity / EM
    public const double C = 299_792_458.0;     // speed of light in vacuum, m/s

    // Particle physics
    public const double ElectronVolt = 1.602176634e-19; // eV in joules
    public const double ProtonRestMeV = 938.272;        // proton rest energy, MeV/c^2

    /// <summary>Proton rest energy m0·c^2 in joules.</summary>
    public static readonly double ProtonRestJoules = ProtonRestMeV * 1e6 * ElectronVolt;

    /// <summary>Proton rest mass in kg, from E = m c^2.</summary>
    public static readonly double ProtonMass = ProtonRestJoules / (C * C);

    // Air (sea-level ISA reference, Earth)
    public const double EarthSeaLevelDensity = 1.225;   // kg/m^3
    public const double EarthScaleHeight = 8500.0;      // m

    public static double DegToRad(double deg) => deg * Math.PI / 180.0;
    public static double RadToDeg(double rad) => rad * 180.0 / Math.PI;
}
