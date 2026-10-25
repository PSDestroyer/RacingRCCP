# Gameplay UI/UX polish

The existing gameplay layout is retained. This revision refines results, feedback and highlight balance; it does not redesign Home, Career or Garage.

## Results and input

- A successful one-lap event shows one **LAP TIME**, taken from the valid lap. Multi-lap results retain session time and best lap.
- Failure explanation and RESULT SAVED share the left column with Retry. Timing and actual nonzero rewards remain in the right column. Invalid runs still award no career progress; valid finishes that miss a medal keep their real rewards.
- Unlocked livery art increases from 200 × 100 to 320 × 176 design units (2.816 times the area). Its name, XP and bonus are grouped alongside it. The optional next-event recap is hidden in this reward variant; Next Event remains directly available.
- Result fade: 0.18 seconds. Livery fade: 0.28 seconds. Both use unscaled time, leave actions available immediately and settle instantly with Reduced Motion.
- Existing prefab identifiers, explicit button navigation, Retry reload and Next Event briefing routing are preserved. CanvasGroups are authored in the prefab; no new runtime hierarchy builder is introduced.

## Audio

Six original, softly enveloped oscillator cues are stored under Assets/Formula/Audio/UI: Focus (65 ms), Confirm (145 ms), Record (440 ms), Result (350 ms), Unlock (620 ms), and End (270 ms). They contain no sampled recordings. Mono 24 kHz PCM totals approximately 91 KB of sample data; peak amplitudes are below 0.26.

FormulaAudio uses the existing SoundManager SFX source, mixer and volume setting. Focus is quiet and rate limited; duplicate same-frame cues are suppressed. Initial outcome focus does not compete with the result cue. A final lap does not trigger a competing record cue. No AudioSource is created per interaction. The existing music and vehicle audio routes are retained.

## Circuit presentation

The glaring road used a smoothness value of 1. Formula-owned clones use dry smoothness 0.22 and dielectric specular settings, retaining their original URP Lit/Simple Lit shaders, textures and UVs. Five cloned materials replace 67 material slots across the affected dry scenes. Vendor materials are untouched. Scenes that do not use those sources need no material substitution.

Imported daytime directional lights above intensity 2 are capped at 2. This reduces clipped vehicle highlights without changing paint/wetness handling or darkening the accepted garage presentation. Light colors and directions remain unchanged. The wet Circuit 06 and existing night lighting are preserved. SurfaceAuthoring.json records the per-scene/material application. Unity 6.3 also writes its current renderer serialization fields when saving these scenes.

## Import compatibility

WindshieldRainAsset appeared during this work. Its HDRP runtime/editor assemblies initially failed to compile because this project uses URP and has no HDRP package. Their asmdefs now require a version-defined WINDSHIELD_HDRP_AVAILABLE symbol from com.unity.render-pipelines.high-definition. The package content is retained and HDRP is not installed. This is a compilation compatibility fix, not rain gameplay integration.

## Validation

FormulaGameplayHudPreview authors the assets, uses an isolated temporary save and exercises a four-car Racing Visuals circuit, a solo RTG event, invalid/valid Shakedown results, Retry and Next Event. It checks 115% text, 95% safe area, visible TMP glyphs, reduced motion, immediate result focus, livery fade completion, actual keyboard movement, and virtual gamepad pause/results navigation.

Scripted checkpoints, rival positions, RPM and finish receipts are labelled boundary tests. These checks do not constitute a naturally completed race, physical controller/Switch test, listening/mix review or standalone build. See Validation.json and its resolution-specific reports for the final run results.
