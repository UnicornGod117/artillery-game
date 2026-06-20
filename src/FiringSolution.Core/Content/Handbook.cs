// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
namespace FiringSolution.Core.Content;

public sealed record HandbookEntry(string Name, string Formula);
public sealed record HandbookSection(string Title, IReadOnlyList<HandbookEntry> Entries);

/// <summary>
/// A reference of every formula the game can demand, plus a maths cheat-sheet
/// (design §4). A knowledge dump — NEVER a worked solution for the live shot.
/// </summary>
public static class Handbook
{
    public static readonly IReadOnlyList<HandbookSection> Sections = new[]
    {
        new HandbookSection("Trigonometry & Triangulation", new[]
        {
            new HandbookEntry("Law of sines", "a/sin A = b/sin B = c/sin C"),
            new HandbookEntry("Law of cosines", "c² = a² + b² − 2ab·cos C"),
            new HandbookEntry("Bearing from components", "θ = atan2(E, N)"),
        }),
        new HandbookSection("Vacuum ballistics (constant g)", new[]
        {
            new HandbookEntry("Range (flat)", "R = v₀²·sin(2θ) / g"),
            new HandbookEntry("Time of flight", "T = 2·v₀·sinθ / g"),
            new HandbookEntry("Apex height", "h = v₀²·sin²θ / (2g)"),
            new HandbookEntry("Horizontal motion", "x = v₀·cosθ·t"),
            new HandbookEntry("Vertical motion", "y = v₀·sinθ·t − ½·g·t²"),
        }),
        new HandbookSection("Energy & momentum", new[]
        {
            new HandbookEntry("Kinetic energy", "E = ½·m·v²"),
            new HandbookEntry("Work–energy theorem", "ΔE = W = F·d"),
            new HandbookEntry("Impulse / momentum", "J = Δp = m·Δv"),
        }),
        new HandbookSection("Gravity & atmosphere", new[]
        {
            new HandbookEntry("Altitude gravity", "g(h) = G·M / (R+h)²"),
            new HandbookEntry("Exponential density", "ρ(h) = ρ₀·e^(−h/H)"),
            new HandbookEntry("Quadratic drag", "F = ½·ρ·v²·C_d·A"),
        }),
        new HandbookSection("Special relativity (beam)", new[]
        {
            new HandbookEntry("Lorentz factor", "γ = 1 / √(1 − β²),  β = v/c"),
            new HandbookEntry("Time dilation", "Δt = γ·Δτ  (a moving clock runs slow)"),
            new HandbookEntry("Proper-time fuse", "fuse fires at τ (its clock) = γ·τ (ours)"),
            new HandbookEntry("Detonation range", "d = β·γ·c·τ"),
            new HandbookEntry("Speed for a range", "k = R/(c·τ),  β = k/√(1 + k²)"),
            new HandbookEntry("Relativistic KE", "E_k = (γ − 1)·m₀·c²"),
            new HandbookEntry("Pulse energy (N particles)", "E = N·(γ − 1)·m₀·c²"),
        }),
        new HandbookSection("Vectors", new[]
        {
            new HandbookEntry("Decompose", "vₓ = v·cosθ,  v_y = v·sinθ"),
            new HandbookEntry("Magnitude", "|v| = √(vₓ² + v_y² + v_z²)"),
            new HandbookEntry("Crosswind component", "w⊥ = w·sin(φ_wind − φ_fire)"),
        }),
    };

    /// <summary>Tier-aware Help hints (which equations apply — never the answer).</summary>
    public static string HelpHint(string tier) => tier switch
    {
        "easy" => "Vacuum parabola, constant g. Triangulate range, then R = v₀²·sin(2θ)/g.",
        "medium1" => "Full SUVAT both axes; account for g(h) and target altitude. Energy via ½mv².",
        "medium2" => "Decompose crosswind into along/cross components; lead by flight time.",
        "hard" => "Drag is non-analytic — integrate numerically. ρ(h) feeds drag and couples to g(h).",
        "beam" => "Lead ≈ 0. Work bearing/elevation & range R from the coordinates. The warhead's fuse fires after τ on its OWN clock; dilation stretches that to γτ, so it detonates at d = βγcτ. Solve the speed to land d on the target: k = R/(c·τ) (= R in light-seconds ÷ τ), β = k/√(1 + k²). Too fast overshoots.",
        _ => "Read the board. Every instrument gives measurements, never solutions.",
    };
}
