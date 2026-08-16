using HarmonyLib;
using UnityEngine;

namespace HarmonyChecker
{
    internal static class HarmonyCheckerStatus
    {
        public const string BUTTON_LABEL = "IMHI_INSTALLED";

        public static void MarkInstalled(MainMenu_Buttons_Controller controller = null)
        {
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

            Transform modsTransform = controller.Main_Container.transform.Find("Mods");
            if (modsTransform == null)
            {
                return;
            }

            Lang_Button modButton = modsTransform.GetComponentInChildren<Lang_Button>();
            if (modButton == null)
            {
                return;
            }

            modButton.Constant = BUTTON_LABEL;

            // MainMenu_Buttons_Controller.Start may already have run by the time IM-HI
            // finishes loading Workshop Harmony DLLs. Updating Constant alone does not
            // redraw an already-started Lang_Button, so force the text refresh now.
            modButton.ResetText();
        }
    }

    // Keep the original early path for cases where Harmony mods load before this Start.
    [HarmonyPatch(typeof(MainMenu_Buttons_Controller), "Start")]
    public class MainMenu_Buttons_Controller_Start
    {
        public static void Postfix(ref MainMenu_Buttons_Controller __instance)
        {
            HarmonyCheckerStatus.MarkInstalled(__instance);
        }
    }

    // On Steam, Mods.LoadMods() can finish after MainMenu_Buttons_Controller.Start().
    // IM-HI patches Harmony Checker during Mods.LoadMods(); StopSpinner is called later
    // by the same loading coroutine, making this a reliable post-load refresh point.
    [HarmonyPatch(typeof(Mods), "StopSpinner")]
    public class Mods_StopSpinner
    {
        public static void Postfix()
        {
            HarmonyCheckerStatus.MarkInstalled();
        }
    }
}
