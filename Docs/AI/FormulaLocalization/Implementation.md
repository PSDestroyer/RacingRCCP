# Formula localization — Nintendo Switch language set

The game supports exactly `en`, `fr`, `de`, `es`, `it`, `nl`, `pt`, `ru`, `ja`, `ko`, `zh-Hans`, and `zh-Hant`. Romanian was a temporary integration test; its locale, table group, dictionary, and font assets have been removed.

## Player behavior

- Settings contains a controller-selectable language button. Submit cycles through the 12 native language names and returns to English.
- A new or old profile without a supported language uses Unity's native `Application.systemLanguage`; unsupported system languages use English. The chosen override is saved in the existing profile, including the Switch save path.
- Language changes refresh menu labels, dynamic objectives, device hints, loading captions, confirmation dialogs, and results without changing navigation IDs, career data, nickname, or controller bindings.
- Unity Localization owns `SelectedLocale`. There is no second locale state, synchronous table wait, runtime translation service, or added UPM dependency.

## Data and fonts

`FormulaUI` contains the Formula copy. Existing `UI` table IDs are retained and values are populated for all 12 locales. The 12 source dictionaries (including `en.json`) under `Assets/Formula/Localization/Translations` are Editor inputs; String Tables are the runtime data. Indexed format tokens and TMP markup are preserved. Game/livery branding, physical units, controller key names, and proper track brands may intentionally remain identical.

`FormulaFonts` Asset Tables select small font profiles for heading, emphasis, and body text. Western profiles preserve the authored Resources font instead of rebundling it; the other profiles reference their locale-specific font asset. Western locales retain the existing Formula faces, with a small Latin/Cyrillic font and symbol fallback. Russian uses the Cyrillic-capable face. Each CJK locale uses its own Noto region font, through localized asset references rather than a global CJK fallback chain. Source fonts and the SIL OFL license are in `Fonts/`. Fonts prewarm the translated character set and preserve it when building; dynamic multi-atlas support also accommodates player nicknames. Font loads are shared by the localization package. `FontAudit.json` verifies a single atlas per localized font, persisted materials, and disabled build-time clearing of the prewarmed glyphs. Fitting occurs when text or a locale changes, with the existing text-size preference applied.

The first-run Username scene also receives localized fonts, and its saving animation uses localized copy. The singleton Awake guard now avoids destroying an instance that was found before Awake during locale/profile startup. Runtime initialization also reapplies the saved locale if Editor font previews initialized Localization before Play Mode.

Translations originally started from local Argos models. The follow-up language review checked all 585 original copy entries in each of the 12 languages, including English, and corrected terminology, meaning, grammar, and menu length. Traditional Chinese uses Taiwan game terminology. Complete templates preserve the natural word order of event actions, input conflicts, and controller page captions; vibration on/off phrases handle gender agreement. See `CopyReview.json` for the exact before/after changes. A native-language release proofread remains recommended. No player names, save files, or source code were sent to a translation service.

## Rebuilding and verification

- `Tools > Formula Career > Build Nintendo Language Localization` updates the String/Asset Tables and the four Formula prefabs, plus Username fonts. It fails on missing dictionary entries or glyphs.
- `Validate Nintendo Language Localization` checks format tokens, persistent Editor/runtime TMP bindings, all language choices with a virtual gamepad, saved profile data, 20 live menu views per locale at 115% text size, glyph coverage and button bounds, restart persistence, a career race, and representative results. It uses an isolated temporary save and restores Editor state.
- `Build and Audit Localization Content` audits all String Tables, then calls the real Addressables content builder for the active target.

The validation reports record the latest Unity 6000.3.19f1 pass: all 12 controller choices and menu font swaps, 20 menu views plus an upgrade dialog in every locale at 1280 × 720 and 115% text size, both vibration states, a full saved-locale restart, a Japanese career race/restart dialog, and representative results. `TableAudit.json` records 7,212 String Table entries with no gaps (6,252 FormulaUI entries plus 960 legacy entries). The final pass also treats TMP missing-glyph warnings during font transitions as failures; none occurred. Addressables content built successfully for StandaloneOSX. The temporary automatic test trigger has been removed.

The JSON reports and screenshots in this folder are the validation evidence. `overflow` includes TMP overflow and text bounds outside parent buttons. The two unlocalized season-logo numerals can trigger the TMP metric check; their authored design is preserved. Review screenshots alongside those diagnostics.

This Mac has no Unity Switch module or Nintendo SDK. A Switch player build, physical Joy-Con/Pro Controller test, and device memory/frame-time measurements are not covered by these Editor checks.

## Language-set reference

Nintendo lists 12 system languages for the original Switch: https://www.nintendo.com/au/support/articles/how-to-change-the-system-language/

Mario Kart 8 Deluxe's Canadian listing includes the same language set: https://www.nintendo.com/en-ca/store/products/mario-kart-8-deluxe-can-105276/

This is a practical reference set, not a universal translation requirement for every Nintendo game. The user chose these 12 without Romanian on 2026-10-27.
