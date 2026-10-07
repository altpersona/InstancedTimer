# LESSONS — accumulated gotchas (append-only, never pruned)

## [2026-10-05] Valheim 1.0.16 / Unity 6 class renames break old mod snippets
Symptom: References to `AreaMarker`, `Workbench`, `Ward` types fail; docs/decompiles
online still use the old names.
Root cause: This build (engine 6000.0.75f1) renamed them: ring renderer is
`CircleProjector`, workbench logic is `CraftingStation`, ward is `PrivateArea`.
Fix: Dump `TypeDef` names from `assembly_valheim.dll` with `dnfile` (pip) instead of
trusting old decompiles.
Prevent: Verify type names against the local DLL (or `tools/apiscan`) before writing
game-API code.

## [2026-10-05] Location.m_generator is NULL on SunkenCrypt4
Symptom: Reading `location.m_generator.m_zoneSize` for the ring radius NREs / falls to
wrong fallback for sunken crypts.
Root cause: On the SunkenCrypt4 prefab the DungeonGenerator lives on
`Location.m_interiorPrefab` (`DG_SunkenCrypt`, m_zoneSize 64×64); `m_generator` PPtr is
(0,0). Other location prefabs may differ.
Fix: `generator = loc.m_generator ?? loc.m_interiorPrefab.GetComponentInChildren<DungeonGenerator>()`
(see `src/ZoneRing.cs` `ResolveRadius`).
Prevent: Dump the actual prefab before assuming component wiring.

## [2026-10-05] Dumping vanilla prefabs: SoftRef bundles + UnityPy recipe
Symptom: Location prefabs are not in `resources.assets`; grepping *.assets finds nothing.
Root cause: Content lives in content-addressed bundles: `valheim_Data/StreamingAssets/
SoftRef/manifest_extended` maps prefab path → 8-hex bundle hash under
`SoftRef/Bundles/`.
Fix: UnityPy (`pip install UnityPy`) `load(bundle, *dep_bundles)` then
`obj.read_typetree()` — typetrees ARE embedded. Dep-bundle hashes come from
`SoftRef/manifest`, and its section header is `dependencies:[ \t]*\n` (trailing space —
a strict regex silently matches nothing, which looks like "no dependencies").
MonoScript resolution needs the dep bundles loaded. `dnfile` gives .NET field
lists/orders from `assembly_valheim.dll` (`td.FieldList` items are `MDTableIndex` — use
`.row` or `field_rows[row_index-1]`).
Prevent: N/A — keep this recipe; no decompiler exists on this machine and NuGet tool
installs fail (`ilspycmd` not found).

## [2026-10-05] Unity fake-null vs C# null in mod code
Symptom: `??` on UnityEngine.Object references can skip Unity's destroyed-object
detection; conversely `_root != null` on a scene-destroyed GameObject correctly reads
false via Unity's overloaded ==.
Root cause: `??`/`?.` use reference equality only; Unity overloads `==`/`!=` for
destroyed objects.
Fix: In `ZoneRing.cs`: `GetPrefab(...) ?? fallback` is safe (lookup returns true null
when missing); container/entry objects use `!= null` (Unity overload) so scene teardown
is detected.
Prevent: Audit every Unity-object null check for which kind of null it can be.

## [2026-10-05] Cache only verified values (plan-review catch)
Symptom: Plan draft would have terminally cached a radius derived from a fallback
(`m_interiorRadius=14`) whenever the live generator hadn't loaded yet → wrong ring
size forever.
Root cause: Prefab read can be wrong for unverified wiring; live scene generator is the
ground truth but only exists once the interior loads.
Fix: `_radiusCache` stores a value only after live confirmation (scene-root
`DungeonGenerator` at y≥4000 within bounds); unconfirmed values recompute each tick.
Prevent: Never make "cheap substitute" reads terminal when a "ground truth" read exists
but may be lazily available.

## [2026-10-06] Thunderstore API: tss_ tokens are Bearer, and a missing trailing slash is the real 502
Symptom: POST /api/experimental/submission/upload/ returned Cloudflare 502 (HTML) from
a script while an "identical" manual curl succeeded; Swagger docs claim HTTP Basic.
Root cause: TWO separate issues conflated during debugging. (1) tss_ service-account
tokens authenticate via `Authorization: Bearer <token>` (see Thunderstore source
account/authentication.py), not Basic — the docs' securityDefinitions are wrong.
(2) The script's URL lacked the trailing slash; Django's APPEND_SLASH redirect behind
Cloudflare collapses into a 502 for this POST. The User-Agent/Expect:100-continue
headers were NOT the fix (harmless, kept in script).
Fix: Bearer auth + exact URL with trailing slash. Verified by re-running against a
published version: correct transport returns the API's 400 "version already exists".
Prevent: Copy endpoint URLs verbatim from the OpenAPI spec (/?format=openapi — the
docs page itself is a JS shell). Note: package deprecation is blocked for service
accounts ("Service accounts are unable to perform this action") — browser-only.

