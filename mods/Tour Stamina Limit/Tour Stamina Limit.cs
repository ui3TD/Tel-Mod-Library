using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;

namespace TourStamina
{

    // World tours give 3.5x more fans
    [HarmonyPatch(typeof(SEvent_Tour.tour), "GetNewFansByAttendance")]
    public class SEvent_Tour_tour_GetNewFansByAttendance
    {
        public static void Postfix(ref int __result)
        {
            __result = Mathf.RoundToInt(__result * 3.5f);
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
                if (staminaCost > 100)
                {
                    return false;
                }
            }
            return true;
        }
    }

    // World tours are limited to 100 stamina
    [HarmonyPatch(typeof(Tour_Star), "SetTooltip")]
    public class Tour_Star_SetTooltip
    {
        public static bool Prefix(Tour_Star __instance, ref string __state)
        {
            __state = Language.Data["STAMINA"];

            Tour_Country TourCountry = Traverse.Create(__instance).Field("TourCountry").GetValue() as Tour_Country;

            SEvent_Tour.country country = TourCountry.Country;
            SEvent_Tour.tour tour = TourCountry.TourPopup.Tour;
            if (country.GetStaminaCost() + tour.Stamina > 100 && tour.GetCountry(country) == null)
            {
                Language.Data["STAMINA"] = string.Concat(new object[]
                {
                    "<color=",
                    mainScript.red,
                    ">",
                    Language.Data["TOUR__STAMINACAP"],
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

    // World tours are limited to 100 stamina
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
