# UI motion integration — 2026-10-30

Seven shared presentation behaviors are integrated into the existing runtime UI:

1. Controller/pointer focus grows controls by 1.2%; submit/pointer press compresses them. Existing immediate red selection remains readable. Slider rows retain their dedicated selection treatment.
2. Main pages and settings subpages fade and rise 12 UI units over 200 ms. Existing settings, garage and upgrade indicators glide for 180 ms. Career/profile paging also reveals the changed content.
3. Confirmation, purchase/equip and event dialogs enter with a 160 ms fade/scale and close over 100 ms. Logical menu ownership and raycasts release immediately on menu-dialog dismissal. Reopening cancels the previous exit.
4. Upgrade levels enter in a short stagger; existing stat segments tint to their new targets. Actual purchases/equipment changes pulse their existing status and marker. Presentation never repeats a transaction.
5. Audio/vibration percentages pulse on adjustment. Toggle buttons receive the common press feedback. Values update immediately through their existing owners.
6. Race results reveal medal, metrics, progression, bonus and livery in sequence. Credits/XP/bonus count for 550 ms after a 120 ms lead-in. Confirmed next/retry/menu actions finish the presentation and execute on the same press. Saved results and receipt data remain authoritative.
7. Pause, audio settings and pause confirmations use unscaled motion; the existing PauseGameplay controller owns actual pause/resume. Resume waits for the short exit. Race notices use the existing fade plus a small vertical reveal and title pulse.

`FormulaUIMotion` is shared, replaces in-flight goals, restores authored poses on disable, compensates pivots for centered scaling, and runs one unscaled coroutine per active target only while animating. Reduced Motion completes active motion and presents subsequent transitions immediately. No new renderers, textures, materials, canvases or additional rendering passes are authored for the animations. Animated transforms/graphics can still cause UI rebuild work; device performance must be measured on hardware.

The Switch target remains 60 FPS and video settings remain hidden. No additional controller names, translation keys, SDK APIs or packages are introduced.

## Verification

- Unity 6000.3.19f1 Play Mode: `Validation.json`, status passed, phases 0–19, no runtime errors. Fixture save isolated under the OS temporary directory.
- Real UI events, 20 interrupted modal cycles, actual fixture upgrade/livery purchases, immediate slider values, skippable reward counters, profile data unchanged by presentation, Reduced Motion, paused audio/settings/resume and race notice reveal.
- Existing menu CanvasRenderer count unchanged, excluding TMP-generated fallback submeshes, which may appear as additional text becomes visible.
- Runtime and Editor C# compilation succeeded. A separate UNITY_SWITCH + UNITY_EDITOR compilation succeeded; native Nintendo SDK branches and a Switch player build were not validated on this Mac.
- `UI-Motion-Preview.gif` contains genuine Editor test captures, resized for review. Its reward receipt is a presentation fixture. `Results.png` shows the completed fixture presentation.
- `InitialChecks` retains diagnostic failures from development, corrected before the final passing run.

Repeat the isolated Editor check from **Tools → Formula Career → UI → Play Validate All UI Motion**. The temporary automatic trigger was removed after validation.
