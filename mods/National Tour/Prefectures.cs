using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Country = SEvent_Tour._country;

namespace NationalTour
{
    /// <summary>
    /// The 18 prefectures national tours go to. Each is a tour destination of its own, numbered 1200-1217, with
    /// the cost, ticket price and regional discount of the country it stands in for. They are kept out of the
    /// game's country list, so the World Tour's activities, achievement and saved country data never see them.
    /// </summary>
    public static class Prefectures
    {
        public enum Prefecture
        {
            saitama = 1200,
            kanagawa = 1201,
            niigata = 1202,
            aichi = 1203,
            chiba = 1204,
            shizuoka = 1205,
            ishikawa = 1206,
            okinawa = 1207,
            hokkaido = 1208,
            miyagi = 1209,
            fukuoka = 1210,
            kumamoto = 1211,
            ehime = 1212,
            osaka = 1213,
            okayama = 1214,
            hiroshima = 1215,
            hyogo = 1216,
            kyoto = 1217
        };

        /// <summary>
        /// The prefecture each country stands in for. Countries in one discount area map to one region of Japan.
        /// </summary>
        public static readonly Dictionary<Country, Prefecture> ForCountry = new()
        {
            { Country.china, Prefecture.saitama },
            { Country.southKorea, Prefecture.kanagawa },
            { Country.india, Prefecture.niigata },
            { Country.thailand, Prefecture.aichi },
            { Country.philippines, Prefecture.chiba },
            { Country.indonesia, Prefecture.shizuoka },
            { Country.australia, Prefecture.ishikawa },
            { Country.southAfrica, Prefecture.okinawa },
            { Country.canada, Prefecture.hokkaido },
            { Country.US, Prefecture.miyagi },
            { Country.brazil, Prefecture.fukuoka },
            { Country.argentina, Prefecture.kumamoto },
            { Country.UK, Prefecture.ehime },
            { Country.france, Prefecture.osaka },
            { Country.germany, Prefecture.okayama },
            { Country.italy, Prefecture.hiroshima },
            { Country.spain, Prefecture.hyogo },
            { Country.russia, Prefecture.kyoto }
        };

        /// <summary>
        /// Where each prefecture's pin goes on the map of Japan, in world units of a 16:9 screen with the new tour
        /// popup fully open. There the popup is 576 units tall for the camera's 10 world units, so times
        /// POPUP_UNITS_PER_MAP_UNIT gives the popup's own units, which are the same at every screen size.
        /// </summary>
        public const float POPUP_UNITS_PER_MAP_UNIT = 57.6f;

        public static readonly Dictionary<Prefecture, Vector3> MapPosition = new()
        {
            { Prefecture.ishikawa, new Vector3(0.1f, 0f, 0f) },
            { Prefecture.hokkaido, new Vector3(4.8f, 2.5f, 0f) },
            { Prefecture.miyagi, new Vector3(3.2f, 0.5f, 0f) },
            { Prefecture.okinawa, new Vector3(-5.9f, 2.8f, 0f) },
            { Prefecture.chiba, new Vector3(1.6f, -1.9f, 0f) },
            { Prefecture.saitama, new Vector3(1.1f, -0.9f, 0f) },
            { Prefecture.niigata, new Vector3(1.4f, 0f, 0f) },
            { Prefecture.aichi, new Vector3(-0.2f, -1.2f, 0f) },
            { Prefecture.shizuoka, new Vector3(0.3f, -1.9f, 0f) },
            { Prefecture.kanagawa, new Vector3(1f, -1.4f, 0f) },
            { Prefecture.fukuoka, new Vector3(-4f, -0.4f, 0f) },
            { Prefecture.kumamoto, new Vector3(-4.5f, -1.2f, 0f) },
            { Prefecture.ehime, new Vector3(-2.8f, -1.3f, 0f) },
            { Prefecture.hiroshima, new Vector3(-2.8f, -0.8f, 0f) },
            { Prefecture.okayama, new Vector3(-1.9f, -0.4f, 0f) },
            { Prefecture.hyogo, new Vector3(-1.6f, -0.9f, 0f) },
            { Prefecture.osaka, new Vector3(-1.3f, -1.4f, 0f) },
            { Prefecture.kyoto, new Vector3(-0.7f, -0.7f, 0f) }
        };

        /// <summary>
        /// The game's stamina cost per stop, and Osaka's, which keeps the 10 that France costs.
        /// </summary>
        public const int GAME_STAMINA_COST = 20;
        public const int OSAKA_STAMINA_COST = 10;

