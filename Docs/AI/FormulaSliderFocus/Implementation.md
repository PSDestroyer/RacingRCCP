# Settings slider selection

Audio sliders and vibration intensity now highlight their entire row with the same red selected panel and white leading edge as other menu controls. The selected fill turns yellow and its handle becomes white and 20 pixels wide. Deselection restores each slider's authored colours and handle width immediately. The same treatment applies to all three pause-menu volume sliders, including while game time is paused.

The existing `FormulaMenuFocus` owns this feedback. `FormulaSettingsView.Bind` configures its four sliders and `FormulaRaceView.Bind` configures its three audio sliders. The change reuses existing panel/fill/handle graphics and materials, adds no UI renderers or textures, and does not change slider ranges, value listeners, save behaviour or navigation bindings. Slider visuals update on selection and pointer events; they do not run an animated transition. Existing button focus behaviour is preserved.

The vibration row's inherited panel now stretches to its shorter row. Stretched fill rectangles have zero size offsets, and horizontal handles stretch vertically without an additional height offset. This also fixes the pause-menu bars extending beyond their rows and handles rendering at twice their intended height.

## Evidence

- The existing isolated-save vibration-settings runner passed six checks: controller adjustment, navigation, 0/100% bounds, on/off persistence, save/reload and 115% text size. `Menu-Vibration.png` shows the selected vibration row in the actual desktop menu.
- An initial run of the existing Switch presentation runner was observed passing 15 checks during the selection review, including 12 languages and real race pause/audio options. Its JSON was subsequently overwritten by the interrupted follow-up. The observer's three pause captures remain available: one highlighted row at a time. Those initial captures exposed the inherited rectangle sizing problem and are retained under `InitialReview/`.
- After correcting the rectangle sizing, the vibration-settings runner passed again. A follow-up broad presentation run timed out waiting for menu/localization readiness; its result is retained in `FollowUp-Interrupted.json`. It is not counted as a pass for that follow-up.
- Final pause geometry was reviewed separately in an isolated Editor preview after initializing the slider's cached references. `Pause-Geometry-Final.png` shows the selected music row at the 100% boundary. This is a UI-only preview, not a gameplay screenshot or a hardware performance measurement.
- The final runtime sources compile with the host Editor defines and with `UNITY_SWITCH` plus `UNITY_EDITOR`. Native Nintendo SDK branches and physical controller rendering were not built/tested on this Mac.

Temporary preview drivers were removed. The slider change does not author prefabs or scene assets. Concurrent vehicle/HUD/visual work present in the checkout was preserved; no source assets from that work were restored to an earlier version.
