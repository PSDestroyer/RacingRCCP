# Formula 2027 visual presentation upgrade

Applied 30 October 2026 in Unity 6000.3.19f1. These are saved gameplay changes, also used by the marketing captures.

## Saved changes

- All 18 Formula circuit scenes use authored daylight, winter, wet, night or sunset lighting, restrained distance fog, native ACES grading and subtle bloom. Each mode has a shared saved VolumeProfile in `Assets/Formula/Presentation/Grades`.
- Circuit cameras use a closer chase position (6.3 m distance, 1.55 m height), speed-dependent 48–62 degree FOV and the existing camera shake. These settings are saved in the scenes. The capture utility uses them without a special photo camera.
- All eight liveries use native URP Lit with dielectric satin paint, reduced clear-coat glare, matte rubber, darker woven carbon and brushed rims. Original livery artwork and stable livery IDs are retained. Wetness has separate controlled limits per surface. Rear LEDs remain emissive; the red point-light spill and lens flare are disabled in all nine vehicle prefabs. Garage material copies and livery thumbnails are refreshed.
- Dry asphalt smoothness is reduced to .32, while the wet material remains distinct at .82 with its existing surface maps.
- Trees, grass, crowds and decorative tyres use Formula-owned material copies with a more consistent palette. Three in-project F27/APEX signs replace the selected generic billboards and flags. Imported vendor materials are preserved.
- The existing race HUD prefab has smaller translucent panels, a compact minimap and speed/gear cluster, and restrained red rails. The main panel rectangle area is 28.4% smaller at the 1280×720 reference layout; this is a layout measurement, not a GPU performance measurement. Existing telemetry, pause, minimap, localization, safe-area and navigation references are retained. High contrast uses nearly opaque panels.
- Authoring utilities apply the same finish and HUD recipe when those assets are rebuilt.

## Verification

`AssetValidation.json` verifies all eight paint materials, shader compilation, HUD references, all 18 scene grades/cameras and route/road references. `PhysicsPreservation.json` compares the scene physics component YAML and mesh references against the pre-upgrade baseline: all 18 pass unchanged.

`HudPlayValidation.json` records a real race at native 1280×720, checks visible critical HUD glyphs and overflow with default settings and with text scale 115%, safe area 95% and high contrast. It also verifies actual pause focus and resume through a virtual gamepad's physical East button (Nintendo A in the game's preview mode). Screenshots are included beside the report. No runtime errors were recorded in the passing run.

Marketing PNGs are native 1920×1080 live Unity captures with the actual racing HUD. The utility uses isolated save data and real AI inputs/vehicle physics; no vehicle position/velocity changes, UI text replacement, compositing or image retouching. The contact sheet is a separate review layout. Each selected source, speed, camera and checksum is recorded in the marketing manifest.

The final selection contains eight images from 108 valid candidates (78 chase/onboard and 30 look-back captures). Both final capture sessions completed without runtime errors. All eight selected PNGs match their source checksums, and the ZIP passed its integrity check. Deliverables are in `Marketing/Screenshots/2026-10-30/Selected`, with `Contact-Sheet.png`, `Selection.json` and `Formula2027-Visual-Upgrade-8screens.zip` alongside. The prior HUD selection is archived locally in `Previous-HUD-V2`.

## Reproduction

Use the explicit Unity Tools menu commands: **Upgrade Formula Vehicle Finish**, **Upgrade Formula Race HUD**, **Upgrade Formula Circuit Presentation**, **Validate Formula Visual Upgrade**, **Play Validate Formula Visual Upgrade**, **Capture Marketing Screenshots**, and **Refine Marketing HUD Action**. The circuit command requires saved scenes and restores the open scene setup. Capture/QA restore Editor preview settings and do not modify the player's save.

## Practical limits

This is a presentation pass on the existing models and environments. It does not replace the vehicle UV artwork, circuit meshes, vegetation models or crowd assets. Those assets still limit an AAA comparison. No Nintendo build, hardware image-quality/performance measurement or sales-conversion test was performed. Marketing images are a Unity Editor preview and need target-build validation/recapture before Nintendo store submission.

Concurrent controller-slider/font work in the same checkout is preserved; it is outside this visual upgrade.

## Global Volume rollback

At the user's request, both Global Volume grading passes were reverted. All 18 original Volume components and their shared Circuit Surface Grade profile match the pre-upgrade commit `eec51630` byte for byte. The five added grading profiles and both authoring recipes were removed. Other scene lighting, materials, camera settings and HUD changes are preserved. Earlier screenshots/validation results above document the prior grade; no replacement captures were requested or produced after rollback. See `GlobalVolumeUndo.json`.
