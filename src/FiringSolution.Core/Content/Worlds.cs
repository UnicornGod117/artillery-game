using FiringSolution.Core.Models;

namespace FiringSolution.Core.Content;

/// <summary>
/// Worlds as data (design §13 "content as data, not code"). Earth (primary)
/// plus one exotic world (§9) so the environmental physics genuinely bites:
/// different mass → different g, different atmosphere → different drag.
/// </summary>
public static class Worlds
{
    public static readonly World Earth = new(
        Id: "earth", Name: "EARTH",
        Radius: Constants.EarthRadius, Mass: Constants.EarthMass,
        SeaLevelDensity: Constants.EarthSeaLevelDensity, ScaleHeight: Constants.EarthScaleHeight);

    /// <summary>Dense super-Earth with a thin, cold atmosphere (surface g ≈ 13.9).</summary>
    public static readonly World Kepler9c = new(
        Id: "kepler9c", Name: "KEPLER-9c",
        Radius: 8.9e6, Mass: 1.65e25,
        SeaLevelDensity: 0.42, ScaleHeight: 12000.0);

    public static readonly IReadOnlyList<World> All = new[] { Earth, Kepler9c };
}
