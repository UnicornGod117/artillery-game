namespace FiringSolution.Core.Models;

/// <summary>The four independent difficulty sliders (design §6).</summary>
public sealed record DifficultySliders(
    WeaponKind WeaponKind = WeaponKind.Kinetic,
    int MathFidelity = 1,        // 0..3 → easy / medium1 / medium2 / hard
    double Triangulation = 0.3,  // 0..1 noise in localisation
    double Circumstance = 0.3,   // 0..1 environmental/variable load
    double Predictability = 0.0, // 0..1 (reserved: moving-target pattern obscurity)
    int? Seed = null
);

public enum WeaponKind { Kinetic, Beam }

/// <summary>True kinetic target (the oracle scores against this; never shown raw).</summary>
public sealed record KineticTarget(Vec3 Position, double Range, double Bearing, double Altitude);

/// <summary>Noisy intel actually shown to the player (mild noise floor, §10).</summary>
public sealed record KineticObserved(
    double Range, double Bearing, double Altitude,
    double WindSpeed, double WindFrom,
    double AirTemp, double AirDensity, double LocalG);

/// <summary>
/// True beam target. The kill needs the protons to arrive inside an energy WINDOW
/// (RequiredPulseEnergy ± EnergyTolerance): too weak and they don't penetrate, too
/// hot and they over-penetrate without depositing. The player sets the beam SPEED
/// (β); the delivered energy is N·(γ−1)·m₀c², so there is exactly one band of β that
/// works — "max it out and win" fails by overshooting the window.
/// </summary>
public sealed record BeamTarget(
    double SlantRange, double Bearing, double LosElevation,
    double RequiredPulseEnergyJoules, double EnergyToleranceJoules);

public sealed record BeamObserved(
    double SlantRange, double Bearing, double LosElevation,
    double Closing, double AirTemp, double AirDensity, double LocalG,
    double RequiredEnergyGJ, double ToleranceGJ);

/// <summary>A fully-specified mission produced by the generator.</summary>
public sealed record Mission(
    string Id,
    WeaponKind WeaponKind,
    World World,
    int TierIndex,
    string TierName,
    string TierLabel,
    TierFlags Flags,
    DifficultySliders Sliders,
    int Seed,
    // kinetic-specific (null for beam)
    KineticWeapon? KineticWeapon = null,
    EnvironmentSpec? Environment = null,
    KineticTarget? KineticTarget = null,
    KineticObserved? KineticObserved = null,
    double Splash = 0,
    // beam-specific (null for kinetic)
    BeamWeapon? BeamWeapon = null,
    BeamTarget? BeamTarget = null,
    BeamObserved? BeamObserved = null,
    double AngularTolerance = 0.18,
    // Absolute position of the gun/emitter in the battlespace grid (ENU metres). The
    // target is given to the player as an absolute COORDINATE (gun + relative); range,
    // bearing and elevation are theirs to work out. Defaults to the origin so a mission
    // built without a battlespace (older tests) behaves exactly as gun-at-centre.
    Vec3 GunOrigin = default
);
