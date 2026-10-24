using HarmonyLib;
using System;
using System.Collections.Generic;
using static TraitFix.TraitsFix;

namespace TraitFix
{
    // Trait stat contexts use a stack plus Finalizers so nested calls and exceptions cannot poison later GetVal calls.
    [HarmonyPatch(typeof(business._proposal), nameof(business._proposal.GetGirlCoeff))]
    public class Business__proposal_GetGirlCoeff
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => BeginTraitCalculation();

        public static void Postfix(data_girls.girls _girl, ref float __result, business._proposal __instance)
        {
            if (__instance != null && _girl != null && __instance.type == business._type.photoshoot && _girl.trait == traits._trait._type.Photogenic)
                __result += PHOTOGENIC_MODIFIER;
        }

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GetAverageParam))]
    public class Data_girls_GetAverageParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(List<data_girls.girls> Girls) => BeginTraitCalculation(Girls);

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.SenbatsuCalcParam))]
    public class Shows__show_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(List<data_girls.girls> _girls) => BeginTraitCalculation(_girls);

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam))]
    public class Singles__single_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => BeginTraitCalculation();

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), nameof(SEvent_Concerts._concert._song.GetSkillValue))]
    public class SEvent_Concerts__concert__song_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => BeginTraitCalculation();

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), nameof(SEvent_Concerts._concert._mc.GetSkillValue))]
    public class SEvent_Concerts__concert__mc_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => BeginTraitCalculation();

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls.girls.param), nameof(data_girls.girls.param.GetVal))]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (!IsTraitCalculationActive || __instance == null)
                return;
            __result += GetCurrentTraitModifier(__instance.Parent, __instance.type);
        }
    }
}
