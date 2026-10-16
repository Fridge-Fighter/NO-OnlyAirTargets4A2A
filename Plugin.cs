using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AutoAirOnlyWhenAA
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static ConfigEntry<bool> Enabled;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Enable automatic air-only targeting when an IR or ARH missile is selected");

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded. Enabled = {Enabled.Value}");
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.yourname.onlyairtargets4a2a";
        public const string PLUGIN_NAME = "OnlyAirTargets4A2A";
        public const string PLUGIN_VERSION = "1.1.0";
    }

    [HarmonyPatch(typeof(TargetListSelector), nameof(TargetListSelector.CheckExclusions))]
    public static class CheckExclusionsPatch
    {
        static void Postfix(Unit u, ref bool __result)
        {
            if (!Plugin.Enabled.Value)
                return;

            // Already excluded by vanilla filters → leave it
            if (__result)
                return;

            var hud = SceneSingleton<CombatHUD>.i;
            if (hud == null || hud.aircraft == null)
                return;

            var station = hud.GetWeaponStation();
            if (station == null || station.WeaponInfo == null)
                return;

            if (!IsAirToAirMissile(station.WeaponInfo))
                return;

            // AA missile selected → exclude everything that is not an Aircraft or Missile
            if (!(u is Aircraft || u is Missile))
            {
                __result = true;
            }
        }

        private static bool IsAirToAirMissile(WeaponInfo info)
        {
            if (info == null || !info.missile || info.weaponPrefab == null)
                return false;

            try
            {
                // Safer way: look directly for the MissileSeeker component on the prefab
                var seeker = info.weaponPrefab.GetComponent<MissileSeeker>();
                if (seeker == null)
                    seeker = info.weaponPrefab.GetComponentInChildren<MissileSeeker>(true);

                if (seeker == null)
                    return false;

                string seekerType = seeker.GetSeekerType();
                return seekerType == "IR" || seekerType == "ARH";
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to check seeker type: {ex.Message}");
                return false;
            }
        }
    }
}