## [2026-10-06] Never clone another mod's UI panels when adding HUD elements (v1.1.5 regression)
Symptom: Timer label appeared twice (red top-center + brown right side, partly
off-screen), stale copies persisted after leaving the area, and every recreation logged
"Can't remove CanvasRenderer because TextMeshProUGUI depends on it" — 34 label
creations in one session (should be 1 per world).
Root cause: HudLabel cloned a donor label (scene-scan fallback picked other mods'
'Name'/'TimeText' panels) as its SIBLING and stripped non-TMP components (incl. the
CanvasRenderer TMP requires). The clone inherited the panel's color/anchors (panel-
relative top-center ≠ screen top-center → off-screen) and died or got orphaned when
those panels rebuilt/pooled — orphans unreachable by Hide().
Fix: Build the label from scratch (AddComponent auto-adds CanvasRenderer — nothing to
strip) as a direct child of the HUD canvas root; take only the FONT from a donor;
fixed style (size/color) from one canonical element; sweep stale copies by name.
Prevent: UI mods own their panels' lifecycle — never parent into (or clone) foreign
UI; anchor to the canvas root so anchors mean screen coordinates.

## [2026-10-06] Location prefabs resolve through ZoneSystem, not ZNetScene.GetPrefab
Symptom: Ground locations (Grave1, SwampHut*, InfestedTree01, SwampRuin1) each got a
phantom 45.3 m ring — the exact crypt radius.
Root cause: `ZNetScene.GetPrefab(name)` does not resolve location prefabs reliably, so
the ring-radius fallback borrowed a NEIGHBOURING crypt's DungeonGenerator (dense swamp
= one always within the 120 m bound). Location prefabs actually live in
`ZoneSystem.instance.m_locations` as `SoftReference<GameObject>` (lazy-loaded; use
`.IsLoaded` / `.Asset`; requires a csproj reference to SoftReferenceableAssets.dll —
the SoftReference<> type lives there, CS0012 otherwise).
Fix: ZoneRing.FindLocationPrefab builds a name→Location registry from
ZoneSystem.m_locations (GetPrefab kept as secondary); resolved ground locations
correctly return radius 0 (m_hasInterior=false → no ring). Generator borrowing now only
happens for genuinely unresolvable names ("Dungeon" placeholder, DG_*).
Prevent: For location-type questions, query ZoneSystem's registry first; ZNetScene's
prefab index is for networked prefabs, not authoritative for zone locations.

## [2026-10-06] VLR stamps EVERY zone location — plan UI/naming for all of them
Symptom: The HUD timer fired constantly while traveling ("kept updating after leaving
the swamp"), showing raw prefab names like "Grave1 (-435,-142)".
Root cause: On servers with default VLR config (IgnoreList near-empty), every location
instance — runestones, graves, shipwrecks, swamp huts, ruins, abandoned houses, dolmens
— carries VV_LastReset and resets on the same global timer. The mod's dungeon-centric
naming left all of them ugly.
Fix: PrettyNames covers vanilla ground POIs + prefix rules (Runestone_*→Runestone,
StoneTowerRuins*→Ruined Tower); the label still shows all tracked features by design
(user wants them named, not hidden).
Prevent: When building against VLR data, enumerate what the SERVER actually stamps
(check its config + the client LogOutput.log "tracked dungeon:" lines), not just the
dungeon types VLR's per-type config implies.

## [2026-10-06] TMP fontSize is not the rendered size — never tune with constant bonuses
Symptom: timer text adopted the "status-effect text size" but never matched on screen:
raw copy "too small" (v1.2.0), +2 "still too small" (v1.2.1), +4 landed at 1.5x the
labels (v1.2.2 field screenshot: labels 8px caps, timer 12px).
Root cause: TMP `fontSize` is points before transform scaling; the status-effect
hierarchy carries its own scale, and the mod label sits on the unscaled canvas root.
Copying `fontSize` (plus hand bonuses) compares unlike units — each blind bump
overshoots or undershoots and can never converge.
Fix: take `fontSize` from a live status entry (first active TMP — the same lookup
vanilla's `Hud.UpdateStatusEffects` uses for the name label) and multiply by
`donor.lossyScale.x / label.lossyScale.x` (HudLabel.cs `MatchStatusEffectRow`).
Prevent: when matching an on-screen UI size, match rendered size through world
scale; measure a screenshot before shipping another guess.

## [2026-10-06] Size taste diverges per user — ship a knob, not a guess
Symptom: three consecutive builds tuned timer text size by constant (raw, +2, +4);
each field test reversed the previous verdict (too small → too small → 1.5x →
"small-ish" at exact-match).
Root cause: point-size guessing against a scaled reference cannot converge, and
every iteration costs a build + restart + field test round trip.
Fix: anchor the size to something measurable (v1.2.3 rendered-size match), then
expose the remaining taste as a live config entry (`TextScale`, 1.25 default,
0.5-3 range) that applies without a restart.
Prevent: when a tuning cycle produces contradictory verdicts twice, stop
guessing — make the parameter user-facing and let the field tune it.
