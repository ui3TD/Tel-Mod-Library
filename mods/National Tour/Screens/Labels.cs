using HarmonyLib;
using UnityEngine;

namespace NationalTour
{
    /// <summary>
    /// "National Tour" where the game names the World Tour for a national tour: the event cards and the staff
    /// status line.
    /// </summary>
    public static class Labels
    {
        public const string PREFECTURES_KEY = "NATIONAL_TOUR__PREFECTURES";
        public const string PREFECTURES_FALLBACK = "Prefectures";
        public const string WORLD_TOUR_KEY = "SEVENT__WORLD_TOUR";

        public static string NationalTitle() =>
            Language.Data.TryGetValue(NationalTab.TITLE_KEY, out string title) ? title : NationalTab.TITLE_FALLBACK;

        public static void CardInProduction(SEvent_Button_Tour card, SEvent_Tour.tour tour)
        {
            if (Prefectures.IsNational(tour) && card.Title != null)
                NationalTab.SetText(card.Title.transform, NationalTab.TITLE_KEY, NationalTab.TITLE_FALLBACK);
        }

        /// <summary>
        /// The finished card's title, and the label of its stop count ("Countries" in the game).
        /// </summary>
        public static void CardFinished(SEvent_Button_Tour_Finished card, SEvent_Tour.tour tour)
        {
            if (!Prefectures.IsNational(tour))
                return;
            if (card.Title != null)
                NationalTab.SetText(card.Title.transform, NationalTab.TITLE_KEY, NationalTab.TITLE_FALLBACK);
            if (card.Countries != null)
                NationalTab.SetText(card.Countries.transform.parent?.Find("Title"), PREFECTURES_KEY, PREFECTURES_FALLBACK);
        }

        /// <summary>
        /// "Production for the World Tour" becomes "Production for the National Tour" for a room working on one.
        /// </summary>
        public static string StaffStatus(staff._staff staffer, string status)
        {
            agency._room room = staffer.room;
            if (status == null || room == null || room.status != agency._room._status.tourProduction || !Prefectures.IsNational(room.tour))
                return status;
            if (!Language.Data.TryGetValue(WORLD_TOUR_KEY, out string world) || string.IsNullOrEmpty(world))
                return status;
            return status.Replace(world, NationalTitle());
        }
    }


    [HarmonyPatch(typeof(SEvent_Button_Tour), nameof(SEvent_Button_Tour.Set), new[] { typeof(SEvent_Tour.tour) })]
    public class SEvent_Button_Tour_Set
    {
        public static void Postfix(SEvent_Button_Tour __instance, SEvent_Tour.tour _Tour) => Labels.CardInProduction(__instance, _Tour);
    }


    [HarmonyPatch(typeof(SEvent_Button_Tour_Finished), nameof(SEvent_Button_Tour_Finished.Set), new[] { typeof(SEvent_Tour.tour) })]
    public class SEvent_Button_Tour_Finished_Set
    {
        public static void Postfix(SEvent_Button_Tour_Finished __instance, SEvent_Tour.tour Tour) => Labels.CardFinished(__instance, Tour);
    }


    [HarmonyPatch(typeof(staff._staff), nameof(staff._staff.GetStatusText))]
    public class staff_staff_GetStatusText
    {
        public static void Postfix(staff._staff __instance, ref string __result) => __result = Labels.StaffStatus(__instance, __result);
    }
}
