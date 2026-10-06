using System;
using UnityEngine;

namespace InstancedTimer
{
    /// <summary>
    /// Deterministic per-instance display names: "Odin's Tomb (6E77)".
    /// The name is generated from the feature's ZDO uid - no stored state, no
    /// discovery order, identical on every client and across sessions.
    /// Repeats are possible (and fine); the 4-hex uid code disambiguates.
    /// </summary>
    internal static class DungeonNames
    {
        // Norse-flavored roots (gods, saga names) - the memorable half.
        private static readonly string[] Roots =
        {
            "Odin", "Thor", "Loki", "Freya", "Fenrir", "Ymir", "Hel", "Baldur",
            "Tyr", "Njord", "Sif", "Bragi", "Heimdall", "Frigg", "Ragnar",
            "Bjorn", "Astrid", "Grimr", "Ulf", "Sigrid", "Ivar", "Ketil",
            "Orm", "Hilda", "Eirik", "Gudrun", "Thyri", "Vali", "Hod", "Nanna",
            "Fafnir", "Jormungandr", "Surt", "Skadi", "Vidar", "Mimir",
        };

        // Suffix sets flavored by dungeon type.
        private static readonly string[] Burial = { "Tomb", "Barrow", "Crypt", "Rest", "Grave", "Cairn" };
        private static readonly string[] Cave = { "Den", "Hollow", "Howl", "Lair" };
        private static readonly string[] Camp = { "Camp", "Hold", "Stead", "Watch" };
        private static readonly string[] Generic = { "Rest", "Hollow", "Refuge", "Cache" };

        /// <summary>Proper-noun name for a feature, e.g. "Odin's Tomb".</summary>
        internal static string Name(long uid, string prefabName)
        {
            var suffixes = SuffixesFor(prefabName);
            var rng = new System.Random((int)(uid & 0xFFFFFFFF) ^ (int)(uid >> 32));
            return $"{Roots[rng.Next(Roots.Length)]}'s {suffixes[rng.Next(suffixes.Length)]}";
        }

        /// <summary>
        /// Map-usable disambiguator: the feature's 64 m zone coordinates,
        /// e.g. "(-70,-21)" - readable against the minimap, deterministic.
        /// </summary>
        internal static string Zone(Vector3 position)
        {
            int zx = Mathf.FloorToInt(position.x / 64f);
            int zz = Mathf.FloorToInt(position.z / 64f);
            return $"({zx},{zz})";
        }

        private static string[] SuffixesFor(string prefabName)
        {
            if (prefabName == null) return Generic;
            if (prefabName.IndexOf("Crypt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prefabName.IndexOf("Burial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prefabName.IndexOf("Grave", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Burial;
            }
            if (prefabName.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prefabName.IndexOf("Troll", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Cave;
            }
            if (prefabName.IndexOf("Camp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prefabName.IndexOf("Village", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prefabName.IndexOf("Farm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Camp;
            }
            return Generic;
        }
    }
}
