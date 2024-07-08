using HarmonyLib;
using UnityEngine.UI;
using UnityEngine;
using static NationalTour.Utility;

namespace NationalTour
{

    // ovverride tab button click operation to render continue button
    [HarmonyPatch(typeof(SpecialEvents_Manager), "OpenTab_WorldTour")]
    public class SpecialEvents_Manager_OpenTab_WorldTour
    {
        public static void Prefix()
        {
            if (tourPopupBGImage == null)
            {
                GameObject popupObj = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.special_events).obj;
                tourPopupBGImage = popupObj.transform.Find("Container").Find("World Tour").Find("BG").gameObject.GetComponent<Image>();
            }

            tourPopupBGImage.sprite = World_tour_def;
        }
    }


    // Set create new image, if opened directly
    [HarmonyPatch(typeof(SpecialEvents_Manager), "OpenSpecialEventsPopup")]
    public class SpecialEvents_Manager_OpenSpecialEventsPopup
    {
        public static void Postfix(SpecialEvents_Manager __instance)
        {
            if (__instance.OpenTab != SpecialEvents_Manager._type.WorldTour)
                return;

            if (tourPopupBGImage == null)
            {
                GameObject popupObj = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.special_events).obj;
                tourPopupBGImage = popupObj.transform.Find("Container").Find("World Tour").Find("BG").gameObject.GetComponent<Image>();
            }

            tourPopupBGImage.sprite = World_tour_def;

        }
    }
}
