using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace SunkenCryptTimer
{
    /// <summary>
    /// Computes the countdown for a dungeon from its VV_LastReset day stamp and the
    /// effective reset interval. Interval resolution mirrors Venture Location Reset's
    /// GetResetTime (source: github.com/OrianaVenture/VentureValheim, LocationReset/):
    /// if VLR is installed on this client, its live - and on servers, Jotunn-synced -
    /// config entries are read directly, so the countdown always matches what the
    /// server will actually do. Otherwise the mod's own FallbackResetDays is used.
    /// </summary>
    internal static class ResetTimer
    {
        // Location prefab name -> VLR config key (section "Advanced").
        // Copied from VLR's GetResetTime hash mapping; keep in sync with VLR releases.
        private static readonly Dictionary<string, string> TypeConfigKeys = new Dictionary<string, string>
        {
            { "SunkenCrypt4", "CryptResetTime" },
            { "Crypt2", "BurialResetTime" },
            { "Crypt3", "BurialResetTime" },
            { "Crypt4", "BurialResetTime" },
            { "TrollCave02", "TrollResetTime" },
            { "MountainCave02", "CaveResetTime" },
            { "GoblinCamp2", "CampResetTime" },
            { "WoodFarm1", "FarmResetTime" },
            { "WoodVillage1", "VillageResetTime" },
            { "Mistlands_DvergrTownEntrance1", "MineResetTime" },
            { "Mistlands_DvergrTownEntrance2", "MineResetTime" },
            { "Mistlands_DvergrBossEntrance1", "QueenResetTime" },
            { "Hildir_crypt", "HildirCryptResetTime" },
            { "Hildir_cave", "HildirCaveResetTime" },
            { "Hildir_plainsfortress", "HildirTowerResetTime" },
            { "CharredFortress", "CharredFortressResetTime" },
            { "LeviathanLava", "LeviathanLavaResetTime" },
            { "MorgenHole1", "MorgenHoleResetTime" },
            { "MorgenHole2", "MorgenHoleResetTime" },
            { "MorgenHole3", "MorgenHoleResetTime" },
            { "PlaceofMystery1", "PlaceofMysteryResetTime" },
            { "PlaceofMystery2", "PlaceofMysteryResetTime" },
            { "PlaceofMystery3", "PlaceofMysteryResetTime" },
            { "VV_CopperTinCave", "CopperTinCaveResetTime" },
            { "VV_SilverCave", "SilverCaveResetTime" },
        };

        // Prefab name -> friendly name for the HUD line.
        private static readonly Dictionary<string, string> PrettyNames = new Dictionary<string, string>
        {
            { "SunkenCrypt4", "Sunken Crypt" },
            { "Crypt2", "Burial Chamber" },
            { "Crypt3", "Burial Chamber" },
            { "Crypt4", "Burial Chamber" },
            { "TrollCave02", "Troll Cave" },
            { "MountainCave02", "Frost Cave" },
            { "GoblinCamp2", "Goblin Camp" },
            { "WoodFarm1", "Abandoned Farm" },
            { "WoodVillage1", "Abandoned Village" },
            { "Mistlands_DvergrTownEntrance1", "Dvergr Town" },
            { "Mistlands_DvergrTownEntrance2", "Dvergr Town" },
            { "Mistlands_DvergrBossEntrance1", "Dvergr Queen Lair" },
            { "Hildir_crypt", "Hildir's Crypt" },
            { "Hildir_cave", "Hildir's Cave" },
            { "Hildir_plainsfortress", "Hildir's Fortress" },
            { "CharredFortress", "Charred Fortress" },
            { "MorgenHole1", "Morgen Hole" },
            { "MorgenHole2", "Morgen Hole" },
            { "MorgenHole3", "Morgen Hole" },
            { "PlaceofMystery1", "Place of Mystery" },
            { "PlaceofMystery2", "Place of Mystery" },
            { "PlaceofMystery3", "Place of Mystery" },
            { "VV_CopperTinCave", "Copper/Tin Cave" },
            { "VV_SilverCave", "Silver Cave" },
            // Generator GameObject names (fallback when a dungeon's location
            // name is unresolvable - see Tracker/generator via).
            { "DG_SunkenCrypt", "Sunken Crypt" },
            { "DG_BurialChambers", "Burial Chamber" },
            { "DG_TrollCave", "Troll Cave" },
        };

        internal static string PrettyName(string prefabName) =>
            PrettyNames.TryGetValue(prefabName, out var pretty) ? pretty : prefabName;

        /// <summary>Effective reset interval in whole in-game days for the given location prefab.</summary>
        internal static int GetResetDays(string prefabName)
        {
            var vlr = GetVlrConfig();
            if (vlr == null)
            {
                return SunkenCryptTimerPlugin.CeFallbackResetDays.Value;
            }

            // Advanced/OverrideResetTimes == false -> the global General/ResetTime applies
            // to every location, exactly like VLR's GetResetTime fallback.
            if (!ReadBool(vlr, "Advanced", "OverrideResetTimes"))
            {
                return ReadInt(vlr, "General", "ResetTime", SunkenCryptTimerPlugin.CeFallbackResetDays.Value);
            }

            if (TypeConfigKeys.TryGetValue(prefabName, out var key))
            {
                return ReadInt(vlr, "Advanced", key, ReadInt(vlr, "General", "ResetTime",
                    SunkenCryptTimerPlugin.CeFallbackResetDays.Value));
            }

            return ReadInt(vlr, "General", "ResetTime", SunkenCryptTimerPlugin.CeFallbackResetDays.Value);
        }

        // Cached live ConfigFile of the installed Venture Location Reset plugin, if any.
        private static ConfigFile _vlrConfig;
        private static bool _vlrConfigResolved;

        private static ConfigFile GetVlrConfig()
        {
            if (!_vlrConfigResolved)
            {
                _vlrConfigResolved = true;
                if (Chainloader.PluginInfos.TryGetValue(SunkenCryptTimerPlugin.VlrPluginGuid, out var plugin))
                {
                    _vlrConfig = plugin.Instance.Config;
                    SunkenCryptTimerPlugin.Log.LogDebug(
                        "Venture Location Reset detected - using its live (server-synced) config.");
                }
                else
                {
                    SunkenCryptTimerPlugin.Log.LogDebug(
                        "Venture Location Reset not installed - using FallbackResetDays.");
                }
            }
            return _vlrConfig;
        }

        private static int ReadInt(ConfigFile file, string section, string key, int fallback)
        {
            return file.TryGetEntry(new ConfigDefinition(section, key), out ConfigEntry<int> entry)
                ? entry.Value
                : fallback;
        }

        private static bool ReadBool(ConfigFile file, string section, string key)
        {
            return file.TryGetEntry(new ConfigDefinition(section, key), out ConfigEntry<bool> entry) &&
                   entry.Value;
        }
    }
}