        /// <summary>
        /// The prefecture destinations, built with the game's countries. Empty before the game has loaded them.
        /// </summary>
        public static readonly List<SEvent_Tour.country> All = new();

        public static bool IsPrefecture(Country type) => Enum.IsDefined(typeof(Prefecture), (int)type);

        public static Country TypeOf(Prefecture prefecture) => (Country)(int)prefecture;

        /// <summary>
        /// The prefecture's destination, or null if it couldn't be built.
        /// </summary>
        public static SEvent_Tour.country Get(Country type)
        {
            foreach (SEvent_Tour.country prefecture in All)
            {
                if (prefecture.Type == type)
                    return prefecture;
            }
            return null;
        }

        /// <summary>
        /// A national tour is one whose stops are prefectures. A tour has stops on one map only, because each
        /// planning starts a new tour on one map.
        /// </summary>
        public static bool IsNational(SEvent_Tour.tour tour)
        {
            if (tour?.SelectedCountries == null || tour.SelectedCountries.Count == 0)
                return false;
            foreach (SEvent_Tour.tour.selectedCountry stop in tour.SelectedCountries)
            {
                if (stop?.Country == null || !IsPrefecture(stop.Country.Type))
                    return false;
            }
            return true;
        }

        public static string LanguageKey(Prefecture prefecture) => "NATIONAL_TOUR__" + prefecture.ToString().ToUpperInvariant();

        /// <summary>
        /// The prefecture's name from the language files, or its English name if they don't have it.
        /// </summary>
        public static string Title(Prefecture prefecture)
        {
            if (Language.Data.TryGetValue(LanguageKey(prefecture), out string title))
                return title;
            string name = prefecture.ToString();
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>
        /// Builds one prefecture per country of the game's list, copying that country's numbers. A country
        /// another mod has removed leaves its prefecture out. Levels start at 1; they come from the tour
        /// history, not from the game's saved country data.
        /// </summary>
        public static void Build()
        {
            All.Clear();
            foreach (KeyValuePair<Country, Prefecture> pair in ForCountry)
            {
                SEvent_Tour.country paired = null;
                foreach (SEvent_Tour.country country in SEvent_Tour.Countries)
                {
                    if (country.Type == pair.Key)
                    {
                        paired = country;
                        break;
                    }
                }
                if (paired == null)
                    continue;

                All.Add(new SEvent_Tour.country
                {
                    Type = TypeOf(pair.Value),
                    Area = paired.Area,
                    Cost = paired.Cost,
                    TicketPrice = paired.TicketPrice,
                    Level = 1,
                });
            }

            if (All.Count == 0)
                Readiness.Fail(Readiness.Module.Prefectures, "the game's tour country list has none of the 18 countries");
        }
    }


    // Build the prefectures right after the game's countries
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.LoadCountries))]
    public class SEvent_Tour_LoadCountries
    {
        public static void Postfix() => Prefectures.Build();
    }


    // A prefecture's number finds the prefecture; every other number keeps the game's (or another mod's) result
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.GetCountry))]
    public class SEvent_Tour_GetCountry
    {
        // The game's parameter is named __country, which HarmonyX would read as a special argument: take it by position
        public static void Postfix(Country __0, ref SEvent_Tour.country __result)
        {
            if (__result == null && Prefectures.IsPrefecture(__0))
                __result = Prefectures.Get(__0);
        }
    }


    // A prefecture's name, in place of the game's "ERROR" for an unknown number
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.GetCountryTitle))]
    public class SEvent_Tour_GetCountryTitle
    {
        public static void Postfix(Country __0, ref string __result)
        {
            if (!Prefectures.IsPrefecture(__0))
                return;
            if (Language.Data.TryGetValue("ERROR", out string error) && __result != error)
                return;
            __result = Prefectures.Title((Prefectures.Prefecture)(int)__0);
        }
    }


    // Osaka keeps France's lower stamina cost, unless another mod has changed it
    [HarmonyPatch(typeof(SEvent_Tour.country), nameof(SEvent_Tour.country.GetStaminaCost))]
    public class SEvent_Tour_country_GetStaminaCost
    {
        public static void Postfix(SEvent_Tour.country __instance, ref int __result)
        {
            if (__instance.Type == Prefectures.TypeOf(Prefectures.Prefecture.osaka) && __result == Prefectures.GAME_STAMINA_COST)
                __result = Prefectures.OSAKA_STAMINA_COST;
        }
    }
}
