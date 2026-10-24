using HarmonyLib;

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
        /// A game starting or loading: its setting is new, so only a ticked box changes anything.
        /// </summary>
        public static void Forget() => seen = null;
    }

    [HarmonyPatch(typeof(Controls), nameof(Controls.Update))]
    public class Controls_Update_DebugCheats
    {
        public static void Postfix() => DebugCheats.Check();
    }
}
