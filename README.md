# FIRING SOLUTION

A single-seat fire-control simulator in which the player performs the actual
physics to produce artillery and directed-energy firing solutions. *You do the
math; the world is the answer key.*

See [`firing-solution-design-doc.md`](firing-solution-design-doc.md) for the full
design and [`Firing Solution - Vision.dc.html`](Firing%20Solution%20-%20Vision.dc.html)
for the interactive visual study (two station "directions": amber kinetic and
ice-blue relativistic beam).

## Architecture

Per design §13, the project is built in three layers, with an **isolated,
authoritative physics core** as the invariant that de-risks the shell choice:

```
FiringSolution.sln
├── src/FiringSolution.Core/        # the oracle — pure C#, zero presentation deps
│   ├── Constants.cs                #   real SI constants of nature
│   ├── Vec3.cs                     #   ENU 3-vector (East/North/Up)
│   ├── Models/                     #   World, Munition, weapons, Environment, Mission
│   ├── Engine/
│   │   ├── Atmosphere.cs           #   g(h) = GM/(R+h)²,  ρ(h) = ρ₀e^(−h/H)
│   │   ├── Ballistics.cs           #   RK4 trajectory oracle (vacuum + quadratic drag, 3D wind)
│   │   ├── Relativistic.cs         #   special relativity: γ, β, (γ−1)m₀c²
│   │   ├── Scoring.cs              #   diegetic scoring → range/line/miss geometry
│   │   └── MissionGenerator.cs     #   procedural missions from the four sliders
│   ├── Content/                    # DATA layer — worlds, munitions, handbook as data
│   └── GameEngine.cs               # public surface the shell consumes
└── tests/FiringSolution.Core.Tests # xUnit: physics, scoring, mission determinism
```

The **Core** is the ground-truth engine (design §1–§3): the submitted solution
is scored against the trajectory the engine actually simulates. Instruments
yield *measurements*, never *solutions* — the player derives azimuth /
elevation / charge by hand and commits; the engine obeys the physics and stamps
the real impact. A miss is calibration data, not failure.

### Public API (what the shell calls)

```csharp
Mission m = GameEngine.GenerateMission(
    new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 2, Seed: 42));

// player derives a solution by hand, then commits:
KineticResult r = GameEngine.FireKinetic(m, azimuth: 41.7, elevation: 38.0, charge: 5);
// r.Trajectory.Impact, r.Score.Miss / RangeError / LineError / Hit
```

## Physics scope (design §7)

| Tier      | Gravity | Drag | Wind | Notes                                  |
|-----------|---------|------|------|----------------------------------------|
| Easy      | const g | —    | —    | vacuum parabola                        |
| Medium I  | g(h)    | —    | —    | full SUVAT, altitude-dependent gravity |
| Medium II | g(h)    | —    | yes  | 3D crosswind, lead                     |
| Hard      | g(h)    | yes  | yes  | quadratic drag — non-analytic, RK4     |

The relativistic beam is energy/γ-led (lead ≈ 0 at near-c), scored on two
independent gates: pointing accuracy **and** delivered pulse energy ≥ kill
threshold via (γ−1)m₀c².

## Build & test

Requires the .NET 8 SDK.

```bash
dotnet build
dotnet test
```

## Shell — Godot 4 + C#

The recommended shell (design §13). The instrument panels and the simulation /
observation views (the two stations from the visual study — amber kinetic and
ice-blue beam) are built **programmatically in C#** so everything stays in C#
and the physics Core is consumed only through `GameEngine`.

```
shell/godot/
├── project.godot                 # Godot 4 project (main scene = Main.tscn)
├── FiringSolution.Shell.csproj   # Godot.NET.Sdk; references the Core
├── Main.tscn                     # minimal scene: a Control running Main.cs
├── icon.svg
└── scripts/
    ├── Main.cs                   # entry; switch kinetic ↔ beam (Tab / button)
    ├── StationView.cs            # shared chrome: top bar + 3-column layout
    ├── KineticStation.cs         # Direction A — amber gunnery
    ├── BeamStation.cs            # Direction B — ice-blue relativistic beam
    ├── PlottingBoard.cs          # tactical board: pan/zoom, rings, aim, impact
    ├── VerticalPlane.cs          # range/altitude view (real simulated arc)
    ├── Compass.cs                # wind dial
    └── Ui.cs                     # palette + styled-control factory helpers
```

### Run

1. Install [Godot 4.3 **.NET/Mono** edition](https://godotengine.org/download)
   and the .NET 8 SDK.
2. Open `shell/godot/project.godot` in the Godot editor (it will build the C#
   solution, restoring the Core reference).
3. Press **Play**. **Commit & Fire** simulates exactly the azimuth / elevation /
   charge you entered; the impact is stamped on the board, and a miss reports
   range/line corrections. **Tab** switches between the two stations.

> The `Godot.NET.Sdk` version in `FiringSolution.Shell.csproj` (4.3.0) should
> match your installed Godot version.

## Build status / verification

The container this was authored in has a network allowlist that blocks the .NET
SDK installer and the apt/NuGet feeds, so the C# could **not** be compiled or
test-run here yet. The Core is written to compile cleanly under .NET 8; once a
machine with the SDK (and NuGet access) is available:

```bash
dotnet test            # runs the Core unit tests
```

The Godot shell additionally requires the Godot 4 editor to build/run (it cannot
run headless in CI without an export template + display).
