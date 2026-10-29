# Nintendo Switch presentation

Switch builds now use Nintendo controller terminology throughout the authored menu, controls, rebind prompts, back prompts and race HUD. The controls page identifies **Joy-Con / Pro Controller** and omits the device-family selector. Keyboard and steering-wheel pages cannot be opened through hidden callbacks. Physical buttons are shown as A/B/X/Y, L/R, ZL/ZR, +/− and localized stick names, without depending on a controller being connected or on Input System's generic display names.

`FormulaPlatform` owns the presentation policy. `UNITY_SWITCH` selects it unconditionally in a Switch build; the Editor-only preview flag exists to exercise that same policy without installing a Nintendo SDK. Desktop builds keep their existing input hints, device selection, display and graphics preferences.

## UI and input

- A confirms and B goes back on Switch. The existing UI actions use `*/{Submit}` / `*/{Cancel}` usage bindings, so these are explicitly overridden to the Nintendo physical face-button positions. The previous literal-Gamepad-only remapping did not affect those bindings.
- Menu prompts remain Nintendo prompts at startup and without a connected pad. Keyboard/mouse activity cannot change Switch prompt style.
- Controls and conflict/rebind dialogs use a physical-path formatter instead of Xbox-style generic names such as LT/RT. Rebinding can be cancelled with B; normal saved bindings still use the existing RCCP owner.
- The legacy binding display used by the username screen chooses Gamepad bindings or UI usage bindings on Switch, never its keyboard fallback. The shared RCCP icon selector chooses the existing Switch sprite set even when the caller supplies a generic or foreign gamepad layout name.
- The race HUD shows + for pause. The existing authored pause options expose audio controls only.
- Eight small copy entries were added to each of the 12 existing locales. New CJK glyphs were prewarmed in existing font atlases. Existing locale/font profiles, controller sprites and input-action asset files were preserved.

## Fixed graphics configuration

Switch hides the Display and Graphics pages, their tabs and overview shortcuts, and the fullscreen, frame-limit, quality and v-sync controls. Their callbacks are also guarded. Previously saved video preferences cannot change the Switch build's quality, v-sync or display configuration. The frame-rate target is fixed at 60 FPS as requested, applied on entering the menu and a race; this is a target, not a measured hardware frame rate.

Safe-area adjustment remains available under Accessibility alongside text size, reduced motion and high contrast. Audio, vibration intensity, assists and language selection remain available. The shared menu prefab and build/profile graphics assets were not modified.

## Validation

The isolated-save Editor preview passed:

- Nintendo startup prompts, explicit A/B UI bindings, gamepad-only controls, all binding pages, rebind/cancel prompts, hidden callback guards and ignoring saved video overrides.
- Visible text/source checks across all 12 locales, with six representative control-page screenshots and an accessibility screenshot.
- A fresh desktop menu instance retaining keyboard/gamepad/wheel choices and all video tabs.
- A real Time Attack launch with + on the HUD and inspection of the pause/audio menus, followed by returning to the menu.
- No runtime errors or missing-glyph warnings during the final complete run. Earlier test-harness failures and the generic-usage binding finding are retained under `InitialChecks/`.

`PlayValidation.json` records the individual checks and `UIBindings.json` records the effective A/B bindings. The final compilation also checks the runtime sources with both the host Editor defines and `UNITY_SWITCH` plus `UNITY_EDITOR`. Native Nintendo SDK branches and a Switch player build are not covered: this Mac has no Switch module/SDK. Confirm the final button glyphs and behaviour on Joy-Con and Pro Controller in the next Windows Switch build.

The one-time execution trigger was removed. Opening the project does not start tests or update localization data automatically. Manual tools are under `Tools/Formula Career/Switch/`.
