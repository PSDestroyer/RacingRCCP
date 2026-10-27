# Language and menu-fit review

Reviewed English, French, German, Spanish, Italian, Dutch, Portuguese, Russian, Japanese, Korean, Simplified Chinese and Traditional Chinese on 2026-10-28.

All 585 original copy entries were reviewed per language; 2,217 original mapped values changed, and six complete message templates were added. `CopyReview.json` lists the changed values without counting the extra English lookup aliases as additional translations. English copy can now be revised through `en.json` without renaming source keys or changing table IDs. Legacy UI table IDs are preserved, with aliases supporting repeat builds.

| Language | Example corrected |
|---|---|
| English | Front Suspensions Spring Force → Front spring stiffness |
| French | Foie → Livrée |
| German | Vordere Säge → Sturz vorn |
| Spanish | Este vivero no está disponible. → Este diseño no está disponible. |
| Italian | Crediti all'estero → Ottieni crediti |
| Dutch | eigen likeuren → je kleurstellingen |
| Portuguese | Fígado → Pintura |
| Russian | Осенний цирк → Осенняя трасса |
| Japanese | repeated V-SYNC labels → 垂直同期 |
| Korean | 회사 소개 → 상점 |
| Simplified Chinese | machine-generated spring/dam wording → 弹簧和阻尼 |
| Traditional Chinese | 後置暫停 → 後懸吊 |

Racing terms, lap validity, downforce/drag, ownership/equipping, save errors, input rebinding and reward requirements were made consistent within each locale. Short labels were preferred where space is tight, including Russian accessibility tabs. Event-view buttons and input conflicts now use whole templates so CJK word order is correct. Both vibration states use complete phrases for grammatical agreement. Unity's untranslated “Press” interaction prefix is omitted from key labels; physical key/button names are retained.

The expanded Unity validation covers 20 views per language: the six main pages; audio, keyboard, gamepad, wheel, display, graphics and accessibility panels; garage setups and liveries; later career/profile pages; and the last binding page for each controller type. Each locale also opens an upgrade unlock dialog. Captures use the authored 1280 × 720 layout and 115% body text preference. Glyph coverage, format tokens, markup, empty entries, translation artifacts, TMP overflow and text bounds relative to parent buttons are audited. Save/restart and race/results checks are retained.

The JSON reports and current screenshots are the evidence. This review was performed by the coding assistant; it is not a certification by native-language editors. Switch hardware and SDK compilation remain outside the available Mac validation environment.

Final reports: 7,212 String Table entries, zero gaps; 7,944 dictionary entries with preserved format tokens and markup; all 12 locale runs passed. No parent-button overflow or missing glyphs were reported. The two TMP overflow diagnostics concern the unchanged decorative season-logo numerals. The six localized font assets each use one saved atlas and retain their prewarmed characters during builds. Addressables content built successfully for StandaloneOSX.
