# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project summary

- Root: `/Users/gheorghe/GitHub/RacingRCCP`.
- Inspected: 2026-10-22; commit `50720840`; working tree initially clean.
- User-confirmed direction: Formula 2027 themed racing, ambitious AAA presentation, simcade handling, original Nintendo Switch and Steam/PC. Product direction is recorded in `Docs/Formula2027Direction.md`.
- Inspection was read-only for Unity assets and runtime code. No Editor, build, playtest or performance validation was performed.

## Confirmed environment

| Area | Finding | Evidence |
| --- | --- | --- |
| Editor | Unity 6, `6000.0.55f1`, revision `c5f59906196d` | `ProjectSettings/ProjectVersion.txt` |
| Rendering | URP `17.0.4`; default and sole quality level use `PC_RPAsset` | `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/QualitySettings.asset`, `Assets/Settings/PC_RPAsset.asset.meta` |
| Input | Both legacy and new input enabled; first-party UI uses Input System `1.14.2` | `ProjectSettings/ProjectSettings.asset` (`activeInputHandler: 2`), `Assets/_GarageV2/Scripts/InputManager.cs` |
| Camera/UI | Cinemachine `3.1.6`, uGUI `2.0.0`, TextMesh Pro components | Manifest, first-party HUD/menu/loading scripts |
| Other packages | AI Navigation `2.0.11`, Localization `1.5.11`, Timeline `1.8.7`, Recorder `5.1.6` | Manifest |
| Addressables | `2.6.0`, transitive registry dependency; configuration assets exist | `Packages/packages-lock.json`, `Assets/AddressableAssetsData` |
| Tests | Unity Test Framework `1.5.1`; no first-party test assemblies identified | Manifest and source/assembly inventory |
| Networking | Photon PUN/Realtime/Chat source and assemblies imported; no networking usage identified in inspected first-party gameplay | `Assets/Photon`, searches in `_GarageV2` and `_PlatformSpeciffics` |

Manifest dependencies are registry/built-in packages; no Git, local-file or embedded package integration was identified. Imported HDRP/Built-in environment assets also exist; their presence does not change the configured URP pipeline or establish compatibility.

## Structure and assembly boundaries

- `Assets/_GarageV2/Scripts`: first-party menu, selection, missions, gameplay, HUD, camera and input integration.
- `Assets/_GarageV2/Resources`: car prefabs and `CarSO`/`MapSO` catalogs loaded through `Resources`.
- `Assets/_GarageV2/Recorder`: experimental recording/replay; `RecordTransformHierarchy` guards Editor APIs, while `ReplayRccp` is a stub.
- `Assets/_PlatformSpeciffics`: singleton, startup, JSON save data and conditionally compiled Switch account/save integration. Editor tooling lives in its `Editor` subdirectory.
- `Assets/Scenes`: startup, username, menu and gameplay test scenes.
- `Assets/Settings`: URP assets and volume profiles. Mobile rendering assets exist but the serialized quality list currently contains only PC.
- `Assets/Formula`, `Assets/RaceTracksPack`, `Assets/RTG 1.3`: imported candidate vehicle/track content.
- `Assets/Realistic Car Controller Pro`, `Assets/Realistic Traffic Controller`, `Assets/Photon`: imported frameworks. Do not treat vendor examples as implemented product features.
- No first-party `.asmdef`/`.asmref` found in the inspected source inventory; first-party scripts use default Unity assemblies. Photon owns explicit assemblies; `PhotonUnityNetworking` references `PhotonRealtime`.
- No project/ancestor `AGENTS.md`, project `CLAUDE.md`, README, contribution guide or existing project documentation structure was found during this inspection.

## Scenes and startup

`ProjectSettings/EditorBuildSettings.asset` enables these scenes in order:

1. `Assets/Scenes/Init.unity`
2. `Assets/Scenes/Username.unity`
3. `Assets/Scenes/Menu.unity`
4. `Assets/Scenes/GameplayTestScene.unity`
5. `Assets/Package/Scene/Night.unity`
6. `Assets/RaceTracksPack/Scenes/Track A/RT_Track_A.unity`
7. `Assets/ReversedAssets/City Builder Urban 2/Scenes/Tokyo.unity`
8. `Assets/Sci-Fi Neon City HDRP/Scenes/Sci-Fi Neon City.unity`
9. `Assets/Ocean City- Mobile Optimized/scene_night.unity`

