# Formula menu: authored hierarchy

The new menu is serialized in `Assets/Formula/UI/Formula_Menu.prefab` and instantiated as a connected prefab in both `Assets/Scenes/Menu.unity` and `Assets/Formula/Scenes/Formula_Garage.unity`. Its layout, fonts, textures, references and default visibility exist before Play Mode. Changing a screen does not create or destroy its UI objects.

```text
UI
└── Formula_Menu                     FormulaMenuView, Canvas, scaler, raycaster
    ├── Cinematic frame
    ├── Header                       Brand / Season / Driver and Wallet
    ├── Screen Shade                 Shared page backdrop
    ├── Screens
    │   ├── Home                     FormulaHomeView / Navigation
    │   ├── Career                   FormulaCareerView / Event Cards
    │   ├── Garage                   FormulaGarageView
    │   │   ├── Tabs
    │   │   ├── Dezvoltare            FormulaUpgradeView
    │   │   │   ├── Component Categories / Component Levels
    │   │   │   ├── RCCP Statistics / Level Details
    │   │   │   └── Upgrade Logo / Driver and Wallet / Footer
    │   │   ├── Setup                Setup Cards / RCCP Statistics
    │   │   └── Livree               Livery Details / Livery Cards
    │   ├── Quick Race               FormulaQuickView / Event Cards
    │   ├── Profile                  FormulaProfileView / Pilot Summary / Career Medals
    │   └── Settings                 FormulaSettingsView
    │       ├── Assists
    │       └── Audio and Controls
    │           ├── Tabs
    │           ├── Audio            Volumes / Vibration
    │           └── Controls         FormulaControlsView / Bindings / Waiting for Input
    ├── Footer                       Page Title / Input Hints / Back
    └── Dialogs                      FormulaMenuDialogs
        ├── Confirmation
        ├── Upgrade Level Selection
        └── Race Briefing              Circuit / Objectives / Reward / Start
Presentation
├── Main Camera
├── Formula Paddock Garage
└── Vehicle Preview                  FormulaGarageVehiclePreview
    ├── Editor Garage Vehicle        Visible authoring mesh, EditorOnly
    └── Vehicle Anchor
Scene Systems
├── FormulaMainMenu                  Navigation, binding, transactions, input
├── EventSystem                      Exactly one
├── RCCP_SceneManager
└── PlayerInput
```

SaveManager, SoundManager and RCCp-input retain their own scene roots because their existing lifecycle makes them persistent across scene changes.

## Ownership and states

- `FormulaMainMenu` coordinates navigation and domain commands. It does not construct layout. Button listeners are bound once on initialization, through serialized view references.
- `FormulaMenuView` owns the shared Canvas and screen visibility. Each screen owns its labels and controls; reusable event, development, livery and statistic components own their card presentation. Progression and saves remain authoritative in their existing classes.
- `FormulaGarageVehiclePreview` owns the parked runtime Generic. The editor mesh provides a matching preview before Play Mode. Camera composition is saved in the scene and can be edited normally.
- Menu starts with Home active. Garage starts with Garage / Livree active. Other screens, unused subsections and dialogs are intentionally inactive so they cannot overlap or intercept input. Header, footer and Canvas are active.
- Navigation switches existing objects with `SetActive`. Modal visibility and focus are restored without replacing buttons. TMP may lazily create internal fallback-font submeshes; these are not generated menu screens.
- Audio and bindings use new serialized controls. Bindings target the existing RCCP action asset and `inputRebindsJson`, including keyboard, gamepad and existing wheel mappings. Rebinding supports cancel and timeout, and restores the action's enabled state.

## Editing

Edit the connected prefab under `UI/Formula_Menu` or open the prefab asset. All six screens and the three dialog panels can be inspected without running the game. Temporarily activate the screen being edited and deactivate the others; restore the initial state before saving a scene. UI positions and colors remain editable in ordinary uGUI Inspectors.

`Tools > Formula Career > Author Menu Hierarchy` creates the prefab only if absent, installs it into the two scenes and validates the result. It preserves an existing prefab's authored layout. `Validate Authored Menu Hierarchy` checks the serialized scenes. Pre-migration scene backups are in `Docs/AI/FormulaUI/SceneMigration/`; the old Canvas and its presenters are absent from the current scenes. Reusable legacy assets used elsewhere in the project are not deleted.

## Evidence

