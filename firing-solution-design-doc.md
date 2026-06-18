# FIRING SOLUTION — Design Document (v0.2)

*Working title; rebrandable later. A single-seat fire-control simulator in which the player performs the actual physics to produce artillery and directed-energy firing solutions. The thesis is the inverse of* IRON NEST*: where that game stages the* ritual *of calculation and lets the engine do the arithmetic, this one hollows out nothing — the world holds the ground truth, and the player's mathematics is the only path to it.*

---

## 1. Concept

You are the fire-control specialist for a single advanced weapon system — not the admiral who orders the shot, but the person held accountable for the numbers behind it. Intelligence arrives (briefings, spotters, sensor returns, sometimes only second- or third-hand fragments). From it you localise the target, characterise the environment, select your munition and propellant, and **derive the firing solution yourself** — the launch angles, the charge, the lead. You commit, you fire, and the round lands exactly where your physics said it would. The miss distance is a readout of your modelling error.

The fantasy is the *sniper's discipline applied to artillery*: patient, precise, and entirely your responsibility.

## 2. Design pillars

1. **You do the math; the world is the answer key.** Verification is diegetic — you see the impact on the plotting board, not a green tick. Correctness is confirmed by the universe obeying your arithmetic.
2. **No autosolver.** Instruments yield *measurements*, never *solutions*. The line is absolute: the game hands you the data an operator of that calibre would genuinely possess; it never hands you the answer or the model.
3. **Physically honest.** All numbers are textbook-accurate SI physics — real constants, real drag models, real special relativity. Nothing invented in the quantitative layer.
4. **Misfire is calibration, not failure.** Splash radius and visible impact convert a near-miss into information, so a wrong shot becomes a ranging shot rather than a game-over.
5. **One seat, one gun.** The intimacy of the specialist. Scale stays small so the cognition stays central.

## 3. Core loop

1. **Receive** — briefing and intel (target description, threat type, sometimes raw bearings rather than coordinates).
2. **Localise** — use in-world instruments (plotting map, ruler, protractor/compass, pencil) to triangulate target position from spotter bearings or fragmentary intelligence.
3. **Characterise the environment** — read instruments for wind vector, altitude, air data, local gravity (on exotic worlds).
4. **Configure the weapon** — select munition (mass, ballistic coefficient, drag/thermal profile) and propellant charge (sets muzzle velocity).
5. **Derive the firing solution** — by hand, with a scientific calculator and the handbook as the only aids. Produce the output schema (§5).
6. **Commit and fire** — input the final solution; watch the round fly.
7. **Observe and adjust** — see the impact and splash on the board; treat any miss as a calibration fire; re-solve and re-fire.
8. **Confirm** — target-destroyed / mission-complete indicators; multi-target boards mark each kill.

## 4. The calculation apparatus

The defining balance of the whole design. Split cleanly into *what the game provides* and *what the player must produce*.

**Provided (in-world instruments — measurement only):**
- A tactical map / plotting board with ruler, protractor, compass, and pencil for triangulation and geometry.
- Environmental sensors: wind speed and direction, altitude, air temperature/density, local gravitational field (exotic worlds).
- Briefing intelligence — sometimes clean coordinates, sometimes second/third-hand fragments requiring triangulation as a sub-task.
- A **scientific calculator** with saved history (handles arithmetic, *not* physics).
- A **handbook**: a complete reference of every formula the game can demand, plus a general maths cheat-sheet (algebra, trigonometry, differentiation, integration, vector decomposition). A knowledge dump, never a worked solution.

**Produced by the player:**
- The choice of *which physical model applies* (which effects matter at this fidelity).
- The full derivation and arithmetic.
- The final firing solution.

The honesty boundary is the calculator: it does the *numbers*, the player does the *physics*. Since this is a solo tool for your own practice, that boundary is partly on the honour system — and that's fine; the point is to keep your own physics sharp, not to defeat a cheater.

