using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StatLimits
{
    [HarmonyPatch(typeof(business._proposal), "GetGirlCoeff")]
    public class Business__proposal_GetGirlCoeff_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            __result = Mathf.Clamp(__result, 0f, 20f);
        }
    }

    [HarmonyPatch(typeof(data_girls), "GetAverageParam")]
    public class Data_girls_GetAverageParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            __result = Mathf.Clamp(__result, 0f, 100f);
        }
    }

    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class Singles__single_SenbatsuCalcParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref data_girls.girls.param __result)
        {
            if (__result != null)
                __result.val = Mathf.Clamp(__result.val, 0f, 100f);
        }
    }

    [HarmonyPatch(typeof(Shows._show), "SenbatsuCalcParam")]
    public class Shows__show_SenbatsuCalcParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref data_girls.girls.param __result)
        {
            if (__result != null)
                __result.val = Mathf.Clamp(__result.val, 0f, 100f);
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetSkillValue")]
    public class SEvent_Concerts__concert__song_GetSkillValue_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            __result = Math.Max(0, Math.Min(100, __result));
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetSkillValue")]
    public class SEvent_Concerts__concert__mc_GetSkillValue_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            __result = Math.Max(0, Math.Min(100, __result));
        }
    }

    [HarmonyPatch(typeof(Show_Popup), "AddCastParam")]
    public class Show_Popup_AddCastParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(List<data_girls.girls.param> ___girlParams)
        {
            if (___girlParams == null || ___girlParams.Count == 0)
                return;

            data_girls.girls.param param = ___girlParams[___girlParams.Count - 1];
            if (param != null)
                param.val = Mathf.Clamp(param.val, 0f, 100f);
        }
    }

    [HarmonyPatch(typeof(Shows._show), "AddCastParam")]
    public class Shows__show_AddCastParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Shows._show __instance)
        {
            if (__instance == null || __instance.girlParams == null || __instance.girlParams.Count == 0)
                return;

            data_girls.girls.param param = __instance.girlParams[__instance.girlParams.Count - 1];
            if (param != null)
                param.val = Mathf.Clamp(param.val, 0f, 100f);
        }
    }

    [HarmonyPatch(typeof(data_girls), "GetTeamChemistry")]
    public class data_girls_GetTeamChemistry_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            __result = Mathf.Clamp(__result, 0f, 100f);
        }
    }
}