`RuntimeValidation.json`: current menu acceptance checks, including explicit preview/equip, purchase and level selection, live RCCP statistics, new audio and interactive rebinding, stable authored object identities, virtual gamepad, and scene reload. `../FormulaGarage/RuntimeValidation.json`: 6 garage checks. `AuthoredHierarchy.txt`: saved scene state. `RuntimeHierarchyDelta.json`: no authored objects removed; only TMP-managed fallback-font submeshes added.

This change does not validate a new target-platform build or resolve the existing full-lap validity issue.

## Presentation revision

`Refine Formula UX and UI` updates the existing prefab controls in place, generates native circuit diagrams from this game’s waypoint routes, updates the garage stage and rebakes its reflection probe. Home exposes one Career entry to the six-event overview. Upgrade displays Stock plus three levels with current and preview development ticks and actual installed RCCP values. Structured briefing shows career medal targets or free-session format facts, with actual available rewards. Camera shots are owned by the preview component; page entrances preserve the serialized screen objects. Teko titles use overflow because the font’s line metrics exceed compact capital-height slots. The acceptance harness checks visible title glyphs before each capture.

Current before/after captures are in `BeforePremium/` and this directory. `PresentationRevision.json` records the revision and its remaining art/build limitations.

## Primary language

English is selected first by the existing Unity Localization Settings startup selectors. The existing UI English table and other locales are preserved. Formula runtime copy and authored TMP/uGUI labels are English. Internal hierarchy/navigation keys and save/catalog IDs remain stable; they are not player-facing labels. Text migration can be repeated with Tools > Formula Career > Apply English Text without rebuilding layouts.

Career now follows Figma frame `174:37` with the original red selected variant `184:275`. `FormulaCareerView` owns its dedicated header/footer and profile labels; `FormulaEventCard` keeps the existing event IDs and opens the existing briefing. Its presentation suppresses shared chrome only while Career is active. The authoring command `Match Figma Career` saves all layers into the menu prefab. Layer exports, original geometry and the permitted 2027 / circuit-name / calendar adaptations are recorded in `FigmaCareer/Implementation.json`.

Upgrade follows Figma frame `204:767`. `FormulaUpgradeView` owns the four categories, level rows, details and its own chrome; `FormulaUpgradeOption` previews the focused level without changing progress. `FormulaMainMenu` retains purchase confirmation and save transactions. The prior development-card objects are removed from the prefab during explicit authoring. The normal Garage Setup/Liveries sections retain their existing controls. `Match Figma Upgrade` can be repeated; `Refine Formula UX and UI` reapplies the Upgrade styling when the page exists. Source layers and adaptations are in `FigmaUpgrade/Implementation.json`.

## Unified Garage and Upgrade style

Home has three primary actions (Career, Garage, Quick Race) and two secondary actions (Profile, Settings). Garage is the single car-management entry and retains its last tab in the additive `FormulaProgress.lastGarageTab` field. Old profiles default to Upgrade. The three permanent tabs are Upgrade, Setup and Liveries; bumpers/Q/E always switch these tabs, while directional navigation selects components or levels.

The event briefing includes Garage. Its presenter retains the source event, mode and card focus until Back reopens that briefing. Stock and Time Attack rules are explicitly shown inside contextual Garage. Setup focus previews downforce and drag in the existing statistics view without changing the equipped setup; confirmation applies the free preset. Livery previews and upgrade purchases retain their existing ownership and save rules.

`FormulaMenuUnifiedBuilder` edits the current prefab in place. It reuses the exported Upgrade/Career header, wallet, matte currency and footer art. `FormulaPanelGraphic` renders crisp gradient surfaces and hairlines; `FormulaMenuFocus` supplies the common red selection. Menu geometry and references are authored before Play Mode. The result screen is now an authored `FormulaResults.prefab`, owned by `FormulaResultsView`, populated from the saved receipt and instantiated once on finish. It gives Continue career focus when available and handles the standard UI Back action.

Author with `Tools > Formula Career > Unify Garage UX and Upgrade Style`. The older Refine command recognizes the applied theme, and the Figma Upgrade/Career authoring commands preserve the unified Garage conventions. Details and current evidence are recorded in `UnifiedGarage/Implementation.json`. The gameplay HUD and driving rules are outside this menu presentation revision.

Current acceptance: 23 menu/garage checks and 3 English HUD/result-flow checks passed with no errors. Both harnesses use temporary saves. The result test loads the real circuit and scripts completion; it does not validate driving or a platform build.
