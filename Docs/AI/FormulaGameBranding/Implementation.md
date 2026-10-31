# Game logotype everywhere — 2026-10-31

The existing transparent `Formula2027-GrandPrixLegends.png` is the canonical artwork. The menu already uses it in three authored logo containers and Results uses it once. Unity validation confirmed no enabled, visible legacy `F27` label remains in Menu, Results or Race UI. Inactive legacy text/localization entries are retained to preserve existing IDs and bindings.

Replaced the remaining baked F27 billboard and F27 RACING flag graphics with that same artwork. The logo is fitted by aspect ratio and rotated on vertical flags. Existing F27.png / Flag.png GUIDs, importer settings, dimensions (1024×600 / 256×1024), seven materials and scene references are preserved. Coverage inspection found 505 material reference slots across eight circuits using these assets; other circuits have no references to these particular materials. No circuit scenes or materials were rewritten. APEX sponsor artwork is unchanged.

The sign generator now renders the image instead of drawing abbreviated brand text, so future presentation passes retain the logo. The legacy menu-header authoring path also hides its old text composition and styles the canonical logo. The shared editor logo helper now checks Unity object nulls explicitly when adding a RawImage.

Verification: Unity 6000.3.19f1 compiled and completed `FormulaVisualPresentationUpgrade.GameBrandingBatch` with exit code 0; `Validation.json` records prefab checks. Both generated PNGs were visually inspected for readability, cropping, proportions and orientation. `CircuitCoverage.json` records existing material references. No runtime code, renderers, materials, shader passes or animation systems were added. No Nintendo hardware build or frame-time profiling was performed.

Repeat via Tools → Replace F27 With Game Logo. For a separate batch Editor, use `-batchmode -quit -projectPath <project> -executeMethod FormulaVisualPresentationUpgrade.GameBrandingBatch -logFile <log>` with graphics enabled.
