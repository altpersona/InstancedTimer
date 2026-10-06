# CHANGELOG — valheimmer / SunkenCryptTimer

## 2026-10-06 — v1.1.2: fix invisible HUD text + missing rings on dedicated servers

- First real-server test (Stormer dedicated server, Valheim 1.0.16) showed neither
  feature; session-log instrumentation added in-between builds pinpointed both causes.
- **HUD text invisible**: the font copied from the vanilla event-name label was a
  non-null reference to a not-yet-loaded font asset (Unity 6 lazy loading) — TMP
  renders nothing and logs "no Font Asset assigned". Fonts are now trusted only when
  their atlas texture is resident; otherwise the HUD is scanned for any actively
  rendering label's font, then TMP's default asset, retying each update until one
  loads (so late font assignment self-heals).
- **Rings never created**: dungeon names resolved to the generic "Dungeon"
  placeholder because the LocationProxy ZDO's `s_location` string reads empty on
  dedicated servers (worked on the local test server — why the smoke test passed).
  Names now come (via reflection) from the proxy's private `m_instance` — the
  spawned location GameObject, whose name is `SunkenCrypt4(Clone)` — with the ZDO
  field and a zone-system position match as fallbacks.
- Ring lifecycle now logs at Info level once per stage (`zone rings: template
  ready`, `ring radius: <prefab> <r>m`, `ring created: ...`, `tracked dungeon:
  '<name>' (via <source>)`), so field failures are diagnosable from LogOutput.log
  without enabling DebugLogging.
- Buildable on any machine: `dotnet build -p:GameDir=... -p:ProfileDir=...`
  (first non-124 build; dotnet SDK 9 installed on Machine B for this).

## 2026-10-06 — GitHub publish + Thunderstore packaging

- Public repo created: https://github.com/altpersona/SunkenCryptTimer (MIT license,
  gh CLI authenticated by user over device flow, SSH remote via uploaded ed25519 key).
  Private/local files kept out via .gitignore: DEV_NOTES.md, TODO.md, deploy.sh,
  setup-local-server.py, .claude/, build artifacts (leak scan: server IP/local paths
  only ever lived in those).
- Release v1.1.0 published with assets `SunkenCryptTimer-v1.1.0.zip` (Thunderstore
  format: manifest+README+icon+CHANGELOG at root, DLL under Plugins/) and the raw DLL.
- README rewritten user-facing (requirements matrix, install, usage incl. VLR
  approach-trigger semantics, config table, build-from-source, version compatibility).
- `package.sh` (builds the zip), `tools/make_icon.py` (256×256 icon: timer ring +
  arched crypt door), manifest description trimmed to 204/250 chars, website_url set
  to the GitHub repo. Thunderstore upload itself is the remaining user step (TODO.md).

## 2026-10-05 — v1.1.0: zone rings + deep VLR research

- **Research session (all verified against VLR master source, the server-synced config,
  and raw game asset dumps):**
  - Stormer server VLR config: `ResetTime=10` in-game days (~5 h real; 1 day ≈ 30 min),
    `OverrideResetTimes=false` → ALL dungeon types use 10, incl. Sunken Crypts
    (`SunkenCrypt4`; the `Crypt2/3/4` names are black-forest burial chambers).
  - Reset does NOT happen in the background: fires when a modded player approaches
    ≤100 m horizontal (VLR `RESET_RANGE`; watcher gives up >200 m; ~5 s after zone
    load, 1 s polls, ONE SHOT per zone load). Reset executes on the approaching
    client (VLR adds its watcher only on non-dedicated instances, needs ZDO ownership).
  - Blocks ("player activity", horizontal-only checks): player pieces/tombstones/players
    within exteriorRadius = **12 m** (ground) or dungeon radius = **45.25 m**
    (hypot(64/2,64/2) of `DG_SunkenCrypt.m_zoneSize=64×64`, at interior altitude y≥4000)
    of the crypt center. Skip defers, never cancels; timer keeps counting.
  - Terrain: raising land does NOT block resets, but VLR's `TerrainReset.ResetTerrain`
    reverts all terrain mods within the 12 m ground radius on each reset.
- **v1.1.0 changes (user decisions: 45 m ring only, instanced dungeons only, same
  plugin, visible within 100 m):**
  - New `SunkenCryptTimer/src/ZoneRing.cs` — ring manager (see DEV_NOTES architecture).
  - `src/Tracker.cs` — `FindNearestTrackedDungeon` → `CollectTrackedDungeons` (collects
    all); `NearbyDungeon` gained `Position`/`Uid`, now `internal`; HUD label logic
    extracted to `UpdateHudLabel()` unchanged.
  - `Plugin.cs` — `Rings/Enabled` (true), `Rings/ShowRadiusMeters` (100) config;
    `PluginVersion` 1.0.0 → 1.1.0. Same bump in `SunkenCryptTimer.csproj` (`<Version>`)
    and `SunkenCryptTimer/manifest.json` (`version_number`, description updated).
  - `README.md` — zone rings section, config rows, layout line.
  - Verified: build 0 errors; deploy to Gale profile; local server smoke test clean
    (`Loading [SunkenCryptTimer 1.1.0]`, 0 fatal). In-game visual check pending (user).
