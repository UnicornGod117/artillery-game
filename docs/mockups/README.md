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
| `render_ui.py` | the renderer (Pillow); regenerate with `python3 render_ui.py` |

The green ✓ callouts and the legend strip enumerate what was **implemented this
session**: the working scientific calculator, the handbook overlay + tier-aware
HELP, the **NEW MISSION** flow, the persisted career score, stated input
precision, and — on the beam station — environment readouts driven by the real
mission plus an honestly-labelled relativistic-regime panel.
