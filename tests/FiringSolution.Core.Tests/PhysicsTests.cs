// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using FiringSolution.Core;
using FiringSolution.Core.Content;
using FiringSolution.Core.Engine;
using FiringSolution.Core.Models;
using Xunit;

namespace FiringSolution.Core.Tests;

public class RelativisticTests
{
    [Fact]
    public void Lorentz_At094c_IsAbout2_931()
    {
        Assert.Equal(2.9314, Relativistic.Lorentz(0.94), 0.001);
    }

    [Fact]
    public void BetaFromGamma_RoundTrips()
    {
        double gamma = 5.0;
        double beta = Relativistic.BetaFromGamma(gamma);
        Assert.Equal(gamma, Relativistic.Lorentz(beta), 1e-9);
    }

    [Fact]
    public void ParticleKE_IsPositive_AndExceedsNewtonianAtHighBeta()
    {
        var w = Munitions.ProtonFocused();
        const double beta = 0.94;
        double relKE = Relativistic.ParticleKineticEnergy(w.RestEnergyJoules, beta);
        double m0 = w.RestEnergyJoules / (Constants.C * Constants.C);
        double v = beta * Constants.C;
        double newtonKE = 0.5 * m0 * v * v;

        Assert.True(relKE > 0);
        Assert.True(relKE > newtonKE, "Relativistic KE must exceed ½mv² at high β.");
    }

    [Fact]
    public void PulseEnergy_RisesWithBeta()
    {
        var w = Munitions.ProtonFocused();
        Assert.True(Relativistic.PulseEnergy(w, 0.96) > Relativistic.PulseEnergy(w, 0.90));
    }

    [Fact]
    public void Beam_FlightTime_IsSubMillisecondOverEngagementRange()
    {
        var w = Munitions.ProtonFocused();
        var shot = Relativistic.SimulateBeam(w, targetSlantRange: 40000, azimuth: 0, elevation: 10, beta: 0.94);
        Assert.True(shot.FlightTime < 1e-3);
    }

    [Fact]
    public void SpeedForFuseRange_RoundTrips()
    {
        // The speed that solves a fuse/range pair must, fed back through the dilated-fuse
        // detonation distance, land exactly at that range.
        double R = 1.2e10, fuse = 18;
        double beta = Relativistic.SpeedForFuseRange(R, fuse);
        Assert.InRange(beta, 0.0, 0.9999999);
        Assert.Equal(R, Relativistic.DetonationDistance(beta, fuse), R * 1e-6);
    }

    [Fact]
    public void DetonationDistance_RisesWithSpeed()
    {
        // Faster launch ⇒ more dilation and more reach ⇒ the fuse fires farther out.
        Assert.True(Relativistic.DetonationDistance(0.97, 18) > Relativistic.DetonationDistance(0.90, 18));
    }
}

public class AtmosphereTests
{
    [Fact]
    public void EarthSurfaceGravity_IsAbout9_8()
    {
        double g = Atmosphere.SurfaceGravity(Worlds.Earth, 0);
        Assert.Equal(9.8, g, 0.05);
    }

    [Fact]
    public void Gravity_DecreasesWithAltitude()
    {
        double low = Atmosphere.GravityAt(Worlds.Earth, 0);
        double high = Atmosphere.GravityAt(Worlds.Earth, 100_000);
        Assert.True(high < low);
    }

    [Fact]
    public void Density_DecaysExponentially()
    {
        double sea = Atmosphere.DensityAt(Worlds.Earth, 0);
        double up = Atmosphere.DensityAt(Worlds.Earth, Worlds.Earth.ScaleHeight);
        Assert.Equal(sea / Math.E, up, sea * 0.001);
    }
}
