# Formula polish — 30 October 2026

The user confirmed that the preceding Nintendo Switch build runs well. This pass addresses music direction, track clearance, low-speed gamepad steering and opponent competitiveness. New changes were validated in the macOS Unity Editor, not on Switch hardware.

## Music

Replaced the restrained 108 BPM, 35-second bed with **Grid Pulse**, an original 128 BPM, 120-second electronic instrumental. Offline synthesis provides a structured rhythm section, bass, arpeggios and melodic lifts. No external samples or recordings were downloaded. `Tools/Audio/build_formula_music.py` reproduces the clip; the general audio generator also uses this arrangement.

The existing `PaddockTheme.wav` reference and GUID are retained. The clip remains stereo/48 kHz and Streaming. There is one music voice on the existing source, with no new AudioSource objects or runtime synthesis. Music now continues quietly during driving: countdown gain 0.04, normal driving 0.10, falling toward 0.065 under throttle. Saved mixer sliders remain independent. Pause and return-to-menu playback were exercised in Play Mode. Listening and final loudness/genre approval remain subjective and should be checked on the console.

## Track clearance

Inspected **all 18 circuits** in the Editor. The audit includes ordinary tree renderers, individual connected tree groups inside combined vegetation meshes, terrain tree instances and arrow bounds/ground height. Combined RTG vegetation was inspected per tree rather than treating an entire forest's bounding box as a road obstacle.

Confirmed defects were in `Formula_Circuit_08`: three palm instances standing in the racing corridor and seven arrow instances crossing the corridor or buried below the supporting ground. Disabled the complete palm instances, including their leaves and collision children; repositioned/grounded arrows while retaining their authored orientation. The scene diff consists of three active flags and seven transforms. Original environment prefabs, road meshes, route points, physical surface settings, NavMesh data and the other 17 scenes remain unchanged.

The repeated audit finds no remaining violations under these geometric checks. Before/after renders confirm the road is clear. This is geometric coverage of all tracks plus a physical race on track 08, not a claim that every possible visual defect on every circuit was reviewed from every driving camera. Re-run the audit and apply tools after regenerating circuits from their original source packs.

## Low-speed stick response

`FormulaSteering` now shapes the active gamepad's steering value between RCCP's device read and input processing. A continuous, symmetric response is gentler near center below 35 km/h and blends back to the existing response by 100 km/h. The RCCP deadzone is removed once; full-stick lock is retained. Small input changes and rack return/reversal receive short low-speed smoothing. High-speed rack calibration, keyboard input, AI and externally overridden controls keep their existing ownership.

The analog acceptance run uses a virtual Gamepad through the actual Input System bindings and real RCCP physics. It completed a valid stock career lap in **93.80 s**, with peak slip **4.87 degrees**, and checked normal AI takeover after finishing. The keyboard regression completed a valid lap in **93.07 s**, peak slip **4.86 degrees**. These are automated single-run regression results, not a physical Joy-Con/Pro Controller comfort assessment or statistically meaningful lap-time improvement.

An initial keyboard attempt lost application focus before measurement and was recorded as interrupted. The harness now waits for focus before adding a virtual input device; later focus loss still interrupts the run. The successful retry is stored separately. Legacy acceptance reports were restored after collecting the new results.

## Opponent competition

Root cause: `FormulaSession.Configure` explicitly disabled the pre-existing dynamic pacing system in Formula races. It is now enabled for races with opponents, with a six-second launch grace period. Time trials and non-race modes retain their previous behavior.

Reused the existing engine/driver pacing owner and added a continuous Formula-specific gap curve: a neutral close-racing band, up to **25% torque assistance** for a distant trailing opponent, and up to **5% reduction** for a distant leader. Existing smoothing and finish fade are retained. Progress comparisons are aligned with Formula's final-waypoint finish origin so crossing the line does not create a fictitious lap-sized gap.

Authored brake-zone profiles now receive the same smoothed pace multiplier, with speed-target assistance limited to **2.5% in tight corners** and **10% on faster sections**. Checkpoints, lap awards, race ordering and physical transport are unchanged by pacing. References are cached; no per-frame scene scans or additional per-opponent control objects were added.

On the corrected track, all four autonomous RCCP cars completed a lap; the player recorded a valid lap and the opponents required **zero recovery rejoins**. The test exercised the trailing-assistance branch up to 1.25 and verified playback through race, pause and menu return. Ahead reduction and finish fade were checked by deterministic tests. A four-car autonomous test cannot establish final difficulty against a skilled human; tune from further console playtests if needed.

## Evidence and limits

- `LogicValidation.json`: 771 checks for stick range/symmetry, pacing bounds/fade, prefab wiring and streaming audio configuration.
- `TrackAuditBefore.json`, `TrackAudit.json`, `TrackChanges.json`: full 18-circuit before/after clearance evidence and exact changed objects.
- `Track08-Before.png`, `Track08-After.png`: Editor render comparison.
- `CompetitionPlay.json`: actual four-car physics, pacing telemetry, race/pause/menu audio checks; passed without runtime errors.
- `GamepadLap.json`, `KeyboardLap.json`: full input-device laps and post-finish ownership checks; passed without errors.
- `KeyboardLap-FocusInterrupted.json`: preserved environmental interruption.
- `AssetValidation.json`, `Music.json`: source audio analysis and preservation of the user's pre-existing font/build-settings edits.

No Switch SDK/module is installed on this Mac. A new Windows Switch build and console check are still needed for subjective music/handling balance, human-versus-AI difficulty, frame time and hardware memory. No shader, texture-compression or platform-build settings were changed by this pass.
