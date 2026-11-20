# Formula Steam setup

The project uses Steamworks.NET `2025.165.0`. Formula achievements subscribe to
`FormulaSession` events and do not read, write, migrate, or extend the save format.

## App ID

The local `steam_appid.txt` uses Formula's App ID `4728350`. The file is ignored by
Git and must not be included beside a build uploaded to Steam.

## Steamworks App Admin

1. Open **Stats & Achievements > Achievements** for the Formula app.
2. Import `achievements_definitions.csv` and `achievements_localization.csv`.
3. Upload the matching 256 × 256 PNG files from `achievement-icons/` using names
   `<API_NAME>_ACHIEVED.png` and `<API_NAME>_UNACHIEVED.png`.
4. Publish the Steamworks configuration changes.
5. Enable **Steam Achievements** under **Supported Features** on the store page.

The runtime queues unlocks in memory until Steam initializes, calls
`SetAchievement`, and then calls `StoreStats`. Steamworks SDK 1.65 synchronizes
current stats before launching the game, so its removed `RequestCurrentStats` call
is not needed. The integration remains a no-op when Steam is not available, so
non-Steam desktop runs and Switch builds continue normally.
