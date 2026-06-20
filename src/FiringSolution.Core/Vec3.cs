// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
namespace FiringSolution.Core;

/// <summary>
/// Minimal 3-vector in the ENU (East-North-Up) frame, metres.
///
/// Frame convention (design open-decision §14, fixed here):
///   X = East, Y = North, Z = Up.
/// Azimuth is measured CLOCKWISE FROM NORTH (0° = N, 90° = E), as on a compass.
/// Elevation is the angle above the local horizon (0° = flat).
/// </summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => a * s;

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public double Magnitude => Math.Sqrt(Dot(this));

    /// <summary>Horizontal (ground-plane) magnitude only — "ground range".</summary>
    public double MagnitudeXY => Math.Sqrt(X * X + Y * Y);

    /// <summary>Compass bearing (deg, 0..360) of this vector's horizontal part.</summary>
    public double Bearing
    {
        get
        {
            double b = Math.Atan2(X, Y) * 180.0 / Math.PI;
            return b < 0 ? b + 360 : b;
        }
    }

    /// <summary>
    /// Build a velocity vector from compass azimuth + elevation + speed.
    /// </summary>
    public static Vec3 FromAzimuthElevation(double azimuthDeg, double elevationDeg, double speed)
    {
        double az = Constants.DegToRad(azimuthDeg);
        double el = Constants.DegToRad(elevationDeg);
        double horiz = speed * Math.Cos(el);
        return new Vec3(
            horiz * Math.Sin(az), // East
            horiz * Math.Cos(az), // North
            speed * Math.Sin(el)  // Up
        );
    }
}
