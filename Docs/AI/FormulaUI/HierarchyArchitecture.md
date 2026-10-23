# Formula menu: authored hierarchy

The new menu is serialized in `Assets/Formula/UI/Formula_Menu.prefab` and instantiated as a connected prefab in both `Assets/Scenes/Menu.unity` and `Assets/Formula/Scenes/Formula_Garage.unity`. Its layout, fonts, textures, references and default visibility exist before Play Mode. Changing a screen does not create or destroy its UI objects.

```text
UI
└── Formula_Menu                     FormulaMenuView, Canvas, scaler, raycaster
    ├── Cinematic frame
    ├── Header                       Brand / Season / Driver and Wallet
    ├── Screen Shade                 Shared page backdrop
    ├── Screens
    │   ├── Home                     FormulaHomeView / Navigation / Current Vehicle
    │   ├── Career                   FormulaCareerView / Event Cards
    │   ├── Garage                   FormulaGarageView
    │   │   ├── Tabs
    │   │   ├── Dezvoltare            Upgrade Cards / RCCP Statistics
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

`RuntimeValidation.json`: 16 current menu checks, including explicit preview/equip, purchase and level selection, live RCCP statistics, new audio and interactive rebinding, stable authored object identities, virtual gamepad, and scene reload. `../FormulaGarage/RuntimeValidation.json`: 6 garage checks. `AuthoredHierarchy.txt`: saved scene state. `RuntimeHierarchyDelta.json`: no authored objects removed; only TMP-managed fallback-font submeshes added.

This change does not validate a new target-platform build or resolve the existing full-lap validity issue.

## Presentation revision

`Refine Formula UX and UI` updates the existing prefab controls in place, generates native circuit diagrams from this game’s waypoint routes, updates the garage stage and rebakes its reflection probe. Home exposes both Continue and the six-event career overview. Development compares current and next-level RCCP values. Structured briefing shows career medal targets or free-session format facts, with actual available rewards. Camera shots are owned by the preview component; page entrances preserve the serialized screen objects. Teko titles use overflow because the font’s line metrics exceed compact capital-height slots. The acceptance harness checks visible title glyphs before each capture.

Current before/after captures are in `BeforePremium/` and this directory. `PresentationRevision.json` records the revision and its remaining art/build limitations.

## Primary language

English is selected first by the existing Unity Localization Settings startup selectors. The existing UI English table and other locales are preserved. Formula runtime copy and authored TMP/uGUI labels are English. Internal hierarchy/navigation keys and save/catalog IDs remain stable; they are not player-facing labels. Text migration can be repeated with Tools > Formula Career > Apply English Text without rebuilding layouts.
