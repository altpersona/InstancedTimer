using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InstancedTimer
{
    /// <summary>
    /// Draws one workbench-style ground ring around each nearby VLR-tracked instanced
    /// dungeon, sized to the radius Venture Location Reset checks for that feature
    /// (hypot(zoneSize/2) of its DungeonGenerator - 45.25 m for sunken crypts). Rings
    /// are clones of the vanilla CraftingStation AreaMarker (CircleProjector), so they
    /// look and behave exactly like the workbench build ring: thin slices raycast onto
    /// the terrain, slowly rotating. Purely visual client-side effects - nothing is
    /// written to any ZDO and the shared station_radius material is left untouched.
    /// Static like HudLabel: the ring container lives at scene root and dies with the
    /// scene, so world changes clean up automatically.
    /// </summary>
    internal static class ZoneRing
    {
        private const int MaxRings = 4;
        private const float EntryTimeoutSeconds = 30f;
        internal const float SkyY = 4000f;
        private const string TemplatePrefabPrimary = "piece_workbench";
        private const string TemplatePrefabFallback = "guard_stone";

        private class RingEntry
        {
            public GameObject Root;
            public CircleProjector Projector;
            public long Uid;
            public float Radius;
            public float LastSeen;
            public bool SeenThisTick;
        }

        private static readonly Dictionary<long, RingEntry> _entries = new Dictionary<long, RingEntry>();
        // prefabName -> radius; only stored once confirmed live, otherwise recomputed.
        private static readonly Dictionary<string, float> _radiusCache = new Dictionary<string, float>();
        private static readonly HashSet<string> _warnedPrefabs = new HashSet<string>();
        private static readonly HashSet<string> _loggedRadius = new HashSet<string>();
        private static readonly List<Tracker.NearbyDungeon> _candidates = new List<Tracker.NearbyDungeon>();
        private static readonly List<RingEntry> _sweep = new List<RingEntry>();

        private static GameObject _root;       // scene root; destroyed with the scene on world change
        private static Transform _template;    // AreaMarker child, re-fetched per container lifetime
        private static bool _templateWarned;

        /// <summary>
        /// One throttled tick from Tracker: shows, hides, and re-spaces the rings for
        /// all tracked features within Rings/ShowRadiusMeters of the player.
        /// </summary>
        internal static void UpdateRings(Vector3 playerPos, List<Tracker.NearbyDungeon> nearby)
        {
            if (!InstancedTimerPlugin.CeRingEnabled.Value)
            {
                ClearAll();
                return;
            }

            if (EnsureRoot() == null)
            {
                return; // template unavailable (warned once); no rings this world
            }

            float showRadius = Mathf.Max(10f, InstancedTimerPlugin.CeRingShowRadius.Value);
            bool playerInInterior = playerPos.y >= SkyY; // hide while inside a dungeon

            _candidates.Clear();
            foreach (var feature in nearby)
            {
                if (feature.Distance <= showRadius)
                {
                    _candidates.Add(feature);
                }
            }

            if (_candidates.Count > MaxRings)
            {
                _candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                _candidates.RemoveRange(MaxRings, _candidates.Count - MaxRings);
            }

            foreach (var feature in _candidates)
            {
                float radius = ResolveRadius(feature.PrefabName, feature.Position);
                if (radius <= 0f)
                {
                    continue; // ground feature or unresolvable prefab: no ring
                }

                var entry = GetOrCreateEntry(feature.Uid, feature.PrefabName, feature.Position, radius);
                if (entry == null)
                {
                    continue;
                }

                entry.SeenThisTick = true;
                entry.LastSeen = Time.time;
                if (!Mathf.Approximately(entry.Radius, radius))
                {
                    entry.Projector.m_radius = radius; // public field; Update() re-spaces the slices
                    entry.Radius = radius;
                }
                entry.Root.SetActive(!playerInInterior);

                if (InstancedTimerPlugin.CeDebug.Value)
                {
                    InstancedTimerPlugin.Log.LogDebug(
                        $"ring {feature.PrefabName} uid={feature.Uid} r={radius:F1} dist={feature.Distance:F0}m");
                }
            }

            SweepUnseen();
        }

        internal static void ClearAll()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
            _entries.Clear();
        }

        /// <summary>
        /// Lazily creates the scene-root container and fetches the AreaMarker template
        /// from the live ZNetScene. A destroyed root (world change) resets the state so
        /// the next world rebuilds from scratch.
        /// </summary>
        private static GameObject EnsureRoot()
        {
            if (_root != null)
            {
                return _root; // Unity-null comparison: destroyed with the old scene
            }

            _entries.Clear();
            _template = null;
            _locationPrefabs = null; // rebuilt from the new world's ZoneSystem

            var prefab = ZNetScene.instance.GetPrefab(TemplatePrefabPrimary) ??
                ZNetScene.instance.GetPrefab(TemplatePrefabFallback);
            var marker = prefab != null ? prefab.transform.Find("AreaMarker") : null;
            if (marker == null)
            {
                if (!_templateWarned)
                {
                    _templateWarned = true;
                    InstancedTimerPlugin.Log.LogWarning(
                        $"No AreaMarker template found ({TemplatePrefabPrimary}/{TemplatePrefabFallback}); " +
                        "zone rings disabled until the next world.");
                }
                return null;
            }

            _template = marker;
            _root = new GameObject("InstancedTimer.Rings"); // scene root: dies on logout to menu
            InstancedTimerPlugin.Log.LogInfo($"zone rings: template ready ({prefab.name}/{marker.name})");
            if (InstancedTimerPlugin.CeDebug.Value)
            {
                InstancedTimerPlugin.Log.LogDebug($"ring container created, template from {prefab.name}");
            }
            return _root;
        }

        private static RingEntry GetOrCreateEntry(long uid, string prefabName, Vector3 position, float radius)
        {
            if (_entries.TryGetValue(uid, out var entry))
            {
                return entry;
            }

            var go = Object.Instantiate(_template.gameObject, _root.transform, false);
            go.name = $"Ring_{prefabName}_{uid}";
            go.transform.position = position; // proxy ground position; slices raycast down to terrain
            go.SetActive(false);               // stay inert until radius is set
            // The AreaMarker subtree is inactive in the workbench prefab and
            // carries leftover meshes (a floating hammer icon among them) -
            // keep them off; CircleProjector spawns its own slice children.
            foreach (Transform child in go.transform)
            {
                child.gameObject.SetActive(false);
            }
            var projector = go.GetComponent<CircleProjector>();
            projector.m_radius = radius;       // set before first Start() so segments spawn correctly

            entry = new RingEntry
            {
                Root = go,
                Projector = projector,
                Uid = uid,
                Radius = radius,
                LastSeen = Time.time
            };
            _entries[uid] = entry;

            InstancedTimerPlugin.Log.LogInfo(
                $"ring created: {prefabName} uid={uid} r={radius:F1}m at ({position.x:F0}, {position.z:F0})");
            return entry;
        }

        /// <summary>
        /// Radius VLR checks for a feature type: hypot(zoneSize.x/2, zoneSize.z/2) of
        /// its DungeonGenerator (VLR GetDungeonRadius), from the location prefab -
        /// either Location.m_generator or, for prefabs like SunkenCrypt4 where that is
        /// null, the generator on m_interiorPrefab. Only instanced dungeons
        /// (m_hasInterior) get rings; everything else returns 0. When the feature's
        /// interior is loaded, the live scene-root generator confirms the prefab value
        /// (VLR parity); only confirmed values are cached.
        /// </summary>
        private static float ResolveRadius(string prefabName, Vector3 featurePos)
        {
            float radius = ComputeRadius(prefabName, featurePos);
            if (_loggedRadius.Add(prefabName))
            {
                InstancedTimerPlugin.Log.LogInfo(radius > 0f
                    ? $"ring radius: {prefabName} {radius:F1}m"
                    : $"ring radius: {prefabName} none (not an instanced dungeon or prefab unresolved)");
            }
            return radius;
        }

        private static float ComputeRadius(string prefabName, Vector3 featurePos)
        {
            var location = FindLocationPrefab(prefabName);
            if (location == null || !location.m_hasInterior)
            {
                if (location != null)
                {
                    return 0f; // known GROUND location (grave/tree/ruin): never a ring
                }
                // Name unresolved (placeholder "Dungeon" or a "DG_*" generator
                // name): the live root generator near the feature IS the
                // interior - take the radius straight from it. Bound kept
                // tight: in dense regions a generous bound borrows a
                // neighbouring dungeon's generator for ground features.
                var live = FindRootGenerator(featurePos, 120f);
                return live != null && live.transform.position.y >= SkyY
                    ? HalfDiagonal(live.m_zoneSize)
                    : 0f;
            }

            if (_radiusCache.TryGetValue(prefabName, out float cached))
            {
                return cached;
            }

            var generator = location.m_generator != null
                ? location.m_generator
                : FindPrefabGenerator(location.m_interiorPrefab);
            float radius = generator != null ? HalfDiagonal(generator.m_zoneSize) : location.m_interiorRadius;

            // Live confirmation (VLR reads the runtime generator): a root generator at
            // interior altitude above this feature wins over the prefab value.
            var dg = FindRootGenerator(featurePos, Mathf.Max(radius, location.m_exteriorRadius, location.m_interiorRadius));
            if (dg != null && dg.transform.position.y >= SkyY)
            {
                float live = HalfDiagonal(dg.m_zoneSize);
                if (Mathf.Abs(live - radius) > 0.25f)
                {
                    if (InstancedTimerPlugin.CeDebug.Value)
                    {
                        InstancedTimerPlugin.Log.LogDebug(
                            $"{prefabName}: live generator radius {live:F1}m overrides prefab value {radius:F1}m");
                    }
                    radius = live;
                }
                _radiusCache[prefabName] = radius; // terminal: confirmed against the running game
            }
            // Unconfirmed values are intentionally not cached - rechecked until the
            // interior loads and the live generator can confirm them.

            if (radius <= 0.5f && _warnedPrefabs.Add(prefabName))
            {
                InstancedTimerPlugin.Log.LogDebug($"no ring radius for {prefabName} (no generator on prefab).");
            }
            return radius;
        }

        private static DungeonGenerator FindPrefabGenerator(GameObject interiorPrefab)
        {
            return interiorPrefab != null ? interiorPrefab.GetComponentInChildren<DungeonGenerator>() : null;
        }

        // Location prefab registry built from ZoneSystem's own location list,
        // with ZNetScene.GetPrefab as secondary. ZNetScene alone did not
        // resolve ground-location prefabs in the field (v1.1.5: every Grave/
        // SwampHut/InfestedTree "resolved" to null, fell into the generator
        // fallback below and borrowed a neighbouring crypt's 45 m radius).
        // Location prefabs are SoftReferences that load lazily, so a name
        // miss rebuilds the registry once before giving up on it.
        private static Dictionary<string, Location> _locationPrefabs;

        private static Location FindLocationPrefab(string name)
        {
            if (_locationPrefabs == null || !_locationPrefabs.ContainsKey(name))
            {
                _locationPrefabs = new Dictionary<string, Location>();
                foreach (var zoneLocation in ZoneSystem.instance.m_locations)
                {
                    var prefabRef = zoneLocation.m_prefab;
                    if (prefabRef == null || !prefabRef.IsLoaded) continue;
                    var prefab = prefabRef.Asset;
                    var loc = prefab != null ? prefab.GetComponent<Location>() : null;
                    if (loc != null && !_locationPrefabs.ContainsKey(prefab.name))
                    {
                        _locationPrefabs.Add(prefab.name, loc);
                    }
                }
            }
            if (_locationPrefabs.TryGetValue(name, out var found))
            {
                return found;
            }
            var netPrefab = ZNetScene.instance.GetPrefab(name);
            return netPrefab != null ? netPrefab.GetComponent<Location>() : null;
        }

        /// <summary>
        /// First scene-root DungeonGenerator within bound (horizontal) of center -
        /// VLR's GetDungeonGeneratorInBounds, used to confirm prefab radius values
        /// and as the fallback name/radius source for instanced dungeons whose
        /// proxy carries no name (VLR regen leaves instance ref and ZDO empty).
        /// </summary>
        internal static DungeonGenerator FindRootGenerator(Vector3 center, float bound)
        {
            foreach (var obj in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var dg = obj.GetComponent<DungeonGenerator>();
                if (dg != null && Utils.DistanceXZ(obj.transform.position, center) <= bound)
                {
                    return dg;
                }
            }
            return null;
        }

        private static float HalfDiagonal(Vector3 zoneSize)
        {
            return new Vector2(zoneSize.x * 0.5f, zoneSize.z * 0.5f).magnitude;
        }

        private static void SweepUnseen()
        {
            _sweep.Clear();
            foreach (var entry in _entries.Values)
            {
                if (entry.SeenThisTick)
                {
                    entry.SeenThisTick = false;
                    continue;
                }

                if (entry.Root != null)
                {
                    entry.Root.SetActive(false);
                    if (Time.time - entry.LastSeen > EntryTimeoutSeconds)
                    {
                        _sweep.Add(entry);
                    }
                }
                else
                {
                    _sweep.Add(entry); // destroyed behind our back; drop the bookkeeping
                }
            }

            foreach (var entry in _sweep)
            {
                if (entry.Root != null)
                {
                    Object.Destroy(entry.Root);
                }
                _entries.Remove(entry.Uid);
            }
        }
    }
}
