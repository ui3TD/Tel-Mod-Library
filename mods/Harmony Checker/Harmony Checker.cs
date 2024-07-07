using HarmonyLib;
using UnityEngine;

namespace HarmonyChecker
{
    // change button text
    [HarmonyPatch(typeof(MainMenu_Buttons_Controller), "Start")]
    public class MainMenu_Buttons_Controller_Start
    {
        public const string buttonLabel = "IMHI_INSTALLED";

        public static void Postfix(ref MainMenu_Buttons_Controller __instance)
        {
            Lang_Button modButton = __instance.Main_Container.transform.Find("Mods").GetComponentInChildren<Lang_Button>();
            modButton.Constant = buttonLabel;
        }
    }
}
