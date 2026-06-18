using FiringSolution.Core.Content;
using FiringSolution.Core.Models;

namespace FiringSolution.Core.Engine;

/// <summary>
/// Procedural mission generation from the four sliders (design §6). Carries the
/// TRUE target the oracle scores against, plus a separate set of OBSERVED
/// measurements shown to the player (with a mild intel noise floor, §10). The
/// player never sees the truth. Deterministic given a seed → reproducible.
/// </summary>
public static class MissionGenerator
{
    private static readonly string[] TierNames = { "easy", "medium1", "medium2", "hard" };
    private static readonly string[] TierLabels = { "EASY", "MEDIUM I", "MEDIUM II", "HARD" };

    public static Mission Generate(DifficultySliders sliders)
    {
        int seed = sliders.Seed ?? Random.Shared.Next();
        var rng = new Rng((uint)seed);
        return sliders.WeaponKind == WeaponKind.Beam
            ? GenerateBeam(sliders, rng, seed)
            : GenerateKinetic(sliders, rng, seed);
    }

    private static TierFlags FlagsFor(int tierIdx) => new(
        Drag: tierIdx >= 2,            // Medium II+: drag — this is what makes crosswind bite
        VariableG: tierIdx >= 1,       // Medium I+: g(h)
        Wind: tierIdx >= 2,            // Medium II+: meaningful wind / crosswind lead
        VariableDensity: tierIdx >= 3);// Hard: ρ(h) varies along the arc and couples to g(h)

    private static Mission GenerateKinetic(DifficultySliders s, Rng rng, int seed)
    {
        int tierIdx = Math.Clamp(s.MathFidelity, 0, 3);
        var flags = FlagsFor(tierIdx);
        bool useExotic = s.Circumstance > 0.6 && tierIdx >= 1 && rng.Next() > 0.5;
        World world = useExotic ? Worlds.Kepler9c : Worlds.Earth;

        var weapon = Munitions.KineticArtillery();
        double siteAltitude = tierIdx >= 1 ? Math.Round(rng.Lerp(0, 1600)) : 0;

        double range = Math.Round(rng.Lerp(4000, 11000));
        double bearing = Math.Round(rng.Lerp(0, 360) * 10) / 10;
        double tAlt = tierIdx >= 1 ? Math.Round(rng.Lerp(-120, 260)) : 0;

        var target = new KineticTarget(
            new Vec3(
                range * Math.Sin(Constants.DegToRad(bearing)),
                range * Math.Cos(Constants.DegToRad(bearing)),
                tAlt),
            range, bearing, tAlt);

        double windSpeed = flags.Wind ? Math.Round(rng.Lerp(4, 16) * 10) / 10 : 0;
        double windFrom = Math.Round(rng.Lerp(0, 360));
        var env = new EnvironmentSpec(
            world, siteAltitude, tAlt,
            flags.Wind ? WindVector(windSpeed, windFrom) : Vec3.Zero);

        double localG = Atmosphere.SurfaceGravity(world, siteAltitude);
        double rho = Atmosphere.DensityAt(world, siteAltitude);

        double triNoise = s.Triangulation * 0.02;
        var observed = new KineticObserved(
            Range: Math.Round(rng.Noisy(range, triNoise)),
            Bearing: Math.Round(rng.Noisy(bearing, triNoise * 0.5) * 10) / 10,
            Altitude: tAlt,
            WindSpeed: windSpeed,
            WindFrom: windFrom,
            AirTemp: Math.Round(rng.Lerp(-10, 25) * 10) / 10,
            AirDensity: Math.Round(rho * 1000) / 1000,
            LocalG: Math.Round(localG * 1000) / 1000);

        return new Mission(
            Id: "MSN-" + ((uint)seed % 10000).ToString("D4"),
            WeaponKind: WeaponKind.Kinetic,
            World: world,
            TierIndex: tierIdx, TierName: TierNames[tierIdx], TierLabel: TierLabels[tierIdx],
            Flags: flags, Sliders: s, Seed: seed,
            KineticWeapon: weapon, Environment: env,
            KineticTarget: target, KineticObserved: observed,
            Splash: weapon.Munition.Splash);
    }

    private static Mission GenerateBeam(DifficultySliders s, Rng rng, int seed)
    {
        int tierIdx = Math.Clamp(Math.Max(1, s.MathFidelity), 0, 3); // beam never trivial
        var flags = FlagsFor(tierIdx);
        World world = s.Circumstance > 0.5 ? Worlds.Kepler9c : Worlds.Earth;
        var weapon = Munitions.ProtonFocused();

        double slantRange = Math.Round(rng.Lerp(18000, 46000));
        double bearing = Math.Round(rng.Lerp(0, 360) * 10) / 10;
        double losElevation = Math.Round(rng.Lerp(4, 18) * 10) / 10;
        // Kill threshold is a published spec the instrument reads out exactly, not a
        // noisy measurement — so snap the TRUTH to the displayed 0.1 GJ grid. Otherwise
        // the readout rounds below the true gate and a player who delivers exactly the
        // shown energy fails the strict (E ≥ kill) test through no fault of their own.
        double killEnergyGJ = Math.Round(rng.Lerp(2.5, 4.5) * (1 + 0.25 * s.Circumstance) * 10) / 10;
        double killEnergy = killEnergyGJ * 1e9;

        var target = new BeamTarget(slantRange, bearing, losElevation, killEnergy);

        double triNoise = s.Triangulation * 0.015;
        var observed = new BeamObserved(
            SlantRange: Math.Round(rng.Noisy(slantRange, triNoise)),
            Bearing: Math.Round(rng.Noisy(bearing, triNoise * 0.5) * 10) / 10,
            LosElevation: Math.Round(rng.Noisy(losElevation, triNoise) * 10) / 10,
            Closing: Math.Round(rng.Lerp(120, 480)),
            AirTemp: Math.Round(rng.Lerp(-60, -10)),
            AirDensity: Math.Round(Atmosphere.DensityAt(world, 12000) * 1000) / 1000,
            LocalG: Math.Round(Atmosphere.SurfaceGravity(world, 12000) * 100) / 100,
            KillEnergyGJ: killEnergyGJ);

        return new Mission(
            Id: "MSN-" + ((uint)seed % 10000).ToString("D4"),
            WeaponKind: WeaponKind.Beam,
            World: world,
            TierIndex: tierIdx, TierName: TierNames[tierIdx], TierLabel: TierLabels[tierIdx],
            Flags: flags, Sliders: s, Seed: seed,
            BeamWeapon: weapon, BeamTarget: target, BeamObserved: observed,
            AngularTolerance: 0.18);
    }

    /// <summary>Wind compass-"from" direction → ENU velocity (air moves toward from+180).</summary>
    private static Vec3 WindVector(double speed, double fromDeg)
    {
        double toDeg = (fromDeg + 180) % 360;
        return new Vec3(
            speed * Math.Sin(Constants.DegToRad(toDeg)),
            speed * Math.Cos(Constants.DegToRad(toDeg)),
            0);
    }

    /// <summary>Deterministic mulberry32 RNG so missions are reproducible.</summary>
    private sealed class Rng
    {
        private uint _a;
        public Rng(uint seed) => _a = seed;

        public double Next()
        {
            _a += 0x6D2B79F5u;
            uint t = _a;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296.0;
        }

        public double Lerp(double lo, double hi) => lo + Next() * (hi - lo);

        public double Noisy(double value, double fraction)
            => fraction <= 0 ? value : value * (1 + (Next() * 2 - 1) * fraction);
    }
}
