# Formula automatic gearbox and clutch

## Integration

`FormulaTransmission` is authored on the eight Generic livery prefabs and the shared race prefab. It runs after RCCP_CarController and before the RCCP engine, clutch and gearbox physics steps. Automatic forward shifts complete on the physics clock; reverse, neutral, external manual commands, gear input and torque-output handling remain in RCCP. Vendor code, gear ratios, final drive, engine torque curve, wheel friction and suspension settings are unchanged by this feature.

The component controls the public RCCP automatic-shift timer gate because RCCP’s automatic decision methods are private. Each physics step holds that gate closed while Formula makes its own shift decision. The native one-second cooldown is therefore not used by the Formula scheduler. Automatic forward changes use the original configured 80 ms duration and the RCCP public gear/state fields, opening the existing clutch for that interval. Completing on fixed steps avoids the native WaitForSeconds coroutine stretching a shift when rendering stalls. Pending automatic shifts cancel cleanly on ownership release; external manual commands retain their original RCCP API behavior. The cooldown after completion is 180 ms.

## Shift and clutch rules

- The configured shift-up point remains 11,400 RPM on the base car. An upshift requires throttle, at least two grounded wheels, a forward drive state and enough actual road speed to leave the next gear above the downshift band. It does not wait for RCCP’s two independent near-maximum speed gates.
- Normal downshifts occur below 6,500 RPM; braking can request them earlier. Each downshift checks the predicted engine RPM in the lower gear and rejects a mechanical over-rev. Shifts are sequential, with a cooldown to avoid gear hunting.
- The clutch opens during a shift, then closes over a target 60 ms at road speed. Launch engagement uses a separate 220 ms response and low-speed slip/anti-stall behavior. Neutral, forced clutch, handbrake and disabled/kinematic car states disengage drive.
- Engine-to-wheel RPM coupling increases to at least 8 while this controller owns the drivetrain, producing a prompt natural RPM fall after a gear change. This changes coupling, not the RPM reported to the HUD.
- With TCS enabled, a spinning driven axle close to the shift range receives a smoothed engine-torque reduction. It addresses the measured case where driven-wheel speed was much higher than road speed, keeping the engine near redline before a suitable gear could be engaged. It neither rewrites wheel/body velocity nor changes tyre grip. The reduction is inactive with TCS disabled, during braking/shifting, outside the high-RPM range or without sufficient wheel contact.

## Ownership

Manual transmission, public external gear override, external engine RPM override and an already overridden clutch are respected. On release/disable, the original clutch ownership/input, RPM coupling and native timer state are restored. The component does not create a persistent manager or modify saves. Normal Formula upgrades continue to modify torque/brakes/aero independently.

## Validation

Editor commands under Tools / Formula Career:

- Integrate Formula Transmission: adds one component to each of the nine Generic prefab assets.
- Validate Formula Transmission Rules: checks all eight gear decisions, safe downshifts, wheelspin/airborne guards, clutch endpoints and authored prefab coverage.
- Drive Validate Formula Transmission, and its 30/120 FPS variants: two identical stock cars on an isolated flat straight compare original RCCP control with Formula production. Full-throttle acceleration and braking use actual RCCP physics and public input overrides; speed, engine RPM and gear are not seeded during these driving phases. Production must visit 1–8 and 8–1 with exactly seven shifts each way, bounded shift/clutch times and no sustained or repeated high-RPM limiter plateau before top gear.

The later neutral/reverse, external-ownership and airborne checks are separate, labelled, controlled fixtures. All tests use an isolated save and restore editor settings. Reports contain raw physics-step samples for both profiles. A requested frame rate is a validation setting, not a hardware performance guarantee.

No Nintendo Switch hardware run or player build is claimed by the Editor tests. The TCS-off case intentionally retains driver-controlled wheelspin; legitimate wheelspin or top-gear speed limiting must not be confused with a blocked automatic gear change.

## Measured Editor results

All three final driving runs passed in Unity 6000.3.19f1 using the stock Generic, TCS enabled and a 20 ms fixed physics step. Frame rates below are requested render limits, not measured performance claims.

| Requested FPS | Acceleration / braking | Longest automatic shift | Clutch closed after recorded gear change | Longest near-redline episode before eighth gear |
| --- | --- | --- | --- | --- |
| 30 | 1–8 / 8–1, no skipped gears | 80 ms | 40 ms | 20 ms |
| 60 | 1–8 / 8–1, no skipped gears | 80 ms | 40 ms | 0 ms |
| 120 | 1–8 / 8–1, no skipped gears | 80 ms | 40 ms | 20 ms |

Each production run recorded seven upshifts and seven downshifts without hunting. The probe merges near-redline samples separated by up to 100 ms, so brief limiter pulses cannot hide a long plateau. Top gear is excluded from the pre-upshift dwell assertion because no higher gear exists. The original RCCP control recorded merged near-redline episodes up to 1.8 s in these comparisons. Production peak speed at the short eighth-gear checkpoint was about 250 km/h; these are not top-speed tests.

Neutral, actual reverse drive, external override restoration and component disable passed in each run. The separately seeded airborne fixture stayed in its current gear without an automatic upshift. All nine authored prefab assets passed coverage/rule validation. Runtime and Editor assemblies compiled without errors.

Actual menu → four-car race → pause/resume → menu audio regression passed after the transmission change. Its 20-second drive recorded 1176 rendered frames, zero frame/gear/RPM-label mismatches, zero audio/HUD RPM difference and zero pitch-mapping error. Both actual gearbox shift directions were exercised through public commands, and listener DSP captured zero clipped samples. The later chassis-contact fixtures also passed. This checks integration and synchronization, not subjective engine sound quality or speaker latency.
