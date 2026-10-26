# Camera rain for the wet circuit

Updated on 2026-10-26 for `Formula_Circuit_06` (`track_06`). The user requested fewer beads and natural movement responding to vehicle speed, turns and braking while retaining a low Nintendo Switch cost.

## Presentation and ownership

`FormulaCameraRain` remains a scene component override on the circuit's gameplay prefab instance. It owns one independent screen overlay canvas below the HUD (sorting order -100), containing one `FormulaLensDroplets` graphic. The full-screen repeating texture has been replaced with 22 individual beads in a single mesh: 88 vertices and 44 triangles. Most beads spawn near the sides, keeping the central driving view clearer. Beads vary in radius, lifetime, adhesion and mobility; they fade in/out and stretch along their velocity.

The player Rigidbody is obtained from the existing GamePlayManager. Fixed-step world velocity differences supply filtered acceleration, limited to 30 m/s² to contain impact/reset spikes. In LateUpdate, gravity and opposite vehicle acceleration are projected into the active RCCP camera's space. Lateral inertia deflects droplets in turns, speed adds outward/upward airflow, and braking helps release pinned beads and increases runoff. Each bead approaches its own target velocity with damping. This is a cosmetic approximation; it never changes vehicle physics and does not use raw steering or brake input to fake motion.

The imported Windshield Rain 512 × 512 normal texture remains as subtle surface detail inside each bead. The shader provides a smooth meniscus with highlights, without scene refraction or blur. All beads share the same material. No renderer features, vendor assets or render pipeline settings changed.

## Cost controls

- One graphic and one shader pass: one added draw call in the measured fixed view.
- Only bead rectangles rasterize; measured rectangle area was about 0.3% of the screen at 1280 × 720, compared with the former full-screen quad. This is geometric coverage, not measured GPU time.
- One normal texture lookup per covered pixel; no scene-color capture, extra camera, blur or render textures.
- Fixed array of 22 states, private deterministic RNG, cached player reference, reusable UI mesh buffers. No per-bead GameObjects, particles, colliders or per-frame material instantiation.
- Isolated canvas keeps bead mesh rebuilds separate from the HUD.
- Simulation freezes on pause. Menu/results hide the layer; disabling/unloading destroys its owned canvas.

Low draw-call count alone does not establish a console frame-time budget; see [Unity GPU optimization guidance](https://unity.com/how-to/gpu-optimization).

## Validation

`PlayValidation.json` records the actual wet race in Unity 6000.3.19f1 on Apple M2 Pro. An isolated temporary save was used. Real RCCP inputs ran 2.5 seconds of acceleration, 1.2 seconds steering left, 1.2 seconds right and 1.8 seconds braking, without seeded velocities or pose changes. The manoeuvre is a telemetry fixture rather than a completed driving lap; it can leave the track.

The test reached about 85 km/h, observed camera-space lateral inertia of +0.67 g and -0.65 g in opposite turns, and about 1.02 g of forward inertia on braking. Mean bead velocity changed direction with the turns and runoff increased during braking. The telemetry source was verified as the actual player Rigidbody.

The race was then paused to stabilize the scene, and 30 distinct rendered frames were sampled per overlay mode. Draw calls were 204 without rain and 205 with rain: +1. UnityStats includes Editor rendering, so these totals are comparative Editor measurements, not Switch totals.

The supported shader had no shader errors; no runtime errors were recorded. Geometry must exist with positive bounded coverage. Disable/re-enable creates exactly one overlay and menu state hides it. Screenshots: `CameraRainOff.png`, `CameraRainOn.png`.

The test restores the prior save override, play-start scene and background setting on leaving Play Mode. No Switch device GPU time or build was measured. Verify frame time on the target console before final performance sign-off.

## Tuning and repeating

`FormulaCameraRain.Intensity` defaults to 0.72; zero disables drawing. The 22-bead budget is `FormulaLensDroplets.DropCount`. All other circuits remain unaffected.

Use `Tools > Formula Career > Apply Lightweight Camera Rain` to repair material/component wiring, or `Validate Lightweight Camera Rain` to repeat the isolated driving and rendering check. Save open scene edits first.
