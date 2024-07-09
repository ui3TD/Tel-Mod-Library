using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MBTI_Personalities
{

    // Limits for business proposal stats
    [HarmonyPatch(typeof(business._proposal), "GetGirlCoeff")]
    public class Business__proposal_GetGirlCoeff_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            __result = Mathf.Max(0, Mathf.Min(20, __result));
        }
    }


    // Limits for show stats
    [HarmonyPatch(typeof(data_girls), "GetAverageParam")]
    public class Data_girls_GetAverageParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            __result = Mathf.Max(0, Mathf.Min(100, __result));
        }
    }


    // Limits for single senbatsu stats
    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class Singles__single_SenbatsuCalcParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref data_girls.girls.param __result)
        {
            __result.val = Mathf.Max(0, Mathf.Min(100, __result.val));
        }
    }

    // Limits for show senbatsu stats
    [HarmonyPatch(typeof(Shows._show), "SenbatsuCalcParam")]
    public class Shows__show_SenbatsuCalcParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref data_girls.girls.param __result)
        {
            __result.val = Mathf.Max(0, Mathf.Min(100, __result.val));
        }
    }

    // Limits for stats of concert songs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetSkillValue")]
    public class SEvent_Concerts__concert__song_GetSkillValue_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            __result = Math.Max(0, Math.Min(100, __result));
        }
    }


    // Limits for stats for concert MCs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetSkillValue")]
    public class SEvent_Concerts__concert__mc_GetSkillValue_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            __result = Math.Max(0, Math.Min(100, __result));
        }
    }

    // Limit the show cast params
    [HarmonyPatch(typeof(Show_Popup), "AddCastParam")]
    public class Show_Popup_AddCastParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref List<data_girls.girls.param> ___girlParams)
        {
            ___girlParams.Last().val = Mathf.Max(0, Mathf.Min(100, ___girlParams.Last().val));
        }
    }

    // Limit the show cast params
    [HarmonyPatch(typeof(Shows._show), "AddCastParam")]
    public class Shows__show_AddCastParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref Shows._show __instance)
        {
            __instance.girlParams.Last().val = Mathf.Max(0, Mathf.Min(100, __instance.girlParams.Last().val));
        }
    }

    // Limit team chemistry
    [HarmonyPatch(typeof(data_girls), "GetTeamChemistry")]
    public class data_girls_GetTeamChemistry_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            __result = Mathf.Max(0, Mathf.Min(100, __result));
        }
    }
}
