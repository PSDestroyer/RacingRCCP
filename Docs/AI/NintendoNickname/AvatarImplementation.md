# Nintendo profile photo — removed

Nintendo account photo loading was removed at the user's request on 2026-10-28 after the Switch build reported CS1501 for the profile-image SDK call.

`NintendoManager` contains no photo API lookup, reflection, image buffer, image decode, cached profile texture, or photo shutdown callback. The existing account initialization, selected user ID and nickname read remain intact.

`FormulaPlayerAvatar.OnEnable` now presents the normal fallback on every platform. Existing serialized UI components and their generic texture presenter are retained to avoid missing prefab references; production code never supplies an account photo. No scene/prefab migration or saved-profile change is required.

The previous avatar preview and validation reports describe the retired implementation and its synthetic test image. They do not validate the current Nintendo integration. A full Switch build must run on the user's Windows machine with its installed Unity Switch module and Nintendo SDK.
