# InstancedTimer

A **client-only** Valheim mod for players on servers that run
[Venture Location Reset](https://thunderstore.io/c/valheim/p/OrianaVenture/Venture_Location_Reset/) (VLR).

It answers two questions at a glance:

- **"When does this dungeon reset?"** — a HUD countdown
  (*"Sunken Crypt — resets in 2d 14h"*) whenever you are near a dungeon entrance:
  sunken crypts, burial chambers, troll caves, frost caves, goblin camps, dvergr mines,
  Hildir's dungeons, Ashlands fortresses, and more.
- **"How close is too close?"** — a workbench-style **ground ring** around each nearby
  instanced dungeon at the radius VLR's player-activity check uses for it
  (45 m for sunken crypts), so you can see the footprint where player-made structures
  or a tombstone would block the dungeon from ever resetting.

No server changes, no Harmony patches, no world-data writes: the mod only *reads* the
`VV_LastReset` stamps VLR leaves on each dungeon's world object — those sync to every
connected client automatically.

## Requirements

| Component | Needed? | Notes |
|---|---|---|
| Valheim | on your client | Built/tested against Valheim **1.0.16** (Unity 6 build). Older versions untested. |
| BepInEx | on your client | Tested with `denikson-BepInExPack_Valheim 5.4.2351` (BepInEx 5.4.22+). |
| Venture Location Reset | **on the server** | The mod displays VLR's reset data; without VLR on the server there is simply nothing to show. Any recent 1.x. |
| VLR on your client | optional | If installed (it usually is on modded servers), the countdown uses the server's *live* reset intervals, synced through Jotunn. If not, set `FallbackResetDays` to match your server. |
| Jotunn | not required | This mod does not reference Jotunn or any other package. |

The mod does **not** need to be installed on the server (installing it there is
harmless but does nothing — servers have no HUD). Other players don't see your rings
or your timer unless they also run the mod.

## Installation

**With a mod manager** (r2modman / Gale / Thunderstore Mod Manager): search for
"InstancedTimer" and install. Done.

**Manual**: copy `Plugins/InstancedTimer.dll` from the package into your BepInEx
profile's `BepInEx/plugins/` folder.

## Usage

### Timer

Walk near any location VLR manages (default: within 80 m of the entrance) - dungeons
primarily, but on servers where VLR stamps everything (its default), runestones,
graves, shipwrecks and ruins get counted down too. A single line appears in the
top-right corner of the HUD, right below the status-effect row (Rested/Wet), in the
same small text size:

- `Sunken Crypt - resets in 2d 14h` — the dungeon still holds its loot.
- `Sunken Crypt - reset ready (regenerates on approach)` — the timer has elapsed.
  VLR only regenerates a dungeon **when a player walks within 100 m** of it after the
  timer expires, so it will pop fresh the moment someone (maybe you) shows up.

Timers keep running while the server is empty (the world clock advances 24/7 while the
server process runs), and the countdown is computed from live world time — log back in
after a week and the label already reflects it.

### Zone rings

Each nearby *instanced* dungeon (crypt, burial chamber, troll cave, ...) gets a ground
ring cloned from the vanilla workbench build-circle: thin rotating dashes hugging the
terrain. Its radius is the distance VLR checks for player activity **inside** that
dungeon type — 45 m for sunken crypts, computed per type from the game's own dungeon
data (`hypot(zoneSize.x/2, zoneSize.z/2)`, matching VLR's `GetDungeonRadius`).

Rules of thumb the ring makes visible:

- Build or die **inside the ring's area** (or within the small entrance zone around the
  door) and that dungeon stops resetting until the pieces/tombstones are removed.
- A base on the ground outside the ring is safe and never blocks resets.
- Terrain shaping (raising/leveling) never blocks a reset, but VLR reverts terrain
  within ~12 m of the entrance each time the dungeon resets.
- Rings show while you're within 100 m (VLR's own trigger distance), hide while you
  are inside the dungeon, and are capped at the 4 nearest features. Ground locations
  (meadow farms, villages, fuling camps) intentionally get no ring.

## Configuration (`BepInEx/config/lan124.InstancedTimer.cfg`)

All settings are client-side; changes apply immediately (no restart).

| Section | Key | Default | Meaning |
|---|---|---|---|
| General | Enabled | true | Master switch |
| General | DebugLogging | false | Verbose scanner logging |
| HUD | ScanRadiusMeters | 80 | Show the timer within this distance of an entrance |
| HUD | UpdateIntervalSeconds | 1 | Rescan rate |
| HUD | HudOffsetY | 170 | Fallback label offset from the top of the screen (px) - normally the label positions itself under the status-effect row |
| HUD | TextScale | 1.25 | Timer text size relative to the status-effect labels (1 = same size); applies live, no restart |
| HUD | InstanceNaming | NameAndZone | How the label names the location: Type, NameAndZone ("Odin's Tomb (-70,-21)"), Name, or Zone |
| Timers | FallbackResetDays | 10 | Assumed interval when VLR's config can't be read — set to your server's actual interval |
| Rings | Enabled | true | Draw ground rings around nearby instanced dungeons |
| Rings | ShowRadiusMeters | 100 | Show a ring while within this distance of the entrance |

## How it works

VLR stamps every dungeon it manages with `VV_LastReset` (the in-game day it last reset)
on the dungeon's `LocationProxy` world object (ZDO). ZDOs sync to every connected
client, so this mod:

1. scans nearby `LocationProxy` objects for the stamp (throttled, default 1 s),
2. resolves the effective reset interval from VLR's own live config on the client
   (which Jotunn keeps synced with the server's settings) or falls back to
   `FallbackResetDays`,
3. displays `lastReset + interval − now`, mirroring VLR's exact day-math, and
4. (rings) clones the vanilla `CircleProjector` marker used by the workbench and sizes
   it per dungeon type from the game's location prefab data, confirmed against the
   live dungeon generator.

The mod never writes world data, owns no networked objects, and patches nothing.

## Building from source

Requires the .NET SDK (6+) and a local Valheim install + BepInEx profile:

```
./build.sh -p:GameDir=/path/to/Valheim -p:ProfileDir=/path/to/BepInExProfileRoot
```

`GameDir` is the folder containing `valheim_Data/`; `ProfileDir` is the folder
containing `BepInEx/core`. Output: `InstancedTimer/bin/Release/InstancedTimer.dll`.
`./package.sh` additionally builds the Thunderstore zip (requires `zip`). For the
full release/upload workflow see [PUBLISHING.md](PUBLISHING.md).

## Version compatibility

| | Version | Status |
|---|---|---|
| Valheim | 1.0.16 (Unity 6000.0.75f1) | tested, daily use |
| Valheim | older / newer patch levels | untested; the mod only uses stable APIs plus two reflective reads (`EnvMan.m_totalSeconds`, `Hud.m_instance`) — likely fine, not guaranteed |
| BepInEx pack (denikson) | 5.4.2351 | tested |
| Venture Location Reset | 1.1.0 / 1.1.1 | tested; interval mapping mirrors VLR's `GetResetTime` (re-check when VLR adds location types) |
| Jotunn | any / absent | not referenced |

Known limitation: the per-type reset-interval table in `src/ResetTimer.cs` only takes
effect when VLR's per-type override mode is on; otherwise VLR's global `ResetTime`
applies to everything (as on most servers).

## License

[MIT](LICENSE)
