using HarmonyLib;
using UnityEngine;

namespace ModMenus
{
    /// <summary>
    /// Lazily installs ModMenus only when the player opens the in-game Settings tab.
    /// This intentionally does no work on the main menu or while a game is loading.
    /// </summary>
    [HarmonyPatch(typeof(Tabs_Manager), nameof(Tabs_Manager.OpenTab))]
    public class Tabs_Manager_OpenTab
    {
        public static void Postfix(Tabs_Manager._tab._type Type)
        {
            if (Type != Tabs_Manager._tab._type.settings || !ModMenusUtils.IsGameplayReady())
            {
                return;
            }

            // The Settings hierarchy is active by the time this postfix runs, so install
            // the button once without any background retry loop. The heavier mod-menu
            // popup is generated only if the player actually clicks Mod Settings.
            if (!ModMenusUtils.TryInstallSettingsButton())
            {
                Debug.LogWarning("[ModMenus] Could not install the Mod Settings button in the active in-game Settings tab.");
            }
        }
    }

    /// <summary>
    /// Every new game and every loaded scene starts with no settings (variables.Awake clears them).
    /// The defaults are saved straight away, so mods read the menu's default even if the player
    /// never opens Mod Settings.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.Awake))]
    public class variables_Awake
    {
        public static void Postfix()
        {
            ModMenusUtils.SaveMissingDefaults();
            GameKeys.DebugCheats.Forget();
        }
    }

    /// <summary>
    /// Loading a save replaces the settings with the saved ones, which may be from before a mod was
    /// installed or gained a setting.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.LoadFunction))]
    public class variables_LoadFunction
    {
        public static void Postfix()
        {
            ModMenusUtils.SaveMissingDefaults();
            GameKeys.DebugCheats.Forget();
        }
    }
}
