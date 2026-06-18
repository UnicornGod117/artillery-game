# Station mockups — visual review

Godot can't render headless in CI, so these are **faithful reconstructions of the
two fire-control stations built from the C# shell source** (`shell/godot/scripts/`):
the layout, palette (`Ui.cs`) and on-screen text mirror the real `StationView`,
`KineticStation` and `BeamStation`, and the numbers are the **real Core mission
data** for the two default seeds (kinetic `MSN-4471`, beam `MSN-9120`).

They are a design-review aid, not a pixel-exact Godot capture — font metrics and
sub-pixel spacing will differ from the live engine.

| | |
|---|---|
| `firing_solution_kinetic.png` | Direction A — amber kinetic gunnery |
| `firing_solution_beam.png` | Direction B — ice-blue relativistic beam |
| `firing_solution_calculator.png` | the pop-up scientific calculator keypad (`CalculatorView.cs`) |
| `render_ui.py` / `render_calc.py` | the renderers (Pillow) |

The calculator is a **clickable button keypad** (you can also type into the display):
digits, `+ − × ÷ ^ ( )`, `sin/cos/tan/asin/acos/atan/√/ln/log/exp/π/e`, `Ans`, `C`,
`⌫`, `=`, with degree-mode trig and a short history. It is arithmetic-only and holds
no physics, so it cannot predict a path or compute a firing solution.

**No predicted paths:** the simulation views never forecast a trajectory. The two
pre-fire indicators are short, fixed-length stubs out of the gun — a `BRG` heading
tick (azimuth) and a `LAY` barrel stub (elevation) — far too short to reach the
target, so they only echo your inputs and can't show alignment or a hit. A path is
drawn only *after* you commit, and it is the real arc the oracle simulated.

The green ✓ callouts and the legend strip enumerate what was **implemented this
session**: the working scientific calculator, the handbook overlay + tier-aware
HELP, the **NEW MISSION** flow, the persisted career score, stated input
precision, and — on the beam station — environment readouts driven by the real
mission plus an honestly-labelled relativistic-regime panel.

**Measurement tools** (design §4): the plotting-board toolbar adds a cartesian
**GRID**, a **RULER** (distance + bearing between two points — see the live
`5.55 km · 58.6°` readout), a **PROTRACTOR** (angle at a vertex), and a **PEN** for
freehand construction lines, so the player can work distances out by hand. These
yield measurements only — never the firing solution.

**Vertical-plane altitude:** the view now plots target altitude relative to the gun
with a dashed **gun-level (0 m)** line, so a target *above* the gun (beam: +7.4 km)
sits above it and a target *below* the gun (kinetic: −98 m) sits below it — no longer
clamped to the baseline.
