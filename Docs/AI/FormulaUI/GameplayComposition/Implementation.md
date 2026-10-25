# Compact gameplay UI composition

The existing authored Race and Results prefabs were refined in place. Their GUIDs are preserved. All 18 Formula circuit scenes resolve to the same race presentation. No competing runtime hierarchy builder or new package was added.

## HUD

The session panel is 240×104, timing 272×102/128, Career target 240×68, minimap 240×160, and vehicle instruments 272×116 in the existing 1280×720 design space. The minimap and vehicle instruments retain the lower 688 baseline. Quick Race standings move directly below the session panel; Career retains its exact target above standings. Pause keeps the full goal and recovery information.

Opaque panel rectangles in ordinary Quick Race, including four standings backgrounds, total 154,688 design pixels instead of 217,040 (28.7% less). This compares authored rectangles and excludes temporary notices, pause control and conditional chevrons. It is not a GPU profiling claim.

The timing panel retains distinct lap and race clocks and measured checkpoint comparisons. The empty-reference state reads FIRST TIMED LAP. Career Time Attack uses TIME TO BEAT with the same Bronze reference multiplier as the scoring system, with the full or-faster requirement in Pause. Career does not repeat that target in an introductory toast. Free-session intro messages last three seconds.

Proximity warnings use two outlined yellow chevrons on each side, without text or a solid rectangular backing. Detection remains based on actual rival positions with the previous elevation and lateral/longitudinal thresholds and 0.4-second release hold. This is not a fabricated race-time gap.

Formula default opponents receive fictional display names in the existing racer initialization and spawn paths. Custom authored names are preserved; non-Formula defaults remain unchanged.

## Outcomes

The outcome title leads. Passed career results emphasize actual credits and XP; failure emphasizes its explanation and Retry. Zero credits/XP panels, missing lap metrics and unchanged XP progression are hidden. A missed target can still earn a valid-finish reward; those nonzero values remain visible. Free-session success emphasizes session/lap performance. Result saved replaces a misleading generic progression claim. Livery unlocks and level bonuses retain separate space.

The existing result view owns layout switching and focus. Repeated presentation restores panels and button widths correctly. Metric captions and the progression container are serialized references authored by the existing builders. Existing lap limits, receipt calculation, reward rules and save format were not modified.

## Validation

Both final Editor runs passed 21 checks (42 total) at 1280×720 and 1920×1080. Real keyboard driving and virtual gamepad pause/resume were exercised. HUD and outcome variants were checked at 115% text and 95% safe area. Temporary save paths isolate QA rewards from the player profile.

Visual inspection caught a TMP Ellipsis issue: increasing text size could hide standings names because the line metrics exceeded the original 26-pixel text rectangle. The visible row background remains 26 pixels; the text rectangle now reserves 36 pixels around the same center. Checks now require active nonempty labels to produce visible glyphs, as well as fit their slots. Both final resolutions were rerun after this correction and their captures reviewed.

RPM, waypoint splits, nearby-rival placement, career finish receipts and result variants are deliberately scripted boundaries. Free-result and livery-reward screenshots are presentation fixtures; their mock awards are not committed. These runs do not claim natural race completion, a standalone player build or physical Switch validation.

See Validation.json and IntegrationAudit.json for evidence and source snapshots. Previous UI revisions remain in ../GameplayUX and ../Gameplay.
