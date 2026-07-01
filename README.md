# FIRING SOLUTION

> Created by **Unicorn God** — released under the [MIT License](LICENSE).

A single-seat fire-control simulator in which the player performs the actual
physics to produce artillery and directed-energy firing solutions. *You do the
math; the world is the answer key.*

## Run

**First time only — double-click `INSTALL.bat`.**
It will download and install Godot 4.3 .NET edition and check for the .NET 8 SDK
(installing it via `winget` if missing). Takes a few minutes.

**Every time after — double-click `PLAY.bat`.**
It builds the latest code and launches the game. After any `git pull` it always
rebuilds from scratch, so you are never running a stale cached version.

**On Linux / macOS**, use `./play.sh` instead (same force-rebuild + launch). Point the
`GODOT` environment variable at your Godot 4.3 .NET editor, or have `godot` on your `PATH`.

Once in the game: **Commit & Fire** simulates exactly the azimuth / elevation /
charge you entered; the impact is stamped on the board, and a miss reports
range/line corrections. **Tab** switches between the two stations.

> The `Godot.NET.Sdk` version in `FiringSolution.Shell.csproj` (4.3.0) should
> match your installed Godot version.

---

See [`firing-solution-design-doc.md`](firing-solution-design-doc.md) for the full
design and [`Firing Solution - Vision.dc.html`](Firing%20Solution%20-%20Vision.dc.html)
for the interactive visual study (two station "directions": amber kinetic and
ice-blue relativistic beam). The **timed beam intercept** relativistic time-of-flight puzzle
and the **moving-target intercept lead** (Medium II) have both shipped; the remaining roadmap
is tracked in [`docs/roadmap.md`](docs/roadmap.md).

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

**No predicted paths (design pillar 2).** The program never forecasts where a round
will go. The only two pre-fire indicators are deliberately **short, fixed-length
stubs out of the gun** that just echo your inputs: a `BRG` heading tick (azimuth) on
the plotting board and a `LAY` barrel stub (elevation) on the vertical view. Both are
far too short to reach the target, so they can't show alignment or whether a shot
would land — you lay the gun by working the bearing, range and drop out of the
target's **grid coordinate** (the gun is not the origin; the board reads in absolute
battlespace coordinates). A trajectory is drawn *only after* you commit, and it is the real
one the oracle integrated. The calculator is arithmetic-only and holds no physics, so
it cannot predict either.

### Public API (what the shell calls)

```csharp
Mission m = GameEngine.GenerateMission(
    new DifficultySliders(WeaponKind.Kinetic, MathFidelity: 2, Seed: 42));

// player derives a solution by hand, then commits:
KineticResult r = GameEngine.FireKinetic(m, azimuth: 41.7, elevation: 38.0, charge: 5);
// r.Trajectory.Impact, r.Score.Miss / RangeError / LineError / Hit

// the beam commits a pointing + a launch SPEED β (v/c); the dilated fuse detonates at βγcτ:
BeamResult b = GameEngine.FireBeam(m, azimuth: 41.7, elevation: 12.0, beta: 0.937);
// b.Score.DetonationDistance, b.Score.RangeError (d − R) / OnAxis / Hit
```

## Physics scope (design §7)

| Tier      | Gravity | Drag             | Wind | Notes                                          |
|-----------|---------|------------------|------|------------------------------------------------|
| Easy      | const g | —                | —    | vacuum parabola, flat ground                   |
| Medium I  | g(h)    | —                | —    | full SUVAT, altitude-dependent gravity, drop   |
| Medium II | g(h)    | yes (steady ρ)   | yes  | drag couples 3D crosswind → genuine lead       |
| Hard      | g(h)    | yes (ρ(h))       | yes  | altitude-varying density couples drag↔g; RK4   |

At **Medium II** the air density is held at the gun-site value, so drag is steady and
the crosswind deflection is solvable per-axis. **Hard** lets density vary along the arc
(`ρ(h)`), coupling drag to altitude and gravity — the genuinely non-analytic regime.

The relativistic beam is a **long-range (light-second) proper-time warhead intercept**
(lead ≈ 0 at near-c), scored on two independent gates: pointing accuracy **and**
detonation range. You don't set an energy — the warhead's fuse fires after `τ` seconds
on its **own** clock, and time dilation stretches that to `γ·τ` for us, so it detonates
at `d = β·γ·c·τ`. You dial the launch **speed β** (as a % of c) so the dilated fuse lands
the blast on the target: `k = R/(c·τ)` (= R in light-seconds ÷ τ), `β = k/√(1 + k²)`.
Too slow detonates short, too fast overshoots, so "just max it out" doesn't win. Because
the flight is now seconds-to-minutes, the fly-out animation runs over the **real
(time-scaled) flight time**, with a `1×–15×` playback fast-forward.

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

