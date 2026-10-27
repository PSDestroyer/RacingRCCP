# Nintendo profile photo

The existing NintendoManager now reads the selected user's profile photo on demand through Account.LoadProfileImage. The same initialized Uid owns the nickname and save. A bounded 128 KiB temporary buffer is trimmed to the returned byte count before decoding. Failed reads, zero/oversized responses and failed decoding use the existing menu appearance.

One Texture2D is shared for the application's lifetime. It has no mipmaps, uses bilinear filtering/clamped edges, and LoadImage discards the readable CPU copy. Later page openings reuse the cached result, including a failed lookup, instead of querying the account or decoding again. The texture is released on application quit. No image bytes or texture references enter the save JSON, and no network requests or per-frame work are added.

FormulaPlayerAvatar presents that texture in 38×38 RawImage slots beside the existing nickname in the main header, career header, garage upgrades and results. The old helmet is hidden only while a photo is available; disabling the component clears the UI reference and restores the original fallback state. Photos do not receive raycasts. Nickname labels render literal text and ellipsize within their existing bounds. Only menu/results UI contains these slots; racing HUD and rendering are unchanged by this feature.

Authoring: Tools > Formula Career > Add Nintendo Profile Photos. Changes are persisted in Formula_Menu.prefab and FormulaResults.prefab. Relevant menu style builders retain the slots when regenerated.

## Validation and limits

Tools > Formula Career > Validate Nintendo Profile Photos exercises the authored menu with an isolated temporary save and a generated representative portrait. It checks required slots, desktop fallback, positive photo presentation, raycasts, page changes, component disable/re-enable, spacing and literal nickname display. AvatarValidation.json records the result. AvatarPreview.png shows the fixture, not a real Nintendo account photo.

A standalone harness also compiled the actual NintendoManager under UNITY_SWITCH with simulated SDK/Unity dependencies. Valid image caching, failed native reads, zero size, oversized output, failed decode cleanup and shutdown texture release passed. This checks control flow only. No Nintendo SDK/module is installed on this Mac. In particular, the managed binding for the new LoadProfileImage(ref long, byte[], long, Uid) call has not been compiled against a real Nintendo SDK; verify it with the installed SDK's signature and test the actual profile read on console before release. No reflection, native-library imports or new packages were added.

The shared photo normally requires its own UI batch when visible. There is no additional race rendering, but no hardware frame-time or draw-call measurement has been performed. Validate the final menus on the target console.
