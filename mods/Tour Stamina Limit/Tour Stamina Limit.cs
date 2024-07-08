using HarmonyLib;
using System;
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
    [HarmonyPatch(typeof(SEvent_Tour.tour), "GetNewFansByAttendance")]
    public class SEvent_Tour_tour_GetNewFansByAttendance
    {
        public static void Postfix(ref int __result)
        {
            __result = Mathf.RoundToInt(__result * TOUR_FAN_COEFF);
        }
    }

    // World tours are limited to 100 stamina
    [HarmonyPatch(typeof(SEvent_Tour.tour), "SelectCountry")]
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

    // Set tooltip if at max stamina
    [HarmonyPatch(typeof(Tour_Star), "SetTooltip")]
    public class Tour_Star_SetTooltip
    {
        public static bool Prefix(ref string __state, Tour_Country ___TourCountry)
        {
            __state = Language.Data["STAMINA"];

            SEvent_Tour.country country = ___TourCountry.Country;
            SEvent_Tour.tour tour = ___TourCountry.TourPopup.Tour;
            if (country.GetStaminaCost() + tour.Stamina > TOUR_STAM_CAP && tour.GetCountry(country) == null)
            {
                Language.Data["STAMINA"] = string.Concat(new string[]
                {
                    "<color=",
                    mainScript.red,
                    ">",
                    Language.Data[TOUR_STAM_TOOLTIP_ID],
                    "</color>\n",
                    Language.Data["STAMINA"]
                });
            }

            return true;
        }

        public static void Postfix(ref string __state)
        {
            Language.Data["STAMINA"] = __state;
        }
    }

    // Update UI on click
    [HarmonyPatch(typeof(Tour_Country), "OnClick")]
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
