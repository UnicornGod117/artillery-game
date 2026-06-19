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

        // Drag-free tiers (Easy / Medium I) can reach right out to ~40 km, so let the
        // target sit anywhere in that radius. At the drag tiers (Medium II / Hard) the
        // round is drag-limited, so the reachable envelope stays nearer the gun.
        double maxRange = tierIdx >= 2 ? 11000 : 40000;
        double range = Math.Round(rng.Lerp(4000, maxRange));
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

        // Place the gun somewhere on a battlespace grid (NOT at the origin). Drawn last so
        // the deterministic sequence above is untouched. Offsetting by ≥ maxRange keeps the
        // target's absolute coordinate (gun + relative) in the positive quadrant.
        var gunOrigin = new Vec3(
            Math.Round(maxRange + rng.Lerp(0, 8000)),
            Math.Round(maxRange + rng.Lerp(0, 8000)),
            siteAltitude);

        return new Mission(
            Id: "MSN-" + ((uint)seed % 10000).ToString("D4"),
            WeaponKind: WeaponKind.Kinetic,
            World: world,
            TierIndex: tierIdx, TierName: TierNames[tierIdx], TierLabel: TierLabels[tierIdx],
            Flags: flags, Sliders: s, Seed: seed,
            KineticWeapon: weapon, Environment: env,
            KineticTarget: target, KineticObserved: observed,
            Splash: weapon.Munition.Splash,
            GunOrigin: gunOrigin);
    }

    private static Mission GenerateBeam(DifficultySliders s, Rng rng, int seed)
    {
        int tierIdx = Math.Clamp(Math.Max(1, s.MathFidelity), 0, 3); // beam never trivial
        var flags = FlagsFor(tierIdx);
        World world = s.Circumstance > 0.5 ? Worlds.Kepler9c : Worlds.Earth;
        var weapon = Munitions.ProtonFocused();

        double bearing = Math.Round(rng.Lerp(0, 360) * 10) / 10;
        double losElevation = Math.Round(rng.Lerp(4, 18) * 10) / 10;

        // Long-range proper-time WARHEAD intercept. We pick the launch speed the kill is
        // tuned to (β in 0.85–0.98) and a fuse time τ (seconds, on the warhead's own clock),
        // then DERIVE the slant range the dilated fuse detonates at: R = β·γ·c·τ. At these
        // speeds the flight time is γ·τ (seconds → minutes) — long enough to simulate and
        // animate, and the regime where time dilation is the whole puzzle. The player runs
        // the inversion: k = R/(c·τ), β = k/√(1 + k²). (Tip: R in light-seconds ÷ τ = k.)
        double betaSol = 0.85 + 0.13 * rng.Next();
        double fuse = Math.Round(rng.Lerp(6, 30));            // s, integer for clean arithmetic
        double gammaSol = Relativistic.Lorentz(betaSol);
        double slantRange = betaSol * gammaSol * Constants.C * fuse;   // R = βγcτ (exact truth)
        // Detonation window: a fraction of R, tightening with circumstance, floored so the
        // band is always achievable from a β dialled to 0.001 %c.
        double detTol = Math.Max(5e6, (0.008 - 0.004 * s.Circumstance) * slantRange);

        var target = new BeamTarget(slantRange, bearing, losElevation, fuse, detTol);

        // The target is handed over as a precise COORDINATE (no spotter loop exists for the
        // beam), so the observed geometry IS the truth — pointing and the range R fall out
        // of the coordinate to well within tolerance. The dilation solve is the gate.
        var observed = new BeamObserved(
            SlantRange: slantRange,
            Bearing: bearing,
            LosElevation: losElevation,
            Closing: Math.Round(rng.Lerp(2000, 12000)),       // m/s, intercept closing (flavour)
            FuseSeconds: fuse,
            DetonationToleranceMeters: detTol);

        // Emitter on the battlespace grid (NOT at the origin); drawn last so the sequence
        // above stays deterministic. Offset ≥ max possible R → target coordinate stays in
        // the positive quadrant. (max R ≈ 0.98·γ(0.98)·c·30 ≈ 4.4e10 m.)
        var gunOrigin = new Vec3(
            Math.Round(5e10 + rng.Lerp(0, 1e10)),
            Math.Round(5e10 + rng.Lerp(0, 1e10)),
            0);

        return new Mission(
            Id: "MSN-" + ((uint)seed % 10000).ToString("D4"),
            WeaponKind: WeaponKind.Beam,
            World: world,
            TierIndex: tierIdx, TierName: TierNames[tierIdx], TierLabel: TierLabels[tierIdx],
            Flags: flags, Sliders: s, Seed: seed,
            BeamWeapon: weapon, BeamTarget: target, BeamObserved: observed,
            AngularTolerance: 0.18,
            GunOrigin: gunOrigin);
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
