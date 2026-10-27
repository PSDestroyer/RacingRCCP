# Nintendo profile nickname

Implemented 2026-10-27. On Switch players, SaveManager.Load initializes the existing Nintendo save integration, loads the selected user's save, and reads that same account's nickname using NintendoManager.TryGetNickname. The API call follows the existing Account.GetNickname(ref Nickname, Uid) usage in NintendoSave.

A successful, nonblank nickname replaces PlayerName in memory for both new and existing profiles. Existing menu/profile/standings consumers read that field. A fresh profile with a resolved nickname goes directly to Menu through the existing Init.Start flow. A failed or blank nickname preserves the loaded name, or the existing manual-name setup for a new profile. Desktop and Editor retain manual names. No new serialized fields, per-frame polling, save calls, packages, scene or prefab edits are introduced by this feature. The next normal save persists PlayerName; each launch queries the system profile again.

Account and save initialization are now idempotent. SaveManager can safely initialize before Init.Awake, and a later Init.Awake does not reopen the user or remount their save.

## Validation

- Unity 6000.3.19f1 on macOS imported/recompiled the changes without new compilation errors.
- A standalone C# harness compiled the actual NintendoManager.cs, NintendoSave.cs, SaveManager.cs and Init.cs with UNITY_SWITCH, using simulated account/filesystem/Unity dependencies and the project's actual Newtonsoft.Json library. Six scenarios passed: existing save, new save, failed nickname lookup, blank nickname, new save with failed lookup, and nickname change on reload. Checks included Unicode preservation, selected account/save owner consistency, one initialization/mount despite repeated startup, correct startup scene, and preservation of money/XP/settings/unknown JSON fields.
- The same sources compiled without UNITY_SWITCH; the desktop scenario preserved the manual name and made no Nintendo API calls.
- Harness files are in /private/tmp/racing-nickname-validation for this session.

These simulations validate branch logic, not the Nintendo SDK ABI or hardware. Only MacStandaloneSupport is installed on this host. A real Switch SDK build and console profile read remain untested.
