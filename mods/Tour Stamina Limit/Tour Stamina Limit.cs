using HarmonyLib;
using System;
using System.Globalization;
using UnityEngine;
using System.Reflection;
using static TourStamina.TourStamina;

namespace TourStamina
{
    public class TourStamina
    {
        public const float TOUR_FAN_COEFF = 2.5f;
        public const int TOUR_STAM_CAP = 100;

        public const string TOUR_STAM_TOOLTIP_ID = "TOUR__STAMINACAP";
    }

    // World tours give 2.5x more fans
    [HarmonyPatch(typeof(SEvent_Tour.tour), nameof(SEvent_Tour.tour.GetNewFansByAttendance))]
    public class SEvent_Tour_tour_GetNewFansByAttendance
    {
        public static void Postfix(ref int __result)
        {
            __result = Mathf.RoundToInt(__result * TOUR_FAN_COEFF);
        }
    }

    // World tours are limited to 100 stamina
    [HarmonyPatch(typeof(SEvent_Tour.tour), nameof(SEvent_Tour.tour.SelectCountry))]
    public class SEvent_Tour_tour_SelectCountry
    {
        public static bool Prefix(SEvent_Tour.country Country, SEvent_Tour.tour __instance)
        {
            SEvent_Tour.tour.selectedCountry country = __instance.GetCountry(Country);
            if (country == null)
            {
                int staminaCost = __instance.Stamina + Country.GetStaminaCost();
                if (staminaCost > TOUR_STAM_CAP)
                {
                    return false;
                }
            }
            return true;
        }
    }

    // Warn in the tooltip if picking the country would go over the cap: the warning goes in front of the
    // tooltip the game built. The game's shared text is never changed, so no other code can see the warning.
    [HarmonyPatch(typeof(Tour_Star), nameof(Tour_Star.SetTooltip))]
    public class Tour_Star_SetTooltip
    {
        private static readonly AccessTools.FieldRef<ButtonDefault, string> TooltipText =
            AccessTools.FieldRefAccess<ButtonDefault, string>("tooltipText");

        public static void Postfix(Tour_Star __instance, Tour_Country ___TourCountry, bool __runOriginal)
        {
            // Another mod skipped the game's tooltip: leave whatever it set
            if (!__runOriginal)
                return;

            SEvent_Tour.country country = ___TourCountry.Country;
            SEvent_Tour.tour tour = ___TourCountry.TourPopup.Tour;
            if (country.GetStaminaCost() + tour.Stamina > TOUR_STAM_CAP && tour.GetCountry(country) == null)
            {
                ButtonDefault button = __instance.GetComponent<ButtonDefault>();
                // ParseText only replaces tokens, so parsing the warning on its own gives the same text as
                // the game parsing it as part of the whole tooltip
                TooltipText(button) = button.ParseText("<color=" + mainScript.red + ">"
                    + Language.Insert(TOUR_STAM_TOOLTIP_ID, TOUR_STAM_CAP.ToString(CultureInfo.InvariantCulture))
                    + "</color>\n") + TooltipText(button);
            }
        }
    }

    // Update UI on click
    [HarmonyPatch(typeof(Tour_Country), nameof(Tour_Country.OnClick))]
    public class Tour_Country_OnClick
    {
        public static void Postfix(Tour_Country __instance)
        {
            Tour_Country[] componentsInChildren = __instance.TourPopup.CountriesContainer.transform.GetComponentsInChildren<Tour_Country>();
            for (int i = 0; i < componentsInChildren.Length; i++)
            {
                componentsInChildren[i].UpdateData();
            }
        }
    }


}
