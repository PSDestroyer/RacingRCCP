# Engine and tachometer synchronization

The Formula engine component now exposes one cached powertrain frame for both audio and the Formula HUD. It samples the actual RCCP Engine and Gearbox once per rendered frame, avoiding the CarController copy from the previous physics tick. The frame includes raw RPM, a shared 12 ms dezippered RPM, gear index, displayed gear, shift phase, limiter state and the configured gearbox shift-up RPM. Audio applies this in LateUpdate; the HUD can request the same frame earlier without sampling twice.

## Changes

- Removed the second pitch smoothing stage. The shared 12 ms RPM response drives the numeric tachometer, rev bar, shift lights, band weights and playback pitch. Volume/load smoothing remains independent.
- Removed the narrow 0.8–1.2 per-layer pitch ratio clamp that created flat audible regions. Each active band follows the RPM ratio. The broad 0.25–3.0 safety clamp lies outside the active ranges of the fallback bank.
- Added an optional measured `referenceRpm` to an authored EngineLayer. Zero retains provisional anchor-based fallback. No fabricated recording calibration is claimed.
- Gear labels correctly map RCCP index 0 to first gear, 1 to second, and so on; reverse and neutral remain distinct. Corrected the equivalent mapping in the legacy HUD as well.
- Shift lights reach full progression at `Gearbox.shiftUpRPM` (11,400 RPM on the base Generic). This is the actual RPM prerequisite, not the separate speed threshold. The shift indication also respects the current shift phase, top gear and reduced-motion preference.
- Mechanical gear-change audio uses the completed gear index from the same powertrain frame as the HUD; the combustion cut uses that frame’s shift phase. Recovery is shortened to 25 ms.

## Validation

The editor validator checks strictly increasing pitch at every audible band across the base RPM range, measured-reference support, a 90-percent dezipper response within 50 ms at 30/60/120 fps, forward/reverse/neutral gear mapping and ordered lights reaching the configured shift point.

The Play validator captures 20 seconds of actual RCCP autonomous driving, with explicit public gearbox up/down commands so both directions are covered. An Editor-only end-of-frame probe checks the actual TMP labels, the audio source pitch parameters, frame identity and shared RPM, and writes `EngineSynchronization.csv` with DSP timestamps alongside `GameplayAudio.wav`. Other existing tests retain pause/resume, scene return and contact coverage. Tests use an isolated save.

These checks establish agreement of the visual values and playback parameters. They do not measure end-to-end speaker/display latency or establish authentic Formula timbre. The fallback still uses the existing RCCP Sport2 recordings; measured load/coast recordings are the next art dependency.

## Latest observed result

Unity 6000.3.19f1: 42 reference/math checks passed. The complete Play test passed with 1197 sampled rendered frames, zero frame/gear/RPM-label mismatches, zero audio-vs-HUD RPM difference and zero error between the active AudioSource pitch parameters and the shared-frame target. It exercised 4 gear changes and 22 frames during shifts. The listener capture contains 20.01 seconds, peak -10.95 dBFS, and zero clipped samples. This is a parameter-synchronization result, not a speaker-latency or subjective timbre measurement.
