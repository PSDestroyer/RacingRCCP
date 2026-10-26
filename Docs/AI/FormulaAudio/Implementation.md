# Formula sound design integration

## Ownership and data

Formula audio observes RCCP telemetry and collisions; it does not change handling, friction, suspension, powertrain, AI or progression. The eight Generic livery prefabs and shared race prefab own `FormulaEngineAudio` and `FormulaContactAudio`. Existing `SoundManager` owns UI, music and `FormulaSoundscape`; there is no second persistent audio manager.

`Assets/Formula/Resources/Formula/Audio/SoundBank.asset` holds the shared clips and mixer routes. The Formula mixer separates player engine, opponents, tyres, contacts, world, UI and music. Existing Vehicle, SFX and Music settings control exposed parent gains once. Menu, Race, Pause and Results snapshots change presentation without overwriting saved volume settings.

## Vehicle and track behavior

- RPM bands use continuous equal-power crossfades, smoothed RPM/load and restrained pitch. Throttle release, shifts and limiter cuts affect the sound. Cockpit filtering and distance-based opponent detail are separate from player sound.
- Light chassis impacts use mass-normalized impulse and contact normal speed, including contacts below the former 800 Ns gate. Sustained contact drives a scrape loop; stopping contact fades it. Metal, barriers, vehicle body contacts and road impacts select separate clip families.
- Wheel telemetry drives asphalt, kerb, grass, gravel, wet-road and snow rolling layers, progressive tyre scrub and suspension hits. Static surface metadata is saved in all 18 circuits. Terrain uses a baked dominant-surface map, sampled without per-frame terrain allocations.
- Contact transients are pooled. Only the two nearest opponents within 50 m receive full contact sound, and distant engine voices are culled. Player voices have higher priorities; the existing Unity real-voice limit remains unchanged. This is a priority budget, not a claim that every simultaneous source stays below 32 voices.
- Trackside crowds are spatial and use the World route. Menus receive a restrained original music loop and room ambience. Race music fades out. UI remains usable during listener pause.
- Countdown, GO, invalid lap, wrong way and final lap have semantic cues. UI distinguishes focus, confirm, back, error, purchase and equip. Same-frame UI requests are prioritized, and repetitive warnings have cooldowns.

## Source provenance and tuning

`Tools/Audio/build_formula_audio.py` creates 32 original deterministic synthesized support clips and records provenance beside them. These include surface/contact details, UI/race cues, room/rain beds and a short original electronic menu theme. Existing RCCP impact, skid, shift and Sport2 engine clips and RTG crowd audio remain project dependencies; no external samples were downloaded.

The engine bank is still the existing Sport2 recording set. Better blending is implemented, but an authentic Formula timbre requires RPM-calibrated load/coast recordings and a listening/tuning pass. `EngineLayer[]` allows that bank to be replaced without rewriting the runtime. The supplied rain bed is available in the bank but is not automatically played merely because asphalt is wet; a future weather state should drive it. No engineer voice-over has been added.

Mixed surface areas within one shared MeshCollider still use that collider’s acoustic classification; dedicated kerb colliders or a surface atlas would improve those transitions. Terrain resolution is 64 by 64 per terrain. Existing physical surface behavior is unchanged.

## Authoring and validation

Editor menu commands under Tools / Formula Career:

1. Integrate Formula Sound Design: assigns bank, components and track metadata; refuses unsaved open scenes; checks collider/rigidbody/transform fingerprints before saving; restores the original scene setup.
2. Validate Formula Sound Design: verifies deterministic audio math, references, nine prefabs and 18 saved circuits without saving them.
3. Play Validate Formula Sound Design: uses an isolated save, runs the real menu/countdown/race flow with RCCP autonomous input, captures actual listener output, tests pause/resume and menu return, then separately tests light impact and scrape using a clearly isolated Play-only cloned-chassis PhysX fixture. It restores editor/play settings on exit.

Reports in this directory record actual outcomes. The listener capture measures clipping in that test only; it does not establish all-scenario loudness, a hardware performance budget or subjective AAA sound quality. Nintendo Switch hardware and a player build must receive their own final audio profiling and listening pass.

## Latest observed result — Unity 6000.3.19f1

Reference/math validation: 42 checks passed. Complete Play validation: passed. The 20-second driving run exercised four gear changes and checked 1,197 completed rendering frames with no HUD/audio frame, RPM or gear mismatch. The recorded listener output has no clipped samples. See `EngineSynchronization.md`, `Validation.json`, `PlayValidation.json` and `EngineSynchronization.csv` for exact scope and evidence.

The light-contact fixture generated a nonzero voice gain through closing speed even though the first collision impulse was zero. Static tests cover the impulse-based path separately. A 3 cm drop begins outside the combined collider contact offsets and retains the normal rest-noise threshold.
