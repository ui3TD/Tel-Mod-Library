using HarmonyLib;
using System;
using UnityEngine;

namespace ModMenus.GameKeys
{
    /// <summary>
    /// "Enable Debug Cheats": ticking it turns on the game's debug mode, as pressing F7 five times does, when
    /// it's applied and whenever a save with it ticked loads. Unticking it and applying turns debug mode off.
    /// A save loaded with it unticked leaves debug mode as it is, so the F7 presses still work.
    /// </summary>
    public static class DebugCheats
    {
        public const string VAR_ID = "ModMenus_EnableDebugCheats";

        /// <summary>The setting when last looked at; null after a game starts or loads.</summary>
        private static string seen;

        /// <summary>Whether an error has been logged since the game started or loaded, so it's logged once, not every frame.</summary>
        private static bool errorLogged;

        /// <summary>
        /// Called every frame: turns debug mode on or off when the setting changes.
        /// </summary>
        public static void Check()
        {
            string value = variables.Get(VAR_ID);
            if (value == seen)
            {
                return;
            }
            if (value == "1")
            {
                DEBUG.Debug_Enabled = true;
            }
            else if (seen == "1" && value == "0")
            {
                DEBUG.Debug_Enabled = false;
            }
            seen = value;
        }

        /// <summary>
        /// Check, called from the game's Controls.Update: an error stays here, so other mods' changes to it still
        /// run, and it's logged once.
        /// </summary>
        public static void CheckSafely()
        {
            try
            {
                Check();
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Debug.LogError("[ModMenus] Couldn't check Enable Debug Cheats: " + e);
                }
            }
        }

        /// <summary>
        /// A game starting or loading: its setting is new, so only a ticked box changes anything.
        /// </summary>
        public static void Forget()
        {
            seen = null;
            errorLogged = false;
        }
    }

    /// <summary>
    /// Also when another mod skipped Controls.Update: this watches a setting, not a key.
    /// </summary>
    [HarmonyPatch(typeof(Controls), nameof(Controls.Update))]
    public class Controls_Update_DebugCheats
    {
        public static void Postfix() => DebugCheats.CheckSafely();
    }
}
