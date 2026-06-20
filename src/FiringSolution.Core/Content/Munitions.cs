// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using FiringSolution.Core.Models;

namespace FiringSolution.Core.Content;

/// <summary>Munitions and weapon profiles as data (design §5/§8).</summary>
public static class Munitions
{
    public static readonly Munition M72HeFrag = new(
        Id: "m72_hefrag", Name: "M-72 HE-FRAG",
        Mass: 43.2, DragCoeff: 0.255, Diameter: 0.155, Splash: 70);

    public static readonly Munition M81ApHe = new(
        Id: "m81_aphe", Name: "M-81 AP-HE",
        Mass: 51.0, DragCoeff: 0.21, Diameter: 0.155, Splash: 45);

    public static readonly IReadOnlyList<Munition> All = new[] { M72HeFrag, M81ApHe };

    public static KineticWeapon KineticArtillery(Munition? munition = null)
        => new("KINETIC ARTILLERY", munition ?? M72HeFrag);

    /// <summary>Protons per pulse — sized so a pulse near β≈0.94 delivers a few GJ.</summary>
    public const double ProtonsPerPulse = 1.0e19;

    public static BeamWeapon ProtonFocused() => new(
        Name: "RELATIVISTIC BEAM",
        ProfileName: "PROTON · FOCUSED",
        RestEnergyJoules: Constants.ProtonRestJoules,
        ParticleCount: ProtonsPerPulse);
}
