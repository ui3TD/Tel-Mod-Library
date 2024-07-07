using HarmonyLib;
using System;
using TMPro;
using UnityEngine;
using static GoingViral.TrendingManager;

namespace GoingViral
{
    // display single triggering neg trending
    [HarmonyPatch(typeof(Single_Marketing_Roll), "OnFail")]
    public class Single_Marketing_Roll_OnFail
    {
        public static bool Prefix(ref TrendingManager.TrendingStatus __state)
        {
            __state = TrendingManager.IsTrending();
            return true;
        }
        public static void Postfix(ref Single_Marketing_Roll __instance, TrendingManager.TrendingStatus __state)
        {
            if (__state != TrendingStatus.none)
            {
                return;
            }

            singles._single single = Traverse.Create(__instance).Field("Single").GetValue() as singles._single;
            if (TrendingManager.IsTrending() == TrendingManager.TrendingStatus.crisis)
            {
                __instance.Description.GetComponent<TextMeshProUGUI>().text += "\n" + Language.Insert("NOTIF__CRISIS", single.GetGroup().Title);
                return;
            }
        }
    }
    // display single triggering neg trending
    [HarmonyPatch(typeof(Single_Marketing_Roll), "OnFailCrit")]
    public class Single_Marketing_Roll_OnFailCrit
    {
        public static bool Prefix(ref TrendingManager.TrendingStatus __state)
        {
            __state = TrendingManager.IsTrending();
            return true;
        }
        public static void Postfix(ref Single_Marketing_Roll __instance, TrendingManager.TrendingStatus __state)
        {
            if (__state != TrendingStatus.none)
            {
                return;
            }

            singles._single single = Traverse.Create(__instance).Field("Single").GetValue() as singles._single;
            if (TrendingManager.IsTrending() == TrendingManager.TrendingStatus.crisis)
            {
                __instance.Description.GetComponent<TextMeshProUGUI>().text += "\n" + Language.Insert("NOTIF__CRISIS", single.GetGroup().Title);
                return;
            }

            singles._param marketing = single.GetRiskyMarketing();

            float coeff = Rivals.GetSinglesCoeff(single);

            float p = TrendingManager.GetTrendingChance(marketing, single.Marketing_Result_Status, single.GetGroup(), coeff);
            if (mainScript.chance(p))
            {
                SetTrending(TrendingManager.GetTrendingMagnitude(marketing, single.Marketing_Result_Status));
                __instance.Description.GetComponent<TextMeshProUGUI>().text += "\n" + Language.Insert("NOTIF__CRISIS", single.GetGroup().Title);
            }
        }
    }
    // display single triggering trending
    [HarmonyPatch(typeof(Single_Marketing_Roll), "OnSuccessCrit")]
    public class Single_Marketing_Roll_OnSuccessCrit
    {
        public static bool Prefix(ref TrendingManager.TrendingStatus __state)
        {
            __state = TrendingManager.IsTrending();
            return true;
        }
        public static void Postfix(ref Single_Marketing_Roll __instance, TrendingManager.TrendingStatus __state)
        {
            if (__state != TrendingStatus.none)
            {
                return;
            }

            singles._single single = Traverse.Create(__instance).Field("Single").GetValue() as singles._single;
            singles._param marketing = single.GetRiskyMarketing();

            float coeff = Rivals.GetSinglesCoeff(single);

            float p = TrendingManager.GetTrendingChance(marketing, single.Marketing_Result_Status, single.GetGroup(), coeff);
            if (mainScript.chance(p))
            {
                SetTrending(TrendingManager.GetTrendingMagnitude(marketing, single.Marketing_Result_Status));
                __instance.Description.GetComponent<TextMeshProUGUI>().text += "\n" + Language.Insert("NOTIF__TRENDING", single.GetGroup().Title);
            }
        }
    }



    // chance of trending on crit success after 1 week
    [HarmonyPatch(typeof(Shows._show), "NewEpisode")]
    public class Shows__show_NewEpisode
    {
        public static void Postfix(Shows._show __instance)
        {
            if (IsTrending() != TrendingStatus.none || __instance.episodeCount != 2 || __instance.medium.media_type != Shows._param._media_type.tv)
            {
                return;
            }

            float p = TrendingManager.GetTrendingChance(__instance);
            if (mainScript.chance(p))
            {
                SetTrending(TrendingManager.GetTrendingMagnitude(__instance));
            }
        }
    }


    // chance of crisis upon scandal points
    [HarmonyPatch(typeof(data_girls.girls), "addParam")]
    public class data_girls_girls_addParam
    {
        public static void Postfix(data_girls._paramType type, float val)
        {
            if (IsTrending() != TrendingStatus.none)
            {
                return;
            }
            if (type != data_girls._paramType.scandalPoints || val <= 0)
            {
                return;
            }
            float p = TrendingManager.GetTrendingChance(val);
            if (mainScript.chance(p))
            {
                SetTrending(TrendingManager.GetTrendingMagnitude(val));
            }
        }
    }

    // chance of crisis upon scandal points
    [HarmonyPatch(typeof(resources), "_Add")]
    public class resources__Add
    {
        public static void Postfix(resources.type _type, long val)
        {
            if (IsTrending() != TrendingStatus.none)
            {
                return;
            }
            if (_type != resources.type.scandalPoints || val <= 0)
            {
                return;
            }
            float p = TrendingManager.GetTrendingChance(val);
            if (mainScript.chance(p))
            {
                SetTrending(TrendingManager.GetTrendingMagnitude(val));
            }
        }
    }

}