## Build status / verification

**Verified** under the .NET 8 SDK — and now enforced on every push / PR by the
[CI workflow](.github/workflows/ci.yml) (`dotnet build -warnaserror` + `dotnet test`):

```bash
dotnet build           # Core + tests build clean (0 warnings)
dotnet test            # physics, scoring, mission-determinism + calculator suites pass
```

The shell's scientific calculator is pure C# (it deliberately holds no physics), so it is
**linked into the test project and unit-tested headlessly** — the recursive-descent
evaluator that does the player's arithmetic is now pinned for precedence, degree-mode trig,
domain errors and the recursion guard.

The Godot shell (`shell/godot`) also **compiles clean** against `Godot.NET.Sdk`
and the Core (`dotnet build` inside `shell/godot/`). Actually *running* the shell
still needs the Godot 4 editor + a display (it can't render headless in CI), so
the shell is intentionally kept out of `FiringSolution.sln` — that keeps root
`dotnet build` / `dotnet test` working on a machine without Godot installed.

### Fixes made while finishing the game

- **Beam kill-energy was unwinnable as displayed.** The shown kill threshold was
  rounded *below* the true gate, so a player delivering exactly the displayed
  energy failed the strict `E ≥ kill` test. The truth is now snapped to the same
  0.1 GJ grid the instrument reads out, so the readout is honestly achievable.
- **Give-up could fail on solvable missions.** The "give up" reveal now lives in
  the Core (`GameEngine.RevealKineticSolution`) and searches azimuth (for
  crosswind lead at Hard tier) with a coarse-to-fine elevation pass (needed where
  the arc is steep near a charge's max range and ~0.1° precision is required).
- **Input precision is now stated** on every firing-solution field and on the
  observed readouts (azimuth/elevation to 0.1°, range to 0.01 km, etc.).

### Shell feature status (see `docs/mockups/`)

The fire→score→observe loop is wired, and these instruments are now functional:

- **Scientific calculator** — a pop-up **button keypad** (`CalculatorView.cs`, you can
  also type into the display) over a real recursive-descent evaluator (`Calculator.cs`):
  digits, `+ − × ÷ ^ ( )`, `sin/cos/tan/asin/acos/atan/sqrt/ln/log/exp`, `pi`/`e`, `Ans`,
  history, and degree-mode trig to match the game's angle convention.
- **Handbook** (`HandbookView.cs`) — an overlay rendering the Core's formula
  reference, with the tier-aware `Handbook.HelpHint` banner; **HELP** uses the same.
- **NEW MISSION** — regenerates a fresh procedural mission in place.
- **Career score** (`Career.cs`) — persisted to `user://career.save`, updated live.
- **Beam environment** — now read from the real mission (closing velocity, target
  bearing, LOS-derived altitude, air data, local g) instead of hard-coded values.
- **Moving targets** (Medium II) — the `Predictability` slider turns the target into a
  tracker: the kinetic station reads out its observed **track** (speed + heading), the
  oracle scores the impact against the **lead point** `p(t) = Position + Velocity·t` at the
  round's true time of flight (`GameEngine.KineticTargetPositionAt`), and the give-up reveal
  runs a fixed-point intercept solver. So the player must *lead* the mover, by hand.
- **Shareable mission codes** (`MissionCode.cs`) — since a mission is a pure function of
  seed + sliders, the station shows a short code (e.g. `FS1-K2963-000011A7`) that reproduces
  the byte-identical mission for anyone who enters it.
- **Stated input precision** on every field and observed readout.

Also wired: a **difficulty selector** in the top bar (cycles the four tiers and
regenerates in place), a **munition selector** on the kinetic station, a real
**reload cooldown** (the fire button locks while the RELOAD bar refills) and a
post-fire **fly-out animation** of the round on both the board and the vertical
plane. A miss now reads back as a **spotter (OP-1) report** — range and bearing to
the round from the observation post, and how far off the target it looked from there
— so the correction must be triangulated rather than handed over.

Still placeholder: the **Z-correction** input (reserved for higher breadth tiers per
design §5, not used by the current planar solve). Reconstructions of both stations
are in [`docs/mockups/`](docs/mockups/).
