# CHANGELOG — valheimmer / SunkenCryptTimer

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
