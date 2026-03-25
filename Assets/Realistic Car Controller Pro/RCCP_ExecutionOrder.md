# RCCP Script Execution Order

## Overview

RCCP uses `[DefaultExecutionOrder]` attributes to control script initialization order. This ensures singletons and managers initialize before vehicle components that depend on them.

## Execution Order Tiers

| Tier | Order | Purpose | Scripts |
|------|-------|---------|---------|
| Infrastructure | -50 | Singletons & global systems | InputManager, SkidmarksManager, SceneManager |
| Vehicle Core | -10 | Main vehicle orchestrator | CarController |
| Component Managers | -5 | Collection/container components | Axles, Exhausts, OtherAddons, Lights |
| Standard | 0 | Regular components | Engine, Gearbox, Stability, Audio, etc. |
| Camera | +5 | Camera follows vehicle | Camera |
| Late/Visual | +10 | Post-init visual effects | BodyTilt, Customizer, Lod |

## Complete Execution Order Table

| Order | Script | File Location |
|-------|--------|---------------|
| -50 | RCCP_InputManager | Scripts/Inputs/RCCP_InputManager.cs |
| -50 | RCCP_SkidmarksManager | Scripts/Manager/RCCP_SkidmarksManager.cs |
| -50 | RCCP_SceneManager | Scripts/Manager/RCCP_SceneManager.cs |
| -10 | RCCP_CarController | Scripts/Vehicle/RCCP_CarController.cs |
| -5 | RCCP_Axles | Scripts/Vehicle/RCCP_Axles.cs |
| -5 | RCCP_Exhausts | Scripts/Vehicle/RCCP_Exhausts.cs |
| -5 | RCCP_OtherAddons | Scripts/Vehicle/RCCP_OtherAddons.cs |
| -5 | RCCP_Lights | Scripts/Vehicle/RCCP_Lights.cs |
| 0 | (all other components) | Default Unity order |
| +5 | RCCP_Camera | Scripts/Camera/RCCP_Camera.cs |
| +10 | RCCP_BodyTilt | Scripts/Others/RCCP_BodyTilt.cs |
| +10 | RCCP_Customizer | Scripts/Vehicle/RCCP_Customizer.cs |
| +10 | RCCP_Lod | Scripts/Vehicle/RCCP_Lod.cs |

## Initialization Flow

```
AWAKE PHASE (by execution order):

[-50] RCCP_InputManager
      - Creates RCCP_Inputs structure
      - Initializes New Input System
      - DontDestroyOnLoad()

[-50] RCCP_SkidmarksManager
      - Creates skidmark pools for each ground material
      - Must exist before wheels start skidding

[-50] RCCP_SceneManager
      - Subscribes to RCCP_Events
      - Ready to receive vehicle spawn notifications

[-10] RCCP_CarController
      - GetAllComponents() - lazy-loads all child components
      - Components register via CarController property access

[-5]  RCCP_Axles, RCCP_Exhausts, RCCP_OtherAddons, RCCP_Lights
      - GetComponentsInChildren<T>() to collect children
      - Cache references in lists

[0]   Standard components (Engine, Gearbox, Clutch, etc.)
      - Register with CarController on first property access
      - Safe lazy-loading pattern

[+10] RCCP_BodyTilt, RCCP_Customizer, RCCP_Lod
      - Run AFTER all physics components ready
      - BodyTilt uses Start() for collider transfer
      - LOD accesses all components to set detail levels

ONENABLE PHASE:

      RCCP_CarController.OnEnable()
      - Fires OnRCCPSpawned / OnRCCPAISpawned events
      - SceneManager receives and registers vehicle
      - CheckBehavior() applies global behavior settings
```

## Component Dependencies

| Component | Depends On | Reason |
|-----------|------------|--------|
| RCCP_InputManager | InputActionAsset | Reads input actions |
| RCCP_SkidmarksManager | RCCPGroundMaterials | Creates pools per friction type |
| RCCP_CarController | Rigidbody (RequireComponent) | Physics simulation |
| RCCP_Clutch | Engine (via CarController) | Reads engineRPM |
| RCCP_Gearbox | Clutch (via CarController) | Reads clutch output |
| RCCP_Differential | connectedAxle (inspector) | Distributes torque |
| RCCP_WheelCollider | WheelCollider (RequireComponent) | Unity physics |
| RCCP_Lod | All components | Sets LOD on everything |
| RCCP_BodyTilt | Colliders exist | Transfers colliders in Start() |

## Why These Orders?

### -50 (Infrastructure)
- Must exist before ANY vehicle spawns
- Singletons that provide global services
- InputManager: Vehicles read input from it in FixedUpdate
- SkidmarksManager: Wheels request skidmarks immediately
- SceneManager: Receives spawn events from vehicles

### -10 (Vehicle Core)
- CarController is the registration hub
- All child components find it via GetComponentInParent
- Must be ready before child components try to register

### -5 (Component Managers)
- Run after CarController exists
- Collect child components into lists
- Other systems query these lists (Stability queries Axles)

### +10 (Late/Visual)
- Need full vehicle setup complete
- BodyTilt: Transfers colliders (must exist first)
- Customizer: Applies visual changes
- LOD: References all other components

## Notes

- All execution orders are set via `[DefaultExecutionOrder(n)]` attributes in the source code
- Project Settings > Script Execution Order will reflect these attributes
- RCCP_Camera (+5) runs after standard components but before visual effects (+10)
