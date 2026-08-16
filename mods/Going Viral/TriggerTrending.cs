using HarmonyLib;
using TMPro;
using UnityEngine;
using static GoingViral.TrendingManager;

namespace GoingViral
{
    internal static class TrendingRollUI
    {
        internal static singles._single GetSingle(Single_Marketing_Roll roll)
        {
            return roll == null ? null : Traverse.Create(roll).Field("Single").GetValue() as singles._single;
        }

        internal static void Append(Single_Marketing_Roll roll, string key, singles._single single)
        {
            if (roll == null || roll.Description == null || single == null)
                return;
            TextMeshProUGUI text = roll.Description.GetComponent<TextMeshProUGUI>();
            Groups._group group = single.GetGroup();
            if (text == null || group == null)
                return;
            text.text += "\n" + Language.Insert(key, new string[] { group.Title });
        }
    }

    [HarmonyPatch(typeof(Single_Marketing_Roll), "OnFail")]
    public class Single_Marketing_Roll_OnFail
    {
        public static void Prefix(ref TrendingStatus __state)
        {
            __state = IsTrending();
        }

        public static void Postfix(Single_Marketing_Roll __instance, TrendingStatus __state)
        {
            if (__state == TrendingStatus.none && IsTrending() == TrendingStatus.crisis)
                TrendingRollUI.Append(__instance, "NOTIF__CRISIS", TrendingRollUI.GetSingle(__instance));
        }
    }

    [HarmonyPatch(typeof(Single_Marketing_Roll), "OnFailCrit")]
    public class Single_Marketing_Roll_OnFailCrit
    {
        public static void Prefix(ref TrendingStatus __state)
        {
            __state = IsTrending();
        }

        public static void Postfix(Single_Marketing_Roll __instance, TrendingStatus __state)
        {
            if (__state != TrendingStatus.none)
                return;

            singles._single single = TrendingRollUI.GetSingle(__instance);
            if (single == null)
                return;

            // A fake-scandal resource change may already have started the crisis.
            if (IsTrending() == TrendingStatus.crisis)
            {
                TrendingRollUI.Append(__instance, "NOTIF__CRISIS", single);
                return;
            }

            singles._param marketing = single.GetRiskyMarketing();
            Groups._group group = single.GetGroup();
            float coeff = Rivals.GetSinglesCoeff(single);
            float chance = GetTrendingChance(marketing, single.Marketing_Result_Status, group, coeff);
            if (!mainScript.chance(chance))
                return;

            SetTrending(GetTrendingMagnitude(marketing, single.Marketing_Result_Status));
            if (IsTrending() == TrendingStatus.crisis)
                TrendingRollUI.Append(__instance, "NOTIF__CRISIS", single);
        }
    }

    [HarmonyPatch(typeof(Single_Marketing_Roll), "OnSuccessCrit")]
    public class Single_Marketing_Roll_OnSuccessCrit
    {
        public static void Prefix(ref TrendingStatus __state)
        {
            __state = IsTrending();
        }

        public static void Postfix(Single_Marketing_Roll __instance, TrendingStatus __state)
        {
            if (__state != TrendingStatus.none)
                return;

            singles._single single = TrendingRollUI.GetSingle(__instance);
            if (single == null)
                return;
            singles._param marketing = single.GetRiskyMarketing();
            Groups._group group = single.GetGroup();
            float coeff = Rivals.GetSinglesCoeff(single);
            float chance = GetTrendingChance(marketing, single.Marketing_Result_Status, group, coeff);
            if (!mainScript.chance(chance))
                return;

            SetTrending(GetTrendingMagnitude(marketing, single.Marketing_Result_Status));
            if (IsTrending() == TrendingStatus.trending)
                TrendingRollUI.Append(__instance, "NOTIF__TRENDING", single);
        }
    }

    [HarmonyPatch(typeof(Shows._show), "NewEpisode")]
    public class Shows__show_NewEpisode
    {
        public static void Postfix(Shows._show __instance)
        {
            if (__instance == null || __instance.medium == null || IsTrending() != TrendingStatus.none ||
                __instance.episodeCount != 2 || __instance.medium.media_type != Shows._param._media_type.tv)
                return;

            if (mainScript.chance(GetTrendingChance(__instance)))
                SetTrending(GetTrendingMagnitude(__instance));
        }
    }

    [HarmonyPatch(typeof(data_girls.girls), "addParam")]
    public class data_girls_girls_addParam
    {
        public static void Postfix(data_girls._paramType type, float val)
        {
            if (IsTrending() == TrendingStatus.none && type == data_girls._paramType.scandalPoints && val > 0f &&
                mainScript.chance(GetTrendingChance(val)))
                SetTrending(GetTrendingMagnitude(val));
        }
    }

    [HarmonyPatch(typeof(resources), "_Add")]
    public class resources__Add
    {
        public static void Postfix(resources.type _type, long val)
        {
            if (IsTrending() == TrendingStatus.none && _type == resources.type.scandalPoints && val > 0 &&
                mainScript.chance(GetTrendingChance(val)))
                SetTrending(GetTrendingMagnitude(val));
        }
    }
}
