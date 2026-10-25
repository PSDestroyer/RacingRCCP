# Power-on cornering diagnosis — 2026-10-26

User reproduction: around 100 km/h, steering while keeping the accelerator pressed causes uncontrollable drift. The diagnosis below led to the production correction described at the end of this document.

## Confirmed findings

The current stock car can spin under sustained acceleration and steering. The previous acceptance matrix used short turns at 25% throttle and was insufficient to establish controllability in this situation.

`PowerCornerDiagnosis.json` compares nine runtime-only treatments. Rear slip builds before ESP braking, all four wheels stay grounded, and the selected Race behavior has drift mode disabled. At 1.1 s the baseline already has 5.58 degrees of body sideslip with no ESP braking or TCS intervention. ESP reacts afterward. The first sampled TCS intervention is around 30 degrees of body sideslip. Turning ESP off does not remove the initial loss. Half steering, a 25% smaller steering curve and a fully open limited-slip setting do not prevent the spin. Zero throttle remains much more stable.

That first probe seeds chassis speed at 100 km/h while wheel/engine RPM starts low, causing a temporary speed drop. Its gear-2 discriminator downshifts before the turn, so it does not isolate gear choice. The natural-launch follow-up below removes these limitations.

The current RCCP TCS monitors forward wheel slip with a 0.35 engagement threshold, then subtracts torque proportional to slip. It does not directly regulate lateral loss of the rear axle. Countersteering assistance is multiplied by `1 - abs(steerInput)`, making it zero at full driver steering. The current 100 km/h steering lock is 6.72 degrees. These explain why existing assistance and smoother rack travel are insufficient under sustained power.

## Natural-launch comparison

`PowerCornerTractionDiagnosis.json`: eight independent stock cars accelerate normally from rest. Each starts its own measurement at 100 km/h, requests full steering from 0.5 to 4.5 s, then releases steering; measurement lasts 6 s. Automatic gears and the production steering curve remain active. Measured render rate was approximately 60 FPS, physics 50 Hz. No pose/velocity injection or added stabilizing forces. All profiles retained four grounded wheels; no recorded errors.

| Runtime treatment | Peak body sideslip | Final speed |
| --- | ---: | ---: |
| Current production | 180.0° | 4.9 km/h |
| TCS threshold lowered to 0.12 | 180.0° | 2.0 km/h |
| Rear grip +25% | 10.18° | 169.8 km/h |
| Throttle capped at 55% during steering | 180.0° | 21.4 km/h |
| Throttle capped at 35% during steering | 59.62° | 74.1 km/h |
| Adaptive throttle from rear forward slip | 11.01° | 114.2 km/h |
| TCS off | 180.0° | 4.3 km/h |
| Adaptive throttle plus body-sideslip guard | 8.66° | 95.2 km/h |

These are diagnostic results, not release acceptance. The detailed report records actual throttle, slip, wheel torque/braking, gearing and control scales. The adaptive body guard starts reducing requested power between 3 and 7 degrees of body sideslip, attacks quickly and recovers gradually. It sometimes cuts requested throttle completely. It does not prove the same timing or feel for a future engine-torque implementation.

The prototype has a material recovery limitation: at 6 s, after steering is released and body sideslip is nearly zero, the rear-slip loop still requests only about 26% throttle. Launch to 100 km/h was explicitly exempted, so this experiment does not establish preserved straight-line acceleration. A production controller must restore available power once lateral demand and sideslip subside. The rear-grip treatment also changes the natural 0–100 launch from 4.84 to 3.88 s, demonstrating a wider performance/balance impact.

## Recommended correction

Implement Formula-specific traction regulation that observes longitudinal and lateral rear-axle slip in the physics update, reduces positive drive torque symmetrically before the slide becomes large, and restores it smoothly. Honor the player's TCS setting and leave AI/external control ownership intact. The existing `RCCP_Engine.Multiply()` API can apply temporary torque scaling without changing permanent engine-upgrade values or vendor code. Test that implementation rather than assuming it is equivalent to this throttle prototype.

Review the front/rear grip balance as a separate tuning axis. The +25% rear-grip treatment prevents the spin here, but can introduce understeer and should not be adopted from one maneuver alone. Simply increasing TCS intensity, reducing steering range, or globally reducing engine power is not supported as the complete fix.

Before production acceptance: natural acceleration into both left and right turns around 80–120 km/h, partial/full inputs, release/recovery, stock/max upgrades, assist settings, keyboard/gamepad, multiple frame rates and actual circuit surfaces. Baseline controllability must be available without upgrades.

## Production correction

