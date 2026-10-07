# CHANGELOG — valheimmer / InstancedTimer (SunkenCryptTimer through v1.1.8)

## 2026-10-06 — v1.2.3: timer text now matches the status labels' on-screen size

- Field test (screenshot measured): v1.2.2 rendered 1.5x the size of the
  Wood/Resting/Shelter labels — the point bonus itself was the whole error.
  `StatusTextSizeBonus` removed; the label now adopts the rendered size of a live
  status-effect entry (its first active TMP - the same label vanilla writes the
  effect name into), carrying `fontSize` through both sides' world scale so the
  on-screen size matches exactly whatever scale the status hierarchy carries.

## 2026-10-06 — v1.2.2: timer text +2 more points

- v1.2.1's +2 over the status-text size was still too small (user field test);
  StatusTextSizeBonus now +4.

## 2026-10-06 — v1.2.1: timer text a couple points larger

- Field feedback on v1.2.0: the status-effect text size (adopted in v1.1.8) is too
  small to read comfortably. The label now adds +2 points to the status-text size
  (`StatusTextSizeBonus` in HudLabel.cs — tweak there and rebuild if it still needs
  tuning).

## 2026-10-06 — release automation: Thunderstore publishing scripted, push-on-every-test-phase

- `publish_thunderstore.sh`: publishes the packaged zip via the Thunderstore API in one
  call (legacy `submission/upload/` endpoint: multipart file + metadata → published;
  team `StandardVibeware`, community `valheim`, category `client-side`). Token at
  `~/.config/thunderstore/api_token` (tss_ service account, Bearer auth). Hard-won
  HTTP facts, handled in-script: Swagger's "Basic" scheme is wrong for tss_ tokens;
  the URL's trailing slash is mandatory (Django append-slash redirect behind Cloudflare
  = 502); live endpoint returns 200 (docs say 201). Package deprecation is blocked for
  service accounts — browser-only step.
- Standing procedure (user instruction): every build deployed for user testing
  automatically gets commit → push → GitHub release → Thunderstore publish. Full flow
  documented in PUBLISHING.md; also stored in Claude project memory.
- v1.1.7 was the first Thunderstore listing (published manually via the same API);
  v1.1.8 published during script debugging (manual curl — the script had the
  trailing-slash bug); v1.2.0 was the script's first clean end-to-end publish.

## 2026-10-06 — v1.2.0: renamed SunkenCryptTimer → InstancedTimer

- The mod outgrew its name: since v1.1.7 it labels every VLR-tracked location
  (runestones, graves, shipwrecks, ruins, huts), not just crypts. New identity
  everywhere: plugin name, GUID (`lan124.SunkenCryptTimer` → `lan124.InstancedTimer`),
  namespace, assembly + DLL name, project dir, scripts, GitHub repo
  (renamed; old URLs redirect), and Thunderstore package — where a name change
  means a new package (`StandardVibeware-InstancedTimer`); the old
  `StandardVibeware-SunkenCryptTimer` listing is deprecated in favor of it.
- Consequence of the GUID change: the config file is now
  `lan124.InstancedTimer.cfg` and starts from defaults (defaults match the
  previous recommended settings). Remove the old `plugins/SunkenCryptTimer/`
  folder when upgrading manually — both DLLs together would double-draw the HUD.
- No behavior changes; v1.2.0 is v1.1.8 under the new name.

## 2026-10-06 — v1.1.8: match status-effect text size, sit below the effect row

- Field feedback on v1.1.7's right-side label: font too large, and the fixed
  170 px top offset overlapped the minimap. The label now copies its font size
  from the status-effect template text (the small text on the Rested/Wet
  icons) and repositions itself every update just below the live bottom edge
  of the status-effect row, right edges aligned - so wrapped rows of effect
  icons push the timer down instead of it overlapping anything. HudOffsetY is
  now only a fallback offset for the rare case the row can't be located.

## 2026-10-06 — v1.1.7: right-side label, friendly ground-feature names, no rings on graves

- HUD label moved from top-center to the top-right corner (user preference; the
  top-center spot collides with event/notice text). Right-aligned, anchored 20 px
  from the right edge; word wrap on with the rect width clamped to the canvas, so
  long lines ("Odin's Tomb (-435,-142) - resets in 9d 3h") wrap to extra lines
  growing downward instead of running off the screen. HudOffsetY now means the
  offset below the top edge on the right side - raise it if it overlaps the
  minimap or right-side UI mods.
- Field log showed VLR stamps nearly every zone location on the Stormer server
  (Grave1, InfestedTree01, SwampHut1/2/5, SwampRuin1, StoneTower1,
  StoneTowerRuins09, Dolmen01, ShipSetting01, Runestone_*, WoodHouse2/4/5,
  Crypt4, ...), so the timer label was firing for all of them with raw prefab
  names ("Grave1 (-435,-142) - resets in ...") - and kept cycling the whole way
  home through meadows, which is why the text "kept updating after leaving the
  swamp". Ground features now get friendly names (Grave, Infested Tree, Swamp
  Hut, Swamp Ruin, Stone Tower, Ruined Tower, Shipwreck, Dolmen, Runestone,
  Abandoned House, Goblin Camp); unknown Runestone_*/StoneTowerRuins* variants
  collapse by prefix.
- Ring bug from the same log: ground features (Grave1, InfestedTree01,
  SwampHut2, SwampRuin1) each got a phantom 45.3 m ring - the same radius as
  crypts. ZNetScene.GetPrefab does not resolve their location prefabs, so the
  radius fallback borrowed a neighbouring crypt's dungeon generator (dense
  swamp = always one within the 120 m bound). Location prefabs now resolve
  through ZoneSystem's own location registry (GetPrefab kept as secondary), so
  ground features correctly resolve to "no ring"; the generator fallback only
  remains for genuinely unresolvable names ("Dungeon" placeholder, DG_*).

## 2026-10-06 — v1.1.6: fix duplicate HUD labels (v1.1.5 field regression)

- First field session with v1.1.5's clone-based label showed timer text twice (red
  top-center + brown right side, sometimes running off the right screen edge) with
  copies persisting long after leaving the dungeon. Session log: "HUD label ready"
  fired 34 times in one session (should be once per world), sources alternating
  between a HUD mod's 'Name' and 'TimeText' panels, each creation followed by
  "Can't remove CanvasRenderer because TextMeshProUGUI depends on it".
- Cause: the label was created by cloning some other panel's text as its SIBLING
  and stripping non-TMP components (including the CanvasRenderer TMP needs).
  Parenting into foreign panels means the label inherits their color (brown/red),
  anchors to the panel's rect instead of the screen (off-screen), and dies or
  gets orphaned when those panels rebuild/pool — orphaned copies are unreachable
  by Hide(), hence the persistent text.
- Fix: only the FONT is taken from a donor label (event-name font if loaded, else
  any actively rendering label). The label itself is built from scratch as a
  direct child of the HUD canvas root — true top-center-of-screen anchoring, no
  component stripping, no CanvasRenderer errors. Style (size + color) comes
  consistently from the event-name element instead of whatever panel donated the
  font. Stale copies from earlier HUD rebuilds are swept by name on recreation so
  only one label can ever exist.

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
