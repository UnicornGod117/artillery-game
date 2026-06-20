// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
namespace FiringSolution.Core.Engine;

/// <summary>How a kinetic shot landed relative to the true target.</summary>
/// <remarks>
/// Verification is diegetic (design pillar 1): we return geometry, not a grade.
/// A miss is calibration data so the player can re-solve.
/// </remarks>
public sealed record KineticScore(
    double Miss,      // total ground miss, m
    double RangeError,// along-line, m  (+long / -short)
    double LineError, // cross-line, m  (+left / -right)
    double Splash,
    bool Hit
);

/// <summary>How a beam shot landed: two independent gates (pointing AND detonation range).</summary>
public sealed record BeamScore(
    double AzError,
    double ElError,
    double AngError,          // deg
    double LateralMiss,       // m at target range
    double DetonationDistance,// d = βγ·c·τ, m (where the dilated fuse fired)
    double RangeError,        // d − R, m  (+beyond / −short of the target)
    bool OnAxis,
    bool DetonationOk,
    bool Hit
);

/// <summary>Scores a fired shot against the ground truth the engine simulated.</summary>
public static class Scoring
{
    public static KineticScore ScoreKinetic(Impact impact, Vec3 target, double splash)
    {
        Vec3 i = impact.Position;
        double alongMag = Math.Sqrt(target.X * target.X + target.Y * target.Y);
        if (alongMag < 1e-9) alongMag = 1e-9;
        double ux = target.X / alongMag, uy = target.Y / alongMag; // downrange unit

        double dx = i.X - target.X, dy = i.Y - target.Y;
        double rangeErr = dx * ux + dy * uy;        // + long / - short
        double lineErr = ux * dy - uy * dx;         // z of (u × delta): + => LEFT
        double miss = Math.Sqrt(dx * dx + dy * dy);

        return new KineticScore(miss, rangeErr, lineErr, splash, miss <= splash);
    }

    public static BeamScore ScoreBeam(
        BeamShot beam, double aimAzimuth, double aimElevation,
        double targetBearing, double targetLosElevation, double targetSlantRange,
        double fuseProperTime, double detonationToleranceMeters, double angularToleranceDeg)
    {
        double azErr = aimAzimuth - targetBearing;
        azErr = ((azErr + 540) % 360) - 180; // wrap to [-180,180]
        double elErr = aimElevation - targetLosElevation;
        double angErr = Math.Sqrt(azErr * azErr + elErr * elErr);
        double lateralMiss = Math.Abs(targetSlantRange * Math.Sin(Constants.DegToRad(angErr)));

        bool onAxis = angErr <= angularToleranceDeg;
        // Detonation range is a WINDOW, not a floor: the dilated fuse fires at one fixed
        // distance d = βγ·c·τ. Too slow → short, too fast (β maxed) → overshoots — so the
        // player must solve the β whose dilation lands the blast on the target.
        double d = Relativistic.DetonationDistance(beam.Beta, fuseProperTime);
        double rangeError = d - targetSlantRange;
        bool detOk = Math.Abs(rangeError) <= detonationToleranceMeters;
        return new BeamScore(azErr, elErr, angErr, lateralMiss, d, rangeError, onAxis, detOk, onAxis && detOk);
    }
}