`FormulaTractionControl` runs after RCCP's controller input copy and before its engine torque generation. While the player accelerates forward with TCS enabled, it samples raw rear WheelCollider contacts once per physics step. During cornering, a rear-wheel slip regulator reduces temporary drive torque; a 3–7 degree body-sideslip guard catches lateral loss early. It uses `RCCP_Engine.Multiply()` so permanent engine power, upgrade baselines, other engine multipliers, input bindings, steering range and tyre grip retain their existing ownership.

When the rack is near centre and body sideslip is below 2 degrees, the rear regulator restores power at 2 scale units per second regardless of its longitudinal-slip deadband. This fixes the prototype's lingering reduction after the turn. TCS off, AI/external control, reverse/neutral/park, no throttle, handbrake, low speed, airborne rear axle, disabled component and stopped/disabled engine reset the component's local state. The engine's own multiplier is never overwritten during cleanup. A stopped engine is not multiplied because RCCP does not consume/reset its multiplier in that state.

All eight Generic livery prefabs and the resource-loaded Generic prefab contain the component. Both the builder and handling authoring command install it. Vendor RCCP files and progression formulas are unchanged by this traction correction.

### Natural-launch acceptance

`TractionValidation.json` runs the actual production component with the accelerator request held at 1. Twelve cases cover 80/100/120 km/h entry, left/right steering, and stock/L3 development. Vehicles accelerate naturally; there are no injected velocities, experimental throttle reductions, or stabilizing forces. Full steering lasts four seconds, followed by 4.5 seconds of recovery. A thirteenth stock car disables only the new component for comparison.

The initial 60 FPS run passed all twelve cases: peak body slip 7.99–9.40 degrees, four grounded wheels throughout, full final torque scale, and unchanged authored maximum torque. The disabled-component comparison reached 180 degrees. Stock 0–100 was 4.74 seconds with and without the component; launch scale remained 1 for every production case. After steering release at 4.5 seconds, body/rack alignment occurred around 5.2 seconds and full available power returned by 5.5–5.6 seconds. These results validate the engine-torque implementation independently of the earlier throttle prototype.

Additional acceptance reports, all with zero recorded errors:

- `TractionValidation-30fps.json`: all twelve cases passed at measured 29.89–30.11 FPS; maximum body slip 9.35 degrees.
- `TractionValidation-120fps.json`: all twelve cases passed with a 120 FPS target and measured 117.67–118.67 FPS; maximum body slip 9.21 degrees. Physics remained at 50 Hz in all runs.
- `TractionValidation-targeted.json`: half steering with full throttle, and full steering with throttle lifted from 2.0–2.7 seconds then reapplied, each at L0/L3. All four passed; half-steer peak slip 5.71/5.77 degrees, lift/reapply peak 8.85/8.73 degrees, final torque scale 1.
- `TractionValidation-ownership.json`: six cases first demonstrated active intervention (scale about 0.78), then independently disabled TCS, set AI/external ownership, set external input override, stopped the engine, disabled the component, or disabled player control. All restored local scale to exactly 1 on the next physics step and held it for 0.2 seconds without changing authored engine torque. The AI case exercises its ownership flag, not navigation.

The additional driving runs enforce a continuous final 0.3 seconds of aligned motion and at least 95% available torque, and reject changed engine maximum torque or non-finite measurements. This avoids accepting a transient power recovery followed by renewed suppression. Straight launch scale must remain at least 0.999. Intentional TCS-off driving retains RCCP's existing behavior; this correction is part of the enabled TCS assistance.

### Actual circuit and input integration

After the traction correction, both existing stock G01 circuit tests passed again, with a valid career lap, normal checkpoints/results and AI takeover. `KeyboardLap.json` completed in 93.29 seconds with peak slip 5.38 degrees; `GamepadLap.json` completed in 94.14 seconds with peak slip 4.73 degrees. Virtual devices exercise the real keyboard and analog input-action bindings. The gamepad run records 4090 fractional-steering and 2502 fractional-trigger samples. The unchanged route followers include full-throttle cornering on the actual circuit: 147 keyboard and 72 gamepad trace samples. Torque protection was observed on the circuit and its scale returned to 1 after post-finish AI takeover. Both reports have zero errors and continuous application focus.

Final evidence comprises 40 focused handling cases, six control-ownership checks and two valid stock circuit laps. A read-only audit also confirmed the initial 60 FPS report's last 3.4–3.6 seconds of sampled motion remain aligned at full protection scale. Physical controller/wheel feel and standalone PC/Switch builds were not validated. No additional Inspector setup is required for the nine authored Generic prefabs.
