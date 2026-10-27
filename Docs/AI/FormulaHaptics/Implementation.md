# Controller vibration

Implemented 2026-10-27. Every Formula race configures one `FormulaControllerHaptics` on its existing GamePlayManager through `FormulaSession.Configure`. The existing Formula menu prefab includes the saved intensity slider; no race scene, action binding, package or project setting changes are required.

## Feedback

The component observes only the player's actual RCCP car. It combines subtle acceleration/drive load and lateral load with stronger braking, raw wheel slip, suspension force rises, kerb/gravel/grass feedback, a short gear-change pulse, and collision impulses. Wheel contacts are read directly to avoid advancing RCCP's filtered slip getters. Acceleration is derived from actual Rigidbody velocity, not controller stick values. This does not change physics or handling.

Settings > Audio includes Vibration intensity from 0–100%, in 1% steps, defaulting to 65% to preserve the original feel. The D-pad or stick adjusts it horizontally; vertical navigation moves between the volume sliders, Vibration On/Off and intensity. The separate On/Off option retains the chosen intensity. The selected value applies live and is saved when leaving the settings, using the existing deferred audio preference save path instead of writing to disk on every slider movement.

`SaveData.vibrationIntensity` stores the normalized value. Old JSON saves with no field retain the 0.65 initializer, out-of-range values are clamped, and non-finite values recover the default. At 0%, both motors stop immediately. Outputs are clamped to 0–1 and additionally capped at 0.65 low-frequency and 0.75 high-frequency motor amplitude. The existing saved `vibrationsState` option is also read live.

## Device and lifecycle ownership

The existing FormulaInputHints active-input policy selects Gamepad.current only when the player is using a gamepad. Switching to keyboard/mouse or another gamepad stops the previous motors. Only the owned pad is commanded; unrelated devices are not globally reset.

Pause, focus loss, application suspension, disabled/disconnected devices, component disable, race results, leaving a race and quitting stop the motors. Normal gameplay must still allow player control; external/AI input takeover suppresses feedback. Pending impulses and telemetry history are cleared on stop to avoid replay on resume. Event subscriptions are removed on disable.

## Cost

One observer per race, with cached player/wheel references and wheel-history buffers allocated only when the player vehicle changes. Telemetry and ordinary motor output are limited to 20 Hz, with immediate safety stops and a 0.5-second refresh for sustained signals. It adds no renderers, textures, effects or draw calls. No per-frame collections, LINQ or material allocations were introduced.

## Validation

Use Tools > Formula Career > Validate Controller Vibration. The test uses the actual Formula_Circuit_01 race with RCCP input overrides and real physics. Two virtual Gamepad subclasses capture SetMotorSpeeds calls instead of actuating physical hardware. Saves are redirected to a temporary path; previous test environment, background behavior, play-start scene and current gamepad are restored.

Validation.json records a passing run in Unity 6000.3.19f1 on macOS, with no runtime errors. It verifies:

- Actual moving-player acceleration reaches the normal Gamepad API.
- Changing saved intensity to zero stops both motors, and raising it resumes feedback while driving.
- Both channels stop at pause, Vibration Off, keyboard takeover, focus loss, component disable and menu state.
- Switching active pads stops the old device and drives the new one.
- A real runtime barrier collision generates a new impact pulse through RCCP collision events.
- A device-disable notification stops output.
- All observed amplitudes remain in [0,1].

Tools > Formula Career > Validate Vibration Intensity Setting checks the actual authored settings UI with a virtual gamepad, both intensity boundaries, On/Off retention, old-save compatibility, disk persistence/reload, and label fit at 115% text size. It uses an isolated temporary save and restores the previous environment. See SettingsValidation.json and VibrationSettings.png. The authoring command Add Vibration Intensity Setting updates the prefab, and the full menu builders retain the same setting when regenerated.

No physical controller feel, hardware vibration support or Nintendo Switch build/device was tested. This host has only MacStandaloneSupport and no Nintendo Input System support package. The standard Gamepad API supports console rumble when the corresponding official console-specific Input System package is installed; refer to [Unity's gamepad haptics documentation](https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/gamepad-haptics.md). Packages were not installed or changed. Verify actual controller/backend support and tune feel on the intended hardware before release.
