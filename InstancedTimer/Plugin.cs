using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace InstancedTimer
{
    /// <summary>
    /// Client-only mod: shows a "resets in Xd Yh" countdown for instanced dungeons
    /// (sunken crypts, burial chambers, troll caves, mines, ...) when you are near
    /// their entrance. Works on any server running Venture Location Reset - the mod
    /// reads the VV_LastReset day number that VLR stamps onto each LocationProxy ZDO
    /// (ZDOs sync to all clients, so no server-side changes are needed). Read-only:
    /// this mod never writes world data.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class InstancedTimerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "lan124.InstancedTimer";
        public const string PluginName = "InstancedTimer";
        public const string PluginVersion = "1.2.4";

        internal static ManualLogSource Log;

        // ZDO int field written by Venture Location Reset on every LocationProxy it manages.
        internal const string VlrLastResetField = "VV_LastReset";
        // GUID of the Venture Location Reset plugin, for reading its (server-synced) config.
        internal const string VlrPluginGuid = "com.orianaventure.mod.LocationReset";

        internal static ConfigEntry<bool> CeEnabled;
        internal static ConfigEntry<bool> CeDebug;
        internal static ConfigEntry<float> CeScanRadius;
        internal static ConfigEntry<float> CeUpdateInterval;
        internal static ConfigEntry<float> CeHudOffsetY;
        internal static ConfigEntry<float> CeTextScale;
        internal static ConfigEntry<int> CeFallbackResetDays;
        internal static ConfigEntry<bool> CeRingEnabled;
        internal static ConfigEntry<float> CeRingShowRadius;
        // How the HUD line names the dungeon instance (F1-configurable).
        internal enum InstanceNaming { Type, NameAndZone, Name, Zone }
        internal static ConfigEntry<InstanceNaming> CeInstanceNaming;

        private void Awake()
        {
            Log = Logger;

            CeEnabled = Config.Bind("General", "Enabled", true, "Master switch for the dungeon reset timer HUD.");
            CeDebug = Config.Bind("General", "DebugLogging", false,
                "Log scanner decisions each update (verbose; for troubleshooting).");
            CeScanRadius = Config.Bind("HUD", "ScanRadiusMeters", 80f,
                "Show the timer when the player is within this distance of a dungeon entrance (float, meters).");
            CeUpdateInterval = Config.Bind("HUD", "UpdateIntervalSeconds", 1f,
                "How often to rescan for nearby dungeons (float, seconds). 1 is plenty.");
            CeHudOffsetY = Config.Bind("HUD", "HudOffsetY", 170f,
                "Fallback top offset for the timer text, used only until the status-effect row " +
                "(Rested/Wet icons) is found - normally the timer positions itself just below " +
                "that row (float, pixels).");
            CeTextScale = Config.Bind("HUD", "TextScale", 1.25f,
                new ConfigDescription("Timer text size relative to the status-effect labels " +
                "(1 = same size as Resting/Shelter). Applies live - no restart needed.",
                new AcceptableValueRange<float>(0.5f, 3f)));
            CeFallbackResetDays = Config.Bind("Timers", "FallbackResetDays", 10,
                "Assumed reset interval (in-game days) when Venture Location Reset is not installed on this " +
                "client and its synced config cannot be read. Set this to match the server you play on. " +
                "Ignored whenever VLR's own (server-synced) config is available.");
            CeRingEnabled = Config.Bind("Rings", "Enabled", true,
                "Draw a workbench-style ground ring around each nearby instanced dungeon (crypts, burial " +
                "chambers, troll caves, ...), sized to the radius Venture Location Reset checks for it " +
                "(45 m for sunken crypts). Instanced dungeons only - ground locations get no ring.");
            CeRingShowRadius = Config.Bind("Rings", "ShowRadiusMeters", 100f,
                "Show a dungeon's ring while the player is within this distance of its entrance " +
                "(float, meters). 100 matches Venture Location Reset's own reset-trigger distance.");
            CeInstanceNaming = Config.Bind("HUD", "InstanceNaming", InstanceNaming.NameAndZone,
                "How the timer names the dungeon: Type (vanilla type name), NameAndZone " +
                "(\"Odin's Tomb (-70,-21)\" - name deterministic per instance, zone readable on " +
                "the minimap), Name, or Zone.");

            gameObject.AddComponent<Tracker>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }
    }
}