`SampleScene` is disabled. `Init.Start` chooses build index 1 or 2 based on saved player name. `LoadingManager` provides asynchronous scene loading with progress UI; some other scripts load scenes directly. This is confirmed source flow, not a verified runtime startup sequence.

## Architecture and existing race foundation

- **Confirmed:** MonoBehaviour managers, a custom `Singleton<T>`, static selected-data helpers, ScriptableObject catalogs, and Newtonsoft JSON save data. No first-party dependency injection or networking architecture identified.
- **Confirmed:** `GamePlayManager.cs` contains a 4,304-line multi-mode implementation including cinematic introduction, countdown, AI spawning, waypoint/lap progression, standings, results and rewards. Extend with focused components rather than increasing its responsibilities.
- **Confirmed:** RCCP supplies vehicle, wheel, powertrain, aero, AI and camera components. `GameplayMangerCarHud.cs` displays RCCP speed, gear, RPM and assists.
- **Confirmed:** `Assets/_GarageV2/Resources/Cars/F1.prefab` contains RCCP components. The nine inspected `CarSO` catalog entries contain road cars and no `Cars/F1` entry, so the Formula prefab is not integrated into ordinary selection.
- **Confirmed:** `Assets/Formula/Formula_A1/Prefabs` contains 14 visual prefabs; track assets include quality variants. Appearance, handling and suitability remain unverified.
- **Likely gaps:** no sector, best-lap/delta, pit-stop, tyre-wear, race-flag/penalty, safety-car or ERS/active-aero gameplay system was identified in inspected first-party code. Race time formatting currently rounds to whole seconds.

## Conventions

Most garage classes have no namespace; platform/save code uses `_Assets._PlatformSpeciffics.Switch` and `HalvaStudio.Save`. Files generally use Allman braces, private serialized fields and public accessors, with mixed underscore/camelCase naming. Coroutines are used for scene loading. Comments mix Romanian and English. Preserve existing serialized field names and asset references; no repository-wide style standard was found.

## Platform prerequisites and validation

- **Confirmed source issue:** `Init.Awake` calls `NintendoSave.Initialize` for every non-Editor player, but `NintendoSave` exists only under `UNITY_SWITCH && !UNITY_EDITOR`.
- **Confirmed source issue:** `SaveManager.Load`/`Save` select Switch methods for every non-Editor player, while those methods are compiled only for Switch. PC player compilation therefore needs corrected platform guards and the local-file save path. This was established by source inspection, not a build invocation.
- Switch account/save code exists; SDK installation, supported Editor module, device access and build readiness are **unknown**.
- No Steam API integration was identified in inspected first-party code/dependencies; store integration and PC OS targets remain **unverified**.
- No first-party automated tests or CI build workflow identified. No tests were run for this documentation-only change.

## Available Unity tooling

`mcp__unity__unity_cli_status` was available but returned: `No Pipeline instance found for project`. Live console, scenes, play mode, tests, build settings and profiling are unavailable through that connection in this session. No Unity MCP package was identified in the manifest. Repository inspection remains available; no packages were installed or changed.

## Important unknowns

Current compile/play-mode health; graphics/material compatibility; Switch frame time and memory; handling and AI at Formula speeds; controller/device behavior; final grid size; visual identity and whether real-world season accuracy is intended. No 2027 sporting or technical regulation has been researched or encoded.

## Representative sources inspected

`ProjectSettings/{ProjectVersion.txt,EditorBuildSettings.asset,GraphicsSettings.asset,QualitySettings.asset,ProjectSettings.asset}`; `Packages/{manifest.json,packages-lock.json}`; `Assets/Settings/PC_RPAsset.asset`; `_PlatformSpeciffics/{Init.cs,Singleton.cs,SaveManager.cs,Switch/NintendoManager.cs,Switch/NintendoSave.cs}`; `_GarageV2/Scripts/{GamePlayManager.cs,GameplayMangerCarHud.cs,InputManager.cs,LoadingManager.cs,CarSO.cs,MapSO.cs,MenuCameraOrbit.cs,EnableCinemachine.cs,Static scripts/GlobalCarData.cs}`; `_GarageV2/Recorder/{RecordTransformHierarchy.cs,ReplayRccp.cs}`; `_GarageV2/Resources/Cars/F1.prefab`; car catalog assets and Photon assembly definitions.

<!-- unity-onboarding:generated:end -->
