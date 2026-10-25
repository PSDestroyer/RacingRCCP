# Gameplay UX and UI revision

The authored `FormulaRace.prefab` now owns the session goal, Pause recap and directional overlap indicators alongside the existing race instruments and minimap. `FormulaRaceAuthoring.RefineHud` edits the existing prefab in place and wires the result caption; no competing runtime HUD builder was introduced. All 18 circuit scenes resolve to the same existing Formula race presentation.

`FormulaRaceView` derives goals, validity, physical overlap and measured checkpoint splits from the current session and live racers. Career Time Attack displays the same reference-lap multiplier (1.2) used for Bronze. Race position remains authoritative in `GamePlayManager`. Fixed lap counts and reward rules were not changed. On the final invalid lap, instructions direct the player to finish and use Retry; an earlier valid lap is explicitly retained.

A local overlap signal uses each actual rival position relative to the player's horizontal heading: 1.25–7 m lateral, at most 6 m longitudinal and 2.5 m elevation difference. A 0.4-second release hold avoids flicker. It is not a race-time gap calculation. Solo sessions hide these signals and standings.

`FormulaResultsView` explains missing valid laps, missed time targets and finishes outside the top three. Failed sessions select Retry; passed career sessions select Next Event when available. Rebinding results clears runtime listeners and restores the appropriate two- or three-button layout.

Panels use opaque black geometry to maintain contrast over bright circuit rendering, with the existing red, white and yellow identity. The minimap and vehicle instruments end at the same lower margin. Temporary notifications occupy the space above the minimap and fade on rendered frames; reduced motion uses immediate transitions.

## Validation

See `Validation.json`, the two resolution reports and the PNG captures in this folder. The final runtime and prefab implementation passed 20 checks at 1280×720 and 21 at 1920×1080. The 1080p run additionally covers four scripted result-presentation variants, including reuse after failure. Keyboard driving and virtual gamepad pause/resume were exercised. HUD text at 115% and 95% safe area was checked at both resolutions. Saves were isolated in temporary paths.

The retry test waits for the replacement scene handle before submitting its scripted successful result; it cannot accidentally finish the outgoing scene. Proximity geometry probes clear the earlier real warning's release hold before testing elevation rejection. Glyph layout checks use visible character vertices rather than the font's unused ascender/descender area.

Finish, split, RPM and proximity boundary cases were scripted; these runs do not claim natural race completion. No standalone build or Switch device test was performed for this revision. Previous gameplay evidence remains in `../Gameplay`; its summary links to this revision.
