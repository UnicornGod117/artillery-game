// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using System.Globalization;
using FiringSolution.Core.Models;

namespace FiringSolution.Core;

/// <summary>
/// A compact, human-shareable code for a mission. Because a mission is a pure function
/// of its seed and the four difficulty sliders (design §6), those few values are all a
/// code needs to carry — anyone who types the same code gets the byte-identical mission.
/// This turns the single-player puzzle into something social: "solve <c>FS1-K2953-0000002A</c>".
///
/// Format: <c>FS1-{K|B}{tier}{tri}{cir}{prd}-{seed:X8}</c>
///   • <c>K</c>/<c>B</c>  — weapon (Kinetic / Beam)
///   • <c>tier</c>        — MathFidelity 0..3
///   • tri/cir/prd        — the three 0..1 sliders, each a base-36 digit (round(v·35), 0..Z)
///   • seed               — the 32-bit seed as 8 uppercase hex digits
/// </summary>
public static class MissionCode
{
    private const string Prefix = "FS1-";
    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Encode a mission's sliders (which must carry a concrete seed) into a code.</summary>
    public static string Encode(DifficultySliders s)
    {
        if (s.Seed is null)
            throw new System.ArgumentException("A shareable code needs a concrete seed.", nameof(s));

        char kind = s.WeaponKind == WeaponKind.Beam ? 'B' : 'K';
        int tier = System.Math.Clamp(s.MathFidelity, 0, 3);
        string seedHex = ((uint)s.Seed.Value).ToString("X8", CultureInfo.InvariantCulture);

        return $"{Prefix}{kind}{tier}{Digit(s.Triangulation)}{Digit(s.Circumstance)}{Digit(s.Predictability)}-{seedHex}";
    }

    /// <summary>Parse a code back into sliders. Throws <see cref="System.FormatException"/> if malformed.</summary>
    public static DifficultySliders Decode(string code)
    {
        if (code is null) throw new System.FormatException("null mission code");
        string c = code.Trim().ToUpperInvariant();
        if (!c.StartsWith(Prefix) || c.Length != Prefix.Length + 5 + 1 + 8)
            throw new System.FormatException($"not a mission code: '{code}'");

        string body = c.Substring(Prefix.Length);          // {kind}{tier}{tri}{cir}{prd}-{seed}
        if (body[5] != '-') throw new System.FormatException("missing '-' separator");

        WeaponKind kind = body[0] switch
        {
            'K' => WeaponKind.Kinetic,
            'B' => WeaponKind.Beam,
            _ => throw new System.FormatException("weapon must be K or B"),
        };
        int tier = body[1] - '0';
        if (tier < 0 || tier > 3) throw new System.FormatException("tier must be 0..3");

        double tri = Value(body[2]), cir = Value(body[3]), prd = Value(body[4]);

        string seedHex = body.Substring(6);
        if (!uint.TryParse(seedHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed))
            throw new System.FormatException("seed must be 8 hex digits");

        return new DifficultySliders(kind, tier, tri, cir, prd, unchecked((int)seed));
    }

    private static char Digit(double v)
        => Base36[System.Math.Clamp((int)System.Math.Round(System.Math.Clamp(v, 0, 1) * 35), 0, 35)];

    private static double Value(char ch)
    {
        int i = Base36.IndexOf(ch);
        if (i < 0) throw new System.FormatException($"bad slider digit '{ch}'");
        return i / 35.0;
    }
}
