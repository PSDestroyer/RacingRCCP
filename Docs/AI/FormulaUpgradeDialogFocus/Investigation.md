# Upgrade dialog controller focus — 2026-10-31

Reported: with no upgrades owned, select Level 3, then its action to view Level 1. The replacement dialog has no usable controller selection/navigation.

Confirmed cause: the prerequisite action closes and reopens the same dialog in one callback. Animated Hide immediately sets its CanvasGroup.interactable to false. Confirm previously called Wire and FormulaUI.Focus before Enter restored that group. IsInteractable therefore excluded both buttons and the existing selection remained on the disabled garage underneath. Waiting for the visual entry could not rebuild the lost navigation.

Fix: in confirmation, level selection and event briefing, call Enter before building navigation or assigning selection. This restores interaction synchronously and cancels the previous hide; no delayed focus workaround, animation removal, input remapping, economy or save changes.

Evidence:

- `Before.json`: Unity Play Mode regression failed on the original source at “Level 3 to Level 1 redirect lost controller focus (Reduced Motion=False)”. The fixture owned no engine upgrades, had Level 1 unlocked and enough credits to expose the purchase dialog.
- `After.json`: same regression plus full motion suite passed, phases 0–19, zero runtime errors, batch exit code 0. Eight rapid redirects with/without Reduced Motion verify a live selection inside the dialog, reciprocal Back/Buy navigation, actual uGUI Move/Submit events, dismissal, and unchanged ownership/credits while viewing.
- Nearby checks: interrupted/reopened dialogs, sliders, actual isolated-save purchases/equipment, reward presentation, pause/audio/resume and notifications.

Unity 6000.3.19f1, macOS Editor batch with graphics enabled, real Menu scene and isolated temporary save. No physical Nintendo controller or Switch player build was used. Screenshots/GIF from the earlier motion review were retained; this batch run intentionally captures no frames.

Re-run in Editor with Tools → Formula Career → UI → Play Validate All UI Motion, or launch Unity with `-batchmode -projectPath <project> -executeMethod FormulaUIMotionValidation.RunBatch -logFile <log>` (omit `-quit`; the runner exits after Play Mode finishes).
