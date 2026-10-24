using HarmonyLib;
using System;
using UnityEngine;
using static MBTIPersonalities.MBTIPersonalities;

namespace MBTIPersonalities
{
    /// <summary>
    /// Harmony patch for getting the appeal of a stat for a girl.
    /// Increases the appeal to hardcore fans if the girl has the ISFP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetAppealOfStat))]
    public class Data_girls_girls_GetAppealOfStat
    {

        /// <summary>
        /// Harmony patch for getting the appeal of a stat for a girl.
        /// Increases the appeal to hardcore fans if the girl has the ISFP MBTI type.
        /// </summary>
        /// <param name="__result">Calculated appeal of the stat.</param>
        /// <param name="_FanType">Type of fan being appealed to.</param>
        /// <param name="__instance">Instance of the girl being patched.</param>
        public static void Postfix(ref float __result, resources.fanType _FanType, data_girls.girls __instance)
        {
            // The fan type first: it's cheaper than the type lookup, and this runs for every idol and stat
            if (_FanType != resources.fanType.hardcore)
                return;

            if (GetGirlMBTI(__instance) == MBTI.ISFP)
            {
                __result *= 1 + ISFPBonus;
            }
        }
    }

    /// <summary>
    /// Harmony patch for generating sales for a single.
    /// Applies the INFP bonus to handshake events.
    /// </summary>
    [HarmonyPatch(typeof(singles), nameof(singles.GenerateSales))]
    public class singles_GenerateSales
    {
        /// <summary>
        /// Harmony patch for generating sales for a single.
        /// Applies the INFP bonus to handshake events.
        /// </summary>
        /// <param name="single">Single instance being patched.</param>
        public static void Prefix(singles._single single)
        {
            if(single.IsGroupHS() || single.IsIndividualHS())
            {
                patchGetFan_Count_INFP = true;
            }
        }

        /// <summary>
        /// Harmony patch for generating sales for a single.
        /// Applies the INFP bonus to handshake events.
        /// </summary>
        public static void Finalizer()
        {
            patchGetFan_Count_INFP = false;
        }
    }

    /// <summary>
    /// Harmony patch for getting the fan count for a girl.
    /// Increases the fan count if the girl has the INFP MBTI type and it's a handshake event.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetFan_Count), new Type[] { typeof(resources.fanType), typeof(resources.fanType), typeof(resources.fanType) })]
    public class data_girls_girls_GetFan_Count_INFP
    {

        /// <summary>
        /// Harmony patch for getting the fan count for a girl.
        /// Increases the fan count if the girl has the INFP MBTI type and it's a handshake event.
        /// </summary>
        /// <param name="__result">Calculated fan count.</param>
        /// <param name="__instance">Instance of the girl being patched.</param>
        public static void Postfix(ref long __result, data_girls.girls __instance)
        {
            if (!patchGetFan_Count_INFP)
                return;

            MBTI mBTI = GetGirlMBTI(__instance);
            if (mBTI != MBTI.INFP)
                return;

            __result = (long)Mathf.Round(__result * (1 + INFPBonus));
        }
    }

    /// <summary>
    /// Harmony patch for generating results for the SSK event.
    /// Applies the ENFJ bonus to vote counts.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_SSK._SSK), nameof(SEvent_SSK._SSK.GenerateResults))]
    public class SEvent_SSK__SSK_GenerateResults
    {
        /// <summary>
        /// Harmony patch for generating results for the SSK event.
        /// Applies the ENFJ bonus to vote counts.
        /// </summary>
        public static void Prefix()
        {
            patchGetFan_Count_ENFJ = true;
        }

        /// <summary>
        /// Harmony patch for generating results for the SSK event.
        /// Applies the ENFJ bonus to vote counts.
        /// </summary>
        public static void Finalizer()
        {
            patchGetFan_Count_ENFJ = false;
        }
    }

    /// <summary>
    /// Harmony patch for getting the fan count for a girl during the SSK event.
    /// Increases the fan count if the girl has the ENFJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetFan_Count), new Type[] { typeof(resources.fanType) })]
    public class data_girls_girls_GetFan_Count_ENFJ
    {
        /// <summary>
        /// Harmony patch for getting the fan count for a girl during the SSK event.
        /// Increases the fan count if the girl has the ENFJ MBTI type.
        /// </summary>
        /// <param name="__instance">Instance of the girl being patched.</param>
        /// <param name="__result">Calculated fan count.</param>
        public static void Postfix(data_girls.girls __instance, ref long __result)
        {
            if (!patchGetFan_Count_ENFJ)
                return;

            if (GetGirlMBTI(__instance) != MBTI.ENFJ)
                return;

            __result = (long)Mathf.Round(__result * (1 + ENFJBonus));
        }
    }
}
