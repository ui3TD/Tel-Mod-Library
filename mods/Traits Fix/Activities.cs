using HarmonyLib;
using System;
using System.Collections.Generic;
using static TraitFix.TraitsFix;

namespace TraitFix
{
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.AddCastParam))]
    public class Shows__show_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, Shows._show __instance)
        {
            if (type == data_girls._paramType.teamChemistry
                || __instance?.medium == null
                || __instance.medium.media_type != Shows._param._media_type.internet
                || !HasMemeQueen(girlList)
                || __instance.girlParams == null
                || __instance.girlParams.Count == 0)
                return;

            data_girls.girls.param param = __instance.girlParams[__instance.girlParams.Count - 1];
            if (param != null && param.type == type)
                param.val += MEME_INT_SHOW;
        }
    }

    [HarmonyPatch(typeof(Show_Popup), nameof(Show_Popup.AddCastParam))]
    public class Show_Popup_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, List<data_girls.girls.param> ___girlParams, Shows._param ___medium)
        {
            if (type == data_girls._paramType.teamChemistry
                || ___medium == null
                || ___medium.media_type != Shows._param._media_type.internet
                || !HasMemeQueen(girlList)
                || ___girlParams == null
                || ___girlParams.Count == 0)
                return;

            data_girls.girls.param param = ___girlParams[___girlParams.Count - 1];
            if (param != null && param.type == type)
                param.val += MEME_INT_SHOW;
        }
    }

    // Recalculate only when the medium changes, avoiding a recalculation on every SetParam.
    [HarmonyPatch(typeof(Show_Popup), nameof(Show_Popup.SetParam))]
    public class Show_Popup_SetParam
    {
        public static void Postfix(Show_Popup __instance, Show_Popup_Param_Button._type type, Shows._show._castType? ___castType)
        {
            if (__instance == null || type != Show_Popup_Param_Button._type.medium || ___castType == null)
                return;
            __instance.SetCastType(___castType.Value);
        }
    }

    [HarmonyPatch(typeof(singles._param), nameof(singles._param.GetSuccessChance), new Type[] { typeof(Single_Marketing_Roll._result), typeof(int), typeof(singles._single) })]
    public class Singles__param_GetSuccessChance
    {
        public static void Postfix(ref float __result, singles._param __instance, Single_Marketing_Roll._result Result, singles._single Single)
        {
            if (__instance == null || Single?.girls == null || __instance.Special_Type != singles._param._special_type.viral_campaign)
                return;

            bool hasMemeQueen = false;
            foreach (data_girls.girls girl in Single.girls)
            {
                if (girl != null && !girl.IsSick() && girl.trait == traits._trait._type.Meme_queen)
                {
                    hasMemeQueen = true;
                    break;
                }
            }
            if (!hasMemeQueen)
                return;

            // Do not patch fail directly: vanilla derives regular fail chance from the
            // success/crit chances, so subtracting 15 from fail double-counted this bonus.
            if (Result == Single_Marketing_Roll._result.success)
                __result += MEME_VIRAL_SUCCESS;
            else if (Result == Single_Marketing_Roll._result.success_crit)
                __result += MEME_VIRAL_SUCCESS_CRIT;
        }
    }

    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.SetStamina))]
    public class Shows__show_SetStamina
    {
        public static void Postfix(Shows._show __instance)
        {
            List<data_girls.girls> cast = __instance?.GetCast();
            if (cast == null || cast.Count == 0)
                return;

            float staminaCost = __instance.GetStaminaCost();
            int annoyingCount = 0;
            foreach (data_girls.girls girl in cast)
            {
                if (girl != null && girl.trait == traits._trait._type.Annoying && girl.IsActive())
                    annoyingCount++;
            }
            if (annoyingCount == 0)
                return;

            foreach (data_girls.girls girl in cast)
            {
                if (girl == null || !girl.IsActive())
                    continue;
                if (annoyingCount > 1 || girl.trait != traits._trait._type.Annoying)
                    girl.addParam(data_girls._paramType.physicalStamina, -staminaCost * ANNOYING_MODIFIER, false);
            }
        }
    }

    [HarmonyPatch(typeof(singles), nameof(singles.ReleaseSingle))]
    public class Singles_ReleaseSingle
    {
        public static void Postfix(singles._single single)
        {
            if (single?.girls == null)
                return;

            bool hasHandshake = single.IsIndividualHS() || single.IsGroupHS();
            if (!hasHandshake)
                return;

            foreach (data_girls.girls girl in single.girls)
            {
                if (girl != null && !girl.IsSick() && girl.trait == traits._trait._type.Misandry && mainScript.chance(MISANDRY_CHANCE))
                    girl.AddAppeal(resources.fanType.male, MISANDRY_MODIFIER);
            }
        }
    }

    // FinishTour clears Tour before returning, so capture the attendance result first.
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.FinishTour))]
    public class SEvent_Tour_FinishTour
    {
        public static void Prefix(SEvent_Tour __instance, ref bool __state)
        {
            __state = __instance?.Tour != null && __instance.Tour.GetAverageAttendance() < PERFECTIONIST_TOUR_ATT;
        }

        public static void Postfix(bool __state)
        {
            if (!__state)
                return;
            List<data_girls.girls> activeGirls = data_girls.GetActiveGirls();
            if (activeGirls == null)
                return;

            foreach (data_girls.girls girl in activeGirls)
            {
                if (girl != null && girl.trait == traits._trait._type.Perfectionist)
                    girl.getParam(data_girls._paramType.mentalStamina)?.add(PERFECTIONIST_MENTAL, false);
            }
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish))]
    public class SEvent_Concerts__concert_Finish
    {
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            if (__instance == null || __instance.Hype >= PERFECTIONIST_HYPE)
                return;
            List<data_girls.girls> girls = __instance.GetGirls(true);
            if (girls == null)
                return;

            foreach (data_girls.girls girl in girls)
            {
                if (girl != null && girl.trait == traits._trait._type.Perfectionist)
                    girl.getParam(data_girls._paramType.mentalStamina)?.add(PERFECTIONIST_MENTAL, false);
            }
        }
    }
}
