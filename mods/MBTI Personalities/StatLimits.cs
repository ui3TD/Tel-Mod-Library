using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace MBTI_Personalities
{

    // Limits for business proposal stats
    [HarmonyPatch(typeof(business._proposal), "GetGirlCoeff")]
    public class Business__proposal_GetGirlCoeff_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            float output = __result;
            if (output > 20f)
            {
                output = 20f;
            }
            else if (output < 0f)
            {
                output = 0f;
            }
            __result = output;
        }
    }


    // Limits for show stats
    [HarmonyPatch(typeof(data_girls), "GetAverageParam")]
    public class Data_girls_GetAverageParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            float output = __result;
            if (output < 0)
            {
                output = 0;
            }
            else if (output > 100)
            {
                output = 100;
            }
            __result = output;
        }
    }


    // Limits for single senbatsu stats
    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class Singles__single_SenbatsuCalcParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref data_girls.girls.param __result)
        {
            if (__result.val < 0f)
            {
                __result.val = 0f;
            }
            else if (__result.val > 100f)
            {
                __result.val = 100f;
            }

        }
    }

    // Limits for show senbatsu stats
    [HarmonyPatch(typeof(Shows._show), "SenbatsuCalcParam")]
    public class Shows__show_SenbatsuCalcParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref data_girls.girls.param __result)
        {

            if (__result.val < 0f)
            {
                __result.val = 0f;
            }
            else if (__result.val > 100f)
            {
                __result.val = 100f;
            }

        }
    }

    // Limits for stats of concert songs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetSkillValue")]
    public class SEvent_Concerts__concert__song_GetSkillValue_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            int output = __result;

            if (output < 0)
            {
                output = 0;
            }
            else if (output > 100)
            {
                output = 100;
            }
            __result = output;

        }
    }


    // Limits for stats for concert MCs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetSkillValue")]
    public class SEvent_Concerts__concert__mc_GetSkillValue_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            int output = __result;

            if (output < 0)
            {
                output = 0;
            }
            else if (output > 100)
            {
                output = 100;
            }


            __result = output;
        }
    }

    // Limit the show cast params
    [HarmonyPatch(typeof(Show_Popup), "AddCastParam")]
    public class Show_Popup_AddCastParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref Show_Popup __instance)
        {
            List<data_girls.girls.param> girlParams = Traverse.Create(__instance).Field("girlParams").GetValue() as List<data_girls.girls.param>;

            float val = girlParams.Last().val;
            if (val > 100)
            {
                val = 100;
            }
            else if (val < 0)
            {
                val = 0;
            }
            girlParams.Last().val = val;
            Traverse.Create(__instance).Field("girlParams").SetValue(girlParams);
        }
    }

    // Limit the show cast params
    [HarmonyPatch(typeof(Shows._show), "AddCastParam")]
    public class Shows__show_AddCastParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref Shows._show __instance)
        {
            float val = __instance.girlParams.Last().val;
            if (val > 100)
            {
                val = 100;
            }
            else if (val < 0)
            {
                val = 0;
            }
            __instance.girlParams.Last().val = val;
        }
    }

    // Limit team chemistry
    [HarmonyPatch(typeof(data_girls), "GetTeamChemistry")]
    public class data_girls_GetTeamChemistry_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            if (__result > 100)
            {
                __result = 100;
            }
            else if (__result < 0)
            {
                __result = 0;
            }
        }
    }
}
