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
