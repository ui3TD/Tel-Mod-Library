using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using static PromotionTierTweaks.Utility;

namespace PromotionTierTweaks
{
    public class Utility
    {
        public const int lvl3Eps = 6;
        public const int lvl4Eps = 6;
        public const int lvl6Eps = 6;
        public const int lvl9Fame = 9;
        public const string lvl3Label = "UNFAIR_PLUS__ACTIVITIES__INTERNETSHOW";
        public const string lvl4Label = "UNFAIR_PLUS__ACTIVITIES__RADIOSHOW";
        public const string lvl6Label = "UNFAIR_PLUS__ACTIVITIES__TVSHOW";

        // Standard game values
        public const int lvl3mags = 3;
        public const int lvl4audience = 6000;
        public const int lvl6mags = 24;
        public const int lvl9IdolCount = 3;

        public static int CountEpisodesOfShowsWithType(Shows._param._media_type type)
        {
            int maxEpisodes = 0;
            foreach (Shows._show show in Shows.shows)
            {
                if (show.status != Shows._show._status.working)
                {
                    Shows._param._media_type? media_type = show.medium.media_type;
                    if (media_type.GetValueOrDefault() == type & media_type != null)
                    {
                        if (show.episodeCount >= maxEpisodes)
                        {
                            maxEpisodes = show.episodeCount;
                        }
                    }
                }
            }
            return maxEpisodes;
        }
    }

    // In hard mode, level 3 requires 6 internet episodes, level 4 requires 6000 internet show viewers and 6 radio episodes, level 6 requires 6 tv episodes,
    // level 9 Promotion requires 3 idols with 9 fame instead of 5
    [HarmonyPatch(typeof(Activities), "GetMaxLevel_Promotion")]
    public class Activities_GetMaxLevel_Promotion
    {       
        public static void Postfix(ref int __result, Activities._activity act)
        {
            int output = __result;
            if (act.lvl < 3 && output >= 3 && CountEpisodesOfShowsWithType(Shows._param._media_type.internet) < lvl3Eps)
            {
                output = 2;
            }
            else if (act.lvl < 4 && output >= 4 &&
                (Shows.GetPeakAudience(Shows._param._media_type.internet) < lvl4audience || CountEpisodesOfShowsWithType(Shows._param._media_type.radio) < lvl4Eps))
            {
                output = 3;
            }
            else if (act.lvl < 6 && output >= 6 && CountEpisodesOfShowsWithType(Shows._param._media_type.tv) < lvl6Eps)
            {
                output = 5;
            }
            else if (act.lvl < 9 && output >= 9 && data_girls.GetNumberOfIdolsWithFame(lvl9Fame) < lvl9IdolCount)
            {
                output = 8;
            }
            __result = output;
        }
    }

    // In hard mode, level 3 requires 6 internet episodes, level 4 requires 6000 internet show viewers and 6 radio episodes, level 6 requires 6 tv episodes,
    // level 9 Promotion requires 3 idols with 9 fame instead of 5
    [HarmonyPatch(typeof(Activities), "GetPromotionDescription")]
    public class Activities_GetPromotionDescription
    {
        private static string GenerateRequirementString(string label, long current, long required, string clr)
        {
            string reqStr = $"{label}: {ExtensionMethods.formatNumber(current)} / {ExtensionMethods.formatNumber(required)}";
            return ExtensionMethods.color(reqStr, current >= required ? mainScript.green : clr);
        }

        public static void Postfix(ref string __result, int lvl, int actualLvl)
        {

            string output = __result;
            string clr = mainScript.blue;
            if (actualLvl + 1 < lvl)
            {
                clr = mainScript.red;
            }

            string req1;
            long num1;
            string req2;
            long num2;
            switch (lvl)
            {
                case 3:
                    req1 = Language.Data["ACTIVITIES__MAGAZINES"];
                    num1 = actualLvl >= lvl ? lvl3mags : Math.Min(lvl3mags, Stats.data.business.photoshoot_counter);
                    req1 = GenerateRequirementString(req1, num1, lvl3mags, clr);

                    req2 = Language.Data[lvl3Label];
                    num2 = actualLvl >= lvl ? lvl3Eps : Math.Min(lvl3Eps, CountEpisodesOfShowsWithType(Shows._param._media_type.internet));
                    req2 = GenerateRequirementString(req2, num2, lvl3Eps, clr);

                    output = req1 + "\n" + req2;
                    break;
                case 4:
                    req1 = Language.Data["ACTIVITIES__INTERNETPEAK"];
                    num1 = actualLvl >= lvl ? lvl4audience : Math.Min(lvl4audience, Shows.GetPeakAudience(Shows._param._media_type.internet));
                    req1 = GenerateRequirementString(req1, num1, lvl4audience, clr);

                    req2 = Language.Data[lvl4Label];
                    num2 = actualLvl >= lvl ? lvl4Eps : Math.Min(lvl4Eps, CountEpisodesOfShowsWithType(Shows._param._media_type.radio));
                    req2 = GenerateRequirementString(req2, num2, lvl4Eps, clr);

                    output = req1 + "\n" + req2;
                    break;
                case 6:
                    req1 = Language.Data["ACTIVITIES__MAGAZINES"];
                    num1 = actualLvl >= lvl ? lvl6mags : Math.Min(lvl6mags, Stats.data.business.photoshoot_counter);
                    req1 = GenerateRequirementString(req1, num1, lvl6mags, clr);

                    req2 = Language.Data[lvl6Label];
                    num2 = actualLvl >= lvl ? lvl6Eps : Math.Min(lvl6Eps, CountEpisodesOfShowsWithType(Shows._param._media_type.tv));
                    req2 = GenerateRequirementString(req2, num2, lvl6Eps, clr);

                    output = req1 + "\n" + req2;
                    break;
                case 9:
                    req1 = $"{Language.Data["ACTIVITIES__IDOLSLEVEL"]} {lvl9Fame} {Language.Data["FAME"].ToLower()}";
                    num1 = actualLvl >= lvl ? lvl9IdolCount : Math.Min(lvl9IdolCount, data_girls.GetNumberOfIdolsWithFame(lvl9Fame));
                    req1 = GenerateRequirementString(req1, num1, lvl9IdolCount, clr);

                    output = req1;
                    break;
            }

            __result = output;
        }
    }

}
