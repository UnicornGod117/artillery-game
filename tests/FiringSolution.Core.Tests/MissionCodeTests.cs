// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using System;
using FiringSolution.Core;
using FiringSolution.Core.Models;
using Xunit;

namespace FiringSolution.Core.Tests;

/// <summary>
/// Shareable mission codes. A mission is a pure function of seed + sliders, so a code that
/// carries those reproduces the byte-identical mission for anyone who types it. These tests
/// pin the round trip, code idempotency, that a code regenerates the same mission, and that
/// malformed input is rejected cleanly.
/// </summary>
public class MissionCodeTests
{
    [Theory]
    [InlineData(WeaponKind.Kinetic, 2, 42)]
    [InlineData(WeaponKind.Beam, 3, 123456)]
    [InlineData(WeaponKind.Kinetic, 0, -777)]           // negative seed must survive the uint hex round trip
    public void RoundTrip_PreservesTheMissionDeterminants(WeaponKind kind, int tier, int seed)
    {
        var s = new DifficultySliders(kind, MathFidelity: tier,
            Triangulation: 0.3, Circumstance: 0.6, Predictability: 0.4, Seed: seed);
        var back = MissionCode.Decode(MissionCode.Encode(s));

        Assert.Equal(kind, back.WeaponKind);
        Assert.Equal(tier, back.MathFidelity);
        Assert.Equal(seed, back.Seed);
    }

    [Fact]
    public void Encode_IsIdempotentThroughDecode()
    {
        // Slider values snap to the code's digit grid on first encode, so a second round
        // trip must reproduce the exact same string.
        var s = new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 2,
            Triangulation: 0.3, Circumstance: 0.55, Predictability: 0.9, Seed: 2024);
        string once = MissionCode.Encode(s);
        string twice = MissionCode.Encode(MissionCode.Decode(once));
        Assert.Equal(once, twice);
    }

    [Fact]
    public void DecodeIsCaseInsensitiveAndTrims()
    {
        var s = new DifficultySliders(WeaponKind.Beam, MathFidelity: 2, Seed: 5);
        string code = MissionCode.Encode(s);
        var back = MissionCode.Decode("  " + code.ToLowerInvariant() + "  ");
        Assert.Equal(s.Seed, back.Seed);
        Assert.Equal(s.WeaponKind, back.WeaponKind);
    }

    [Fact]
    public void SameCode_RegeneratesTheIdenticalMission()
    {
        var s = new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 2,
            Triangulation: 0.3, Circumstance: 0.3, Predictability: 0.0, Seed: 99);
        string code = MissionCode.Encode(s);

        var a = GameEngine.GenerateMission(MissionCode.Decode(code));
        var b = GameEngine.GenerateMission(MissionCode.Decode(code));
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.KineticTarget!.Range, b.KineticTarget!.Range);
        Assert.Equal(a.KineticTarget!.Bearing, b.KineticTarget!.Bearing);
    }

    [Fact]
    public void Encode_WithoutSeed_Throws()
        => Assert.Throws<ArgumentException>(() => MissionCode.Encode(new DifficultySliders(WeaponKind.Kinetic)));

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("FS1-K2000")]              // too short (no seed)
    [InlineData("FS1-Z2000-0000002A")]     // bad weapon letter
    [InlineData("FS1-K9000-0000002A")]     // tier out of range
    [InlineData("FS1-K200000000002A")]     // missing separator
    [InlineData("FS1-K2000-ZZZZZZZZ")]     // seed not hex
    public void Decode_RejectsMalformedCodes(string code)
        => Assert.Throws<FormatException>(() => MissionCode.Decode(code));
}
