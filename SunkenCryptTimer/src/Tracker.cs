using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SunkenCryptTimer
{
    /// <summary>
    /// Scans for VLR-tracked dungeon entrances around the local player and feeds both
    /// the HUD label (nearest one) and the zone rings (all in range). Runs on a
    /// throttled Update (default 1 s); all game state it reads (LocationProxy ZDOs,
    /// EnvMan time) is already client-side.
    /// </summary>
    internal class Tracker : MonoBehaviour
    {
        private float _nextScan;
        private readonly HashSet<int> _loggedUntracked = new HashSet<int>();
        private readonly List<NearbyDungeon> _nearby = new List<NearbyDungeon>();

        // Cached reflection for EnvMan.m_totalSeconds (protected-internal).
        private static readonly FieldInfo TotalSecondsField = GetEnvField("m_totalSeconds");
        private static bool _totalSecondsFallbackWarned;

        private void Update()
        {
            if (!SunkenCryptTimerPlugin.CeEnabled.Value)
            {
                HudLabel.Hide();
                ZoneRing.ClearAll();
                return;
            }

            // Refresh on the scan interval only; countdowns move in minutes, not frames.
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + Mathf.Max(0.25f, SunkenCryptTimerPlugin.CeUpdateInterval.Value);

            var player = Player.m_localPlayer;
            if (player == null || ZoneSystem.instance == null || EnvMan.instance == null ||
                ZNetScene.instance == null)
            {
                HudLabel.Hide();
                ZoneRing.ClearAll();
                return;
            }

            _nearby.Clear();
            CollectTrackedDungeons(player.transform.position, _nearby);
            UpdateHudLabel();
            ZoneRing.UpdateRings(player.transform.position, _nearby);
        }

        /// <summary>
        /// Shows the countdown for the nearest tracked dungeon within ScanRadiusMeters.
        /// Unchanged from v1.0.0 except that the nearest feature now comes from the
        /// shared scan list instead of a dedicated nearest-only lookup.
        /// </summary>
        private void UpdateHudLabel()
        {
            NearbyDungeon? result = null;
            foreach (var feature in _nearby)
            {
                if (result == null || feature.Distance < result.Value.Distance)
                {
                    result = feature;
                }
            }

            if (result == null || result.Value.Distance > SunkenCryptTimerPlugin.CeScanRadius.Value)
            {
                if (SunkenCryptTimerPlugin.CeDebug.Value && result != null)
                {
                    SunkenCryptTimerPlugin.Log.LogDebug(
                        $"Nearest tracked dungeon {result.Value.PrefabName} at {result.Value.Distance:F0}m " +
                        $"(> {SunkenCryptTimerPlugin.CeScanRadius.Value:F0}m radius), hiding label.");
                }
                HudLabel.Hide();
                return;
            }

            string name = result.Value.PrefabName;
            int lastResetDay = result.Value.LastResetDay;
            int resetDays = ResetTimer.GetResetDays(name);
            double remainingDays = lastResetDay + resetDays - CurrentFractionalDay();

            string text = remainingDays > 0d
                ? $"{ResetTimer.PrettyName(name)} - resets in {FormatRemaining(remainingDays)}"
                : $"{ResetTimer.PrettyName(name)} - reset ready (regenerates on approach)";

            if (SunkenCryptTimerPlugin.CeDebug.Value)
            {
                SunkenCryptTimerPlugin.Log.LogDebug(
                    $"{name}: lastResetDay={lastResetDay} resetDays={resetDays} " +
                    $"now={CurrentFractionalDay():F2} remaining={remainingDays:F2} -> \"{text}\"");
            }

            HudLabel.Show(text);
        }

        internal readonly struct NearbyDungeon
        {
            public readonly string PrefabName;
            public readonly int LastResetDay;
            public readonly float Distance;
            public readonly Vector3 Position;
            public readonly long Uid;

            public NearbyDungeon(string prefabName, int lastResetDay, float distance, Vector3 position, long uid)
            {
                PrefabName = prefabName;
                LastResetDay = lastResetDay;
                Distance = distance;
                Position = position;
                Uid = uid;
            }
        }

        /// <summary>
        /// All LocationProxies carrying a VV_LastReset stamp (i.e. managed by VLR),
        /// regardless of distance; callers apply their own display radius. Untracked
        /// proxies (server without VLR, or zone not yet initialized by its watcher)
        /// are skipped, logged once per proxy.
        /// </summary>
        private void CollectTrackedDungeons(Vector3 playerPos, List<NearbyDungeon> results)
        {
            foreach (var proxy in FindObjectsOfType<LocationProxy>())
            {
                var nview = proxy.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;
                var zdo = nview.GetZDO();
                if (zdo == null) continue;

                int lastResetDay = zdo.GetInt(SunkenCryptTimerPlugin.VlrLastResetField, -1);
                if (lastResetDay < 0)
                {
                    if (SunkenCryptTimerPlugin.CeDebug.Value && _loggedUntracked.Add((int)zdo.m_uid.ID))
                    {
                        SunkenCryptTimerPlugin.Log.LogDebug(
                            $"LocationProxy {zdo.m_uid} has no {SunkenCryptTimerPlugin.VlrLastResetField} " +
                            "stamp (server without Venture Location Reset, or timer not started yet).");
                    }
                    continue;
                }

                string prefabName = zdo.GetString(ZDOVars.s_location, "");
                if (string.IsNullOrEmpty(prefabName))
                {
                    prefabName = ResolveNameFromZoneSystem(proxy.transform.position);
                }
                results.Add(new NearbyDungeon(
                    prefabName, lastResetDay,
                    Vector3.Distance(playerPos, proxy.transform.position),
                    proxy.transform.position,
                    zdo.m_uid.ID));
            }
        }

        /// <summary>Fallback name lookup: match the proxy position to a known zone location instance.</summary>
        private static string ResolveNameFromZoneSystem(Vector3 position)
        {
            var instances = ZoneSystem.instance.m_locationInstances;
            string bestName = "Dungeon";
            float bestDist = float.MaxValue;
            foreach (var kv in instances)
            {
                var inst = kv.Value;
                if (inst.m_location == null) continue;
                float dist = Vector3.Distance(position, inst.m_position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestName = inst.m_location.m_prefabName;
                }
            }
            return bestName;
        }

        /// <summary>
        /// Current day as a fraction, on the same scale EnvMan.GetCurrentDay() truncates
        /// (whole days). Mirrors VLR, which stores GetCurrentDay() at reset time, so
        /// lastResetDay + resetDays is the exact fractional day the reset becomes due.
        /// Note: GetCurrentDay() is protected-internal in current Valheim builds, hence
        /// the reflection on m_totalSeconds; the constant +1 offset cancels out between
        /// the stored and current values, so the countdown is exact either way.
        /// </summary>
        internal static double CurrentFractionalDay()
        {
            long dayLength = EnvMan.instance.m_dayLengthSec;
            if (dayLength <= 0) return 1d;

            double totalSeconds = ReadTotalSeconds();
            return totalSeconds / dayLength + 1d;
        }

        private static double ReadTotalSeconds()
        {
            if (TotalSecondsField != null && EnvMan.instance != null)
            {
                return (double)TotalSecondsField.GetValue(EnvMan.instance);
            }
            if (!_totalSecondsFallbackWarned)
            {
                _totalSecondsFallbackWarned = true;
                SunkenCryptTimerPlugin.Log.LogWarning(
                    "EnvMan.m_totalSeconds not found via reflection; falling back to ZNet.GetTimeSeconds(). " +
                    "Countdown may be offset by less than a day.");
            }
            return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0d;
        }

        private static FieldInfo GetEnvField(string name)
        {
            return typeof(EnvMan).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        internal static string FormatRemaining(double remainingDays)
        {
            if (remainingDays >= 1d)
            {
                int days = (int)remainingDays;
                int hours = (int)((remainingDays - days) * 24d);
                return $"{days}d {hours}h";
            }
            int totalHours = (int)(remainingDays * 24d);
            if (totalHours >= 1)
            {
                int minutes = (int)((remainingDays * 24d - totalHours) * 60d);
                return $"{totalHours}h {minutes}m";
            }
            return $"{(int)(remainingDays * 24d * 60d)}m";
        }
    }
}
