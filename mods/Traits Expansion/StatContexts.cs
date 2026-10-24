using HarmonyLib;
using System;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansion
{
    // Each prefix saves the flag as it was and its finalizer puts it back, rather than clearing it: the game
    // never calls one of these inside another, but another mod might, and clearing would switch the outer
    // calculation's trait bonuses off for the rest of it.
    // Apply traits to businesses
    [HarmonyPatch(typeof(business._proposal), nameof(business._proposal.GetGirlCoeff))]
    public class Business__proposal_GetGirlCoeff
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(out bool __state)
        {
            __state = patchGetVal;
            patchGetVal = true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix(data_girls.girls _girl, ref float __result, business._proposal __instance)
        {
            // Girls with Wooden Acting receive penalty
            if (__instance.type == business._type.tv_drama && _girl.trait == (traits._trait._type)NewTraits.Wooden_Acting)
            {
                __result -= WOODACTING_PENALTY;
            }
            // Girls with Wooden Acting receive penalty
            else if (__instance.type == business._type.variety && _girl.trait == (traits._trait._type)NewTraits.Quick_Wit)
            {
                __result += QUICKWIT_BONUS;
            }
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static Exception Finalizer(Exception __exception, bool __state)
        {
            patchGetVal = __state;
            return __exception;
        }
    }

    // Stat changes for shows
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GetAverageParam))]
    public class Data_girls_GetAverageParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(out bool __state)
        {
            __state = patchGetVal;
            patchGetVal = true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static Exception Finalizer(Exception __exception, bool __state)
        {
            patchGetVal = __state;
            return __exception;
        }
    }

    // Stat changes for shows (for team chemistray calc)
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.SenbatsuCalcParam))]
    public class Shows__show_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(out bool __state)
        {
            __state = patchGetVal;
            patchGetVal = true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static Exception Finalizer(Exception __exception, bool __state)
        {
            patchGetVal = __state;
            return __exception;
        }
    }

    // Stat changes for singles
    [HarmonyPatch(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam))]
    public class Singles__single_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(out bool __state)
        {
            __state = patchGetVal;
            patchGetVal = true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static Exception Finalizer(Exception __exception, bool __state)
        {
            patchGetVal = __state;
            return __exception;
        }
    }

    // Stat changes for concert songs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), nameof(SEvent_Concerts._concert._song.GetSkillValue))]
    public class SEvent_Concerts__concert__song_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(out bool __state)
        {
            __state = patchGetVal;
            patchGetVal = true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static Exception Finalizer(Exception __exception, bool __state)
        {
            patchGetVal = __state;
            return __exception;
        }
    }

    // Stat changes for concert MCs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), nameof(SEvent_Concerts._concert._mc.GetSkillValue))]
    public class SEvent_Concerts__concert__mc_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(out bool __state)
        {
            __state = patchGetVal;
            patchGetVal = true;
        }


        [HarmonyPriority(Priority.VeryLow)]
        public static Exception Finalizer(Exception __exception, bool __state)
        {
            patchGetVal = __state;
            return __exception;
        }
    }

    // Apply traits to parameters and skills
    [HarmonyPatch(typeof(data_girls.girls.param), nameof(data_girls.girls.param.GetVal))]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (!patchGetVal)
                return;

            __result += GetTraitModifier(__instance.Parent, __instance.type);
        }
    }
}