## 5. The firing solution — output schema

The committed answer is a small structured packet. Each field maps to real physics:

- **Azimuth (x-angle)** — horizontal traverse to target bearing, plus crosswind/lead correction.
- **Elevation (y-angle)** — launch angle above horizon.
- **Cross-axis correction (z-angle)** — only at higher breadth tiers, where 3D crosswind / out-of-plane motion forces a genuinely three-dimensional solution rather than a planar one.
- **Propellant charge count** — discretised; sets muzzle velocity (more charges → higher v₀, with the energy/recoil consequences that implies).
- **Munition type** — selects shell mass, and at Hard tier its ballistic coefficient / aerodynamic and thermal behaviour.

## 6. Difficulty architecture

Difficulty is **per-mission**, generated procedurally from independent sliders so the axes can be tuned separately:

- **Math fidelity** — the physical-depth ladder (§7). The "how hard is the physics" axis.
- **Triangulation difficulty** — how cleanly the target is localised (exact coordinates → noisy multi-source fragments).
- **Circumstance difficulty** — environmental and variable load (calm planar engagement → 3D wind, exotic gravity, multiple coupled effects).
- **Target predictability** *(proposed fourth slider)* — for moving targets, how obscure the motion pattern is. Every moving target must follow a *discoverable* pattern (or it can't be solved); this slider sets how hard that pattern is to infer. This cleanly separates "the motion model is hard to read" from "the ballistics are hard to compute."

These map to your two orthogonal axes from earlier: **math fidelity** climbs *depth*, while **circumstance** climbs *breadth*. Tuning them independently is what lets a mission be, say, trivially-modelled but combinatorially punishing, or physically deep but geometrically simple.

**Optional advanced mechanic (decision pending — this was question 5.2):** within a single high-fidelity mission, let the player *choose how completely to model*. Solve the cheap approximation (ignore drag, treat g as constant) — fast, but you accept miss-risk that splash may or may not cover — or solve the full model — slower, more precise. This makes "knowing which approximation is legal" a live tactical choice rather than a fixed gate, and it pairs naturally with the timed competitive mode (§9). Flagging it as opt-in because it adds real design and balancing work.

## 7. Physics scope per tier

All SI, all textbook-accurate.

**Easy — "Lay the gun."** One step past IRON NEST: you perform the trigonometry the console used to hide. Triangulate position (law of sines/cosines), resolve range, solve a 2D vacuum parabola with constant *g*. No drag.

**Medium I — depth (clean math, low dimensionality).**
- Full SUVAT on both axes.
- Energy and momentum methods — muzzle energy, impact energy via the work–energy theorem, recoil and terminal momentum via impulse.
- Relativistic correction for the particle weapon: Lorentz factor γ, relativistic momentum γm**v** and kinetic energy (γ−1)mc², invoked once β is high enough that the Newtonian ½mv² materially under-counts.
- Altitude-dependent gravity, g(h) = GM/(R+h)², for high-arc shots.

**Medium II — breadth (same primitives, far more bookkeeping).**
- 3D vector ballistics: crosswind from arbitrary azimuth decomposed into components, solved per-axis, recomposed — lead and deflection now live in two transverse directions.
- A live state vector: moving target (discoverable pattern), possibly moving platform, environment pushing — each step SUVAT-grade, the difficulty is orchestration and not dropping a term.
- Moving-target lead where flight time is non-trivial: solve for the intercept point.

**Hard — coupled and non-analytic.**
- Quadratic drag, F = ½ρv²C_dA — nonlinear, coupled, no closed form, so you integrate numerically by hand (stepwise Euler/RK). Munition aerodynamics now matter.
- Altitude-dependent density ρ(h) (exponential-atmosphere model) feeding the drag term and coupling back into variable g.
- Thermal load: aerodynamic heating / ablation on a kinetic slug, or energy-deposition heating for the beam.
- At the extreme: Coriolis deflection over long range; relativistic aberration / Doppler when sensing very fast closers.

**Weapon–difficulty coupling.** A relativistic beam's near-*c* flight collapses time-of-flight, making *lead trivial* but pushing all the difficulty into the energy/relativity/thermal regime. The kinetic shell is the inverse — long flight time makes *leading* the hard problem. The missile, with a constant-acceleration thrust phase, demands integrating the powered trajectory (hence your "extremely hard" note). Difficulty is therefore best expressed as **weapon × tier**, not tier alone.

## 8. Weapons roster

Five classes, with their primary physics emphasis:

- **Kinetic artillery** *(MVP)* — the core ballistic problem; lead-heavy; full fidelity ladder.
- **Relativistic particle weapon** *(MVP)* — energy/relativity-heavy; trivial lead; thermal at Hard.
- **Interceptor artillery** — defensive-flavoured intercept geometry (inverse problem).
- **Railgun** — extreme muzzle velocity; flattens trajectories, sharpens energy/thermal demands.
- **Missile** — constant-acceleration powered flight; integrate the thrust phase; the hardest profile.

Main long-term focus: varied **kinetic artillery** and **relativistic weaponry**.

**Throttling:** no resource economy yet. A **cooldown with a reload animation** (matched to weapon type) supplies both the pacing constraint and an in-fiction reason for it. Economy (power, heat budget, ammunition) is noted as a later option.

## 9. Targets, missions, feedback

**Targets:** static and moving ground vehicles; static and moving aircraft; orbital assets; ships. Any moving target must follow a *discoverable pattern* (predictability set by the slider) so a solution exists at all.

**Posture:** offensive focus, including "offensive aid to friendly units." Defensive interception is secondary.

**Worlds:** Earth primary, plus one or two exotic planets for variety — custom atmospheric density profiles and masses (hence different g), to make the environmental physics bite.

**Generation:** procedurally generated missions parameterised by the four sliders (§6). No campaign at launch; a **points / career system** persists progress.

**Feedback:** primarily *visual* — you see where the round lands on the board; splash radius absorbs small rounding error and turns near-misses into calibration data; mission-complete and per-target destroyed markers (an X or similar) confirm results. No turn-by-turn numeric grading.

**Scaffolding:** a **Help** button (hints on which equations apply) and a **Give up** button (reveals the correct firing-solution values) — both as graceful exits that preserve the learning value.

**Competitive mode:** an optional timed mode scoring on *both* speed and accuracy, which is where the deliberate-vs-fast modelling tradeoff (§6) earns its keep.

## 10. Information & uncertainty

Intel is generally *decent*, carrying a mild natural noise floor — present so that perfect-to-the-decimal inputs aren't assumed, but **not** the focus, so it shouldn't dominate the solve. Some missions make *triangulation from fragmentary, second/third-hand intelligence* an explicit sub-task; others hand down cleaner coordinates in the briefing.

## 11. Interface & aesthetic

Futuristic, but firmly *data-and-surveillance* futuristic rather than slick-autosolver futuristic — clean readouts, sensor feeds, a high-tech plotting board, with the tactile honesty of real instruments. The governing UX rule restates Pillar 2: **every instrument outputs measurements, never solutions.** Likely screens: plotting board, sensor panel, weapon-configuration panel, firing-solution input, observation view, handbook, calculator.

## 12. MVP scope (for yourself, v1.0)

- **2 weapons:** kinetic artillery + relativistic particle weapon (your stated focus; they contrast maximally and together exercise most of the ladder).
- **All 4 difficulty tiers.**
- **Procedural mission generation** from the four sliders.
- Earth + (optionally) one exotic planet.
- The full core loop, handbook, scientific calculator, Help / Give-up, visual impact feedback, cooldown/reload throttling, and a points/career save.

**Deferred:** campaign, resource economy, upgrade/progression tree, leaderboards, the remaining three weapon classes, additional worlds.

## 13. Platform & architecture

This ships as a **standalone desktop application**, not a web build. The reasoning: the app is two layers at once, and the browser is comfortable with only one of them.

- The **instrument-panel layer** — sensor readouts, weapon configuration, the firing-solution input, handbook, calculator — is dense, form-like, retained-mode UI.
- The **simulation / observation layer** — animated trajectory arcs, impact and splash, the reload animation, and eventually 3D ballistics and orbital geometry — is game-like and dynamic.

The web does the first acceptably and the second only against friction, precisely where the experience most wants to feel like a *machine*. A native shell removes that compromise. So the real decision is not "web or not" but *which layer dominates and whether 3D is genuinely on the roadmap*, since that picks the shell.

**Recommended shell — Godot 4 with C#.** A true engine, so it covers the simulation layer natively (animated arcs, particle splash, reload animation, 2D map input-picking essentially for free), carries a competent built-in UI system (Control nodes) more than adequate for the instrument panels, scales into 3D when Medium-II/Hard and orbital work demand it, and exports a standalone desktop binary. It keeps everything in C#, so the physics core ports directly. Cost: learning its node/scene paradigm, which is gentle.

**Alternatives.**
- **Avalonia (+ a SkiaSharp canvas)** — the spiritual successor to prior WPF work (XAML, data binding), cross-platform, unbeatable for the panel-heavy chrome. Choose it only if this turns out to be ~80% dashboard / 20% canvas and 3D never truly arrives; the simulation visuals are then hand-rolled.
- **MonoGame** — code-first, pure C#, total control over loop and rendering (suits a from-primitives temperament), but *all* UI is built by hand, which for an instrument-heavy game is a real cost.
- Avoid **WPF** (Windows-only, weak at animated game visuals) and **Unity** (heavyweight and licensing-encumbered for a solo, 2D-leaning maths game).

**The invariant that de-risks the choice — an isolated physics core.** The ground-truth engine is authoritative and wholly separate from anything shown to the player; the submitted solution is scored against the trajectory this engine actually simulates. At Hard tier the engine itself integrates the drag/heating ODEs (RK4) to obtain the true impact, and the hand-derived solution is judged against it within tolerance/splash. Building this as a **pure C# class library** — deterministic, fully unit-testable, with zero presentation dependencies — means the shell merely *consumes* it, the front end can be swapped without touching the science, and the intellectually rich part can begin now, ahead of the Godot-vs-Avalonia call.

**Project structure (three layers):**
- **Core** *(pure C# library — the oracle).* Public surface roughly: `Simulate(weapon, environment, solution) → Trajectory/Impact`; `Score(submittedSolution, groundTruth) → result (within tolerance/splash)`; `GenerateMission(sliders) → Mission`; plus the munition, environment, weapon, and atmosphere models. No rendering, no engine types, fully testable in isolation.
- **Shell** *(Godot / Avalonia / MonoGame).* Instrument panels, the plotting board and its instruments, the observation/visual-feedback layer, and input — consuming Core *only* through its public API.
- **Data** *(content as data, not code).* Munition tables, planet/atmosphere profiles, and handbook content, so new shells, shells-of-ammunition, and worlds are authored without recompiling the engine.

## 14. Open decisions

- **Fourth slider** — confirm *target predictability* (proposed) or substitute something else.
- **Within-mission fidelity choice** (§6, the 5.2 mechanic) — include as opt-in advanced play, or leave fidelity strictly gated by tier?
- **Firing-solution conventions** — coordinate frame and angle conventions (azimuth reference, sign of elevation, how the z-correction is expressed) for the input schema.
- **Calculator boundary** — accept the honour-system limit, or constrain the in-game calculator's feature set to discourage off-loading physics into it.
- **Shell choice** — Godot vs Avalonia, pending how central 3D becomes (Godot if the simulation/observation layer and 3D matter; Avalonia if it stays a 2D dashboard-with-canvas).
- **Title** — placeholder for now.
