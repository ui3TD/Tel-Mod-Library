using HarmonyLib;
using System;
using UnityEngine;

namespace HarmonyChecker
{
    internal static class HarmonyCheckerStatus
    {
        // Replaced in tests.
        internal static Action<Transform, bool, Version> ShowBadge = (modsButton, outdated, installed) => StatusBadge.Show(modsButton, outdated, installed);

        /// <summary>
        /// Shows on the main menu that IM-HI works: the Mods button gets back the label of the player's
        /// language (this mod's JSON changes it to say IM-HI isn't installed, which only shows when this DLL
        /// can't run), and a badge with a tick, or a "!" when IM-HI is out of date.
        /// </summary>
        public static void MarkInstalled(MainMenu_Buttons_Controller controller = null)
        {
            // The language table is the same everywhere, so this runs even outside the main menu.
            try
            {
                ModsLabel.Restore();
            }
            catch (Exception e)
            {
                Debug.LogError("[Harmony Checker] Couldn't restore the Mods label: " + e);
            }

            if (controller == null)
            {
                Camera camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                mainScript main = camera.GetComponent<mainScript>();
                if (main == null || main.Data == null)
                {
                    return;
                }

                controller = main.Data.GetComponent<MainMenu_Buttons_Controller>();
            }

            if (controller == null || controller.Main_Container == null)
            {
                return;
            }

            Version installed = IMHIUpdate.InstalledVersion();
            bool outdated = IMHIUpdate.IsOutdated(installed);
            Transform container = controller.Main_Container.transform;
            Transform modsTransform = container.Find("Mods");

            // Only the game's label is redrawn: another mod's label on the Mods button is kept
            Lang_Button modButton = modsTransform == null ? null : modsTransform.GetComponentInChildren<Lang_Button>();
            if (modButton != null && modButton.Constant == GAME_LABEL)
            {
                // MainMenu_Buttons_Controller.Start may already have run by the time IM-HI
                // finishes loading Workshop Harmony DLLs, so the button already shows the old text.
                modButton.ResetText();
            }

            if (modsTransform != null)
            {
                ShowBadge(modsTransform, outdated, installed);
            }

            if (outdated)
            {
                IMHIUpdate.ShowButton(container, modsTransform, installed);
            }
        }

        public const string GAME_LABEL = "MM_MODS";
    }

    // The refreshes below run whether or not another mod skipped the game's method: the menu's label
    // shows whether IM-HI works, which doesn't depend on what those methods do. An error stays in the
    // postfix, so the game's method and other mods' patches on it still finish.

    // Keep the original early path for cases where Harmony mods load before this Start.
    [HarmonyPatch(typeof(MainMenu_Buttons_Controller), nameof(MainMenu_Buttons_Controller.Start))]
    public class MainMenu_Buttons_Controller_Start
    {
        public static void Postfix(MainMenu_Buttons_Controller __instance)
        {
            try
            {
                HarmonyCheckerStatus.MarkInstalled(__instance);
            }
            catch (Exception e)
            {
                Debug.LogError("[Harmony Checker] Couldn't update the main menu: " + e);
            }
        }
    }

    // A language change (or any reload of the language table) loads this mod's JSON label again, so it's
    // put back after the game's load. Last, so it also comes after other mods' changes to the table.
    [HarmonyPatch(typeof(Language), nameof(Language._Load))]
    public class Language__Load
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            try
            {
                HarmonyCheckerStatus.MarkInstalled();
            }
            catch (Exception e)
            {
                Debug.LogError("[Harmony Checker] Couldn't update the main menu: " + e);
            }
        }
    }

    // On Steam, Mods.LoadMods() can finish after MainMenu_Buttons_Controller.Start().
    // IM-HI patches Harmony Checker during Mods.LoadMods(); StopSpinner is called later
    // by the same loading coroutine, making this a reliable post-load refresh point.
    [HarmonyPatch(typeof(Mods), nameof(Mods.StopSpinner))]
    public class Mods_StopSpinner
    {
        public static void Postfix()
        {
            try
            {
                HarmonyCheckerStatus.MarkInstalled();
            }
            catch (Exception e)
            {
                Debug.LogError("[Harmony Checker] Couldn't update the main menu: " + e);
            }
        }
    }
}
