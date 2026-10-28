# Nintendo Switch texture memory optimization

Authored 490 Switch-only texture importer overrides in Unity 6000.3.19f1. The original source textures are unchanged. Default, Standalone, Android and other existing platform settings, mipmaps, sRGB/normal-map interpretation, alpha handling, streaming flags and GUIDs are preserved. The 21 pre-existing metadata edits were retained.

## Profile

- Environment color textures: ASTC 6x6; up to 2048 for large source atlases, otherwise up to 1024. Existing smaller limits remain smaller.
- Normal maps: ASTC 4x4 and up to 1024 when this reduces memory. Textures with no useful saving retain their previous settings.
- Vehicle body/lettering/livery atlases: retain up to 2048 and existing compressed settings where changing formats would increase memory. Shared interior, wheel and particle detail is capped at 1024.
- Six 4096 lightmap atlases and their directional pairs: capped at 2048 together. HDR lightmaps retain Automatic/High Quality compression (currently BC6H); directional maps retain ASTC 4x4.
- Read/Write was already disabled on every inventoried texture. No CPU copies were removed.
- UI, font atlases, cubemaps, legacy importers, meshes, scene/prefab hierarchies and runtime shaders were not changed.

## Estimated payload, not measured RAM

The unique modified Texture2D assets total **1806.9 MiB before → 342.8 MiB after** (81.0% less theoretical texture payload). These assets span multiple scenes and vehicles and are **not all resident at once**. This is not a reduction claim for total application RAM.

The baseline formats come from `TextureImporter.GetAutomaticFormat("Nintendo Switch")`. Estimates include mip chains, ASTC block padding and NPOT scaling. They exclude allocator alignment, import encoder differences, render targets, cubemaps, font atlases, meshes, audio and actual streaming residency.

Only the modified textures referenced by each root are included below. Shared textures are deduplicated within each root. Other textures still contribute to scene memory.

| Scene / vehicle | Before (MiB) | After (MiB) |
|---|---:|---:|
| Formula_Generic_RCCP_01 | 6.2 | 3.4 |
| Formula_Circuit_01 | 73.3 | 38.1 |
| Formula_Circuit_06 | 66.3 | 24.5 |
| Formula_RTG_01 | 32.4 | 10.8 |
| Tokyo | 582.6 | 68.0 |
| Sci-Fi Neon City | 492.4 | 115.7 |
| scene_night | 359.3 | 34.7 |

## Validation and device follow-up

`TextureCompression.json` contains every modified asset, GUID, original metadata hash, format, dimensions, payload estimate and per-root totals. `SerializationValidation.json` checks that removing the new Switch block reproduces the original metadata exactly.

Run **Tools → Formula Career → Validate Switch Texture Compression** to check the imported overrides against the manifest. With an installed Switch module it also checks supported target formats. It writes `ImportValidation.json`. This command does not change asset settings.

The Mac has no installed Switch module or Nintendo SDK. A successful settings validation is not a Switch build or an out-of-memory fix confirmed on hardware. Rebuild Switch content/Addressables with these metadata files on the Windows build machine. Existing bundles built with the previous settings must be rebuilt.

On a development Switch build, capture peak memory for the heaviest available scenes and repeat menu → race → menu transitions. Check car markings, road/normal detail, alpha particles and resized baked lighting on the device. Compression reduces texture payload; confirming the application memory budget and transition peaks requires target hardware.

Current validation: all 490 imported Switch overrides pass in the macOS Unity Editor. Four ASTC 6x6 encode/decode previews (vehicle atlas, asphalt, city props and building atlas) were inspected; fine lettering, colors and surface detail remain recognizable. The car body atlas stays at 2048 after visual review. These previews compare compression at the authored target resolution against the currently imported desktop texture; they are not a full scene rendering or a Switch runtime test. The temporary probe is removed after validation; no automatic import/mutation hook remains. The persistent validation menu command remains available.

Sources: [Unity texture importer platform settings](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/TextureImporter.GetPlatformTextureSettings.html), [Unity texture configuration and memory guidance](https://unity.com/how-to/project-configuration-and-assets).

`RootTextureEstimates.json` includes estimates for all inventoried Texture2D dependencies, including unchanged assets: Formula Circuit 01 approximately 82.1 → 46.9 MiB; the Generic vehicle approximately 12.0 → 9.2 MiB with its body atlas kept at 2K. These static dependency totals exclude dynamically loaded resources and other RAM costs.
