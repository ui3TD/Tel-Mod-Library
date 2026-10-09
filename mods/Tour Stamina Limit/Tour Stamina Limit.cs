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
        public const float TOUR_FAN_COEFF = 3.5f;
        public const int TOUR_STAM_CAP = 100;

        public const string TOUR_STAM_TOOLTIP_ID = "TOUR__STAMINACAP";
    }

    // World tours give 3.5x more fans
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

    // Set tooltip if at max stamina. The game builds the tooltip from the shared "Stamina" text, so the
    // warning goes in front of that text while the tooltip is built.
    [HarmonyPatch(typeof(Tour_Star), nameof(Tour_Star.SetTooltip))]
    public class Tour_Star_SetTooltip
    {
        public static void Prefix(out string __state, Tour_Country ___TourCountry)
        {
            __state = Language.Data["STAMINA"];

            SEvent_Tour.country country = ___TourCountry.Country;
            SEvent_Tour.tour tour = ___TourCountry.TourPopup.Tour;
            if (country.GetStaminaCost() + tour.Stamina > TOUR_STAM_CAP && tour.GetCountry(country) == null)
            {
                Language.Data["STAMINA"] = "<color=" + mainScript.red + ">"
                    + Language.Insert(TOUR_STAM_TOOLTIP_ID, TOUR_STAM_CAP.ToString(CultureInfo.InvariantCulture))
                    + "</color>\n" + __state;
            }
        }

        // A finalizer, not a postfix: it runs even if SetTooltip throws, so the warning can't stay in
        // every "Stamina" label for the rest of the session
        public static void Finalizer(string __state)
        {
            if (__state != null)
                Language.Data["STAMINA"] = __state;
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
