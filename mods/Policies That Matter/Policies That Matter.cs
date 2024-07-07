using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace PoliciesThatMatter
{

    // Performance: Energetic: 1.5x increase in performance profit
    [HarmonyPatch(typeof(Activities), "GetPerformanceMoneyPerLevel")]
    public class Activities_GetPerformanceMoneyPerLevel
    {
        public static void Postfix(int lvl, bool forceHard, ref int __result)
        {
            if (!forceHard && policies.GetSelectedPolicyValue(policies._type.performances).Value == policies._value.performances_energy)
            {
                __result = Mathf.FloorToInt(__result * 1.5f);
            }
        }
    }

    // Performance: Quality: -0.5 mental stamina each week when training
    [HarmonyPatch(typeof(agency._room), "DoGirlTraining")]
    public class agency__room_DoGirlTraining
    {
        //public static bool Prefix(agency._room __instance, ref float __state)
        //{
        //    if(__instance.girl != null)
        //    {
        //        __state = __instance.girl.GetPhysicalStamina();
        //    }
        //    return true;
        //}
        //public static void Postfix(ref agency._room __instance, ref float __state)
        //{
        //    float newState;
        //    if (__instance.girl != null)
        //    {
        //        newState = __instance.girl.GetPhysicalStamina();
        //        if (newState != __state)
        //        {
        //            if (policies.GetSelectedPolicyValue(policies._type.performances).Value == policies._value.performances_quality)
        //            {
        //                float mental = 0.5f / 7f / Mathf.Floor(1440 / (50 / 4));
        //                __instance.girl.addParam(data_girls._paramType.mentalStamina, -mental, false);
        //            }
        //        }
        //    }
        //}
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);

            int index = -1;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Stloc_S)
                {
                    index = i;
                }
                if (instructionList[i].opcode == OpCodes.Call && (MethodInfo)instructionList[i].operand == AccessTools.Method(typeof(policies), "GetSelectedPolicyValue"))
                {
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(agency__room_DoGirlTraining), "Infix")));
            }

            return instructionList.AsEnumerable();
        }

        public static void Infix(agency._room __this)
        {
            if(__this.girl != null)
            {
                if (policies.GetSelectedPolicyValue(policies._type.performances).Value == policies._value.performances_quality)
                {
                    float mental = 0.5f / Mathf.Floor(1440f / (float)(staticVars.dateTimeAddMinutesPerSecond / staticVars.dateTimeDivider));
                    __this.girl.addParam(data_girls._paramType.mentalStamina, -mental, false);
                }
            }
        }
    }


    // Performance: Quality: 2x training speed
    [HarmonyPatch(typeof(data_girls.girls.param), "GetDuration")]
    public class data_girls_girls_param_GetDuration
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (__instance.type != data_girls._paramType.physicalStamina && __instance.type != data_girls._paramType.mentalStamina)
            {
                if (policies.GetSelectedPolicyValue(policies._type.performances).Value == policies._value.performances_quality)
                {
                    __result *= 0.5f;
                }
            }
        }
    }


    // Background Check: Extensive: 100% for positive trait
    [HarmonyPatch(typeof(traits), "GetRandomTraitType")]
    public class traits_GetRandomTraitType
    {
        public static void Postfix(ref traits._trait._type __result)
        {
            if (policies.GetSelectedPolicyValue(policies._type.background_check).Value == policies._value.background_check_extensive)
            {
                List<traits._trait> list = traits.GetPositiveTraits();
                __result = list[UnityEngine.Random.Range(0, list.Count)].Type;
                while (__result == traits._trait._type.Moonlighter || __result == traits._trait._type.Spoiled)
                {
                    __result = list[UnityEngine.Random.Range(0, list.Count)].Type;
                }
            }
        }
    }

    // Image: Rebellious: -40 appeal to adult
    // Dating: Allowed: -30 appeal to hardcore
    [HarmonyPatch(typeof(Shows), "GetBaseAppeal")]
    public class Shows_GetBaseAppeal
    {
        public static void Postfix(ref float __result, resources.fanType fanType)
        {
            if (policies.GetSelectedPolicyValue(policies._type.image).Value == policies._value.image_rebellious)
            {
                if (fanType == resources.fanType.adult)
                {
                    __result -= 0.4f;
                }
            }
            else if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_allowed)
            {
                if (fanType == resources.fanType.hardcore)
                {
                    __result -= 0.15f;
                }
            }
        }
    }

    // Image: Rebellious: -40 appeal to adult
    // Dating: Allowed: 25 appeal penalty of shows/singles to hardcore
    [HarmonyPatch(typeof(singles._single), "GetBaseAppeal")]
    public class singles__single_GetBaseAppeal
    {
        public static void Postfix(ref float __result, resources.fanType fanType)
        {
            if (policies.GetSelectedPolicyValue(policies._type.image).Value == policies._value.image_rebellious)
            {
                if (fanType == resources.fanType.adult)
                {
                    __result -= 0.4f;
                }
            }
            else if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_allowed)
            {
                if (fanType == resources.fanType.hardcore)
                {
                    __result -= 0.1f;
                }
            }
        }
    }

    // Dating: Forbidden: 10% chance for -10 mental each week after turning 16
    // Dating: Ambiguous: 10% chance for -5 mental each week after turning 16
    // Streaming: Controlled: 5% chance for -5 mental each week
    // Streaming: No Restrictions: 5% chance for -10 mental each week
    // Social Media: Controlled: 10% chance for -10 mental each week
    // Social Media: No Restrictions: 10% chance for -20 mental each week
    [HarmonyPatch(typeof(data_girls), "PoliciesStamina")]
    public class data_girls_PoliciesStamina
    {
        public static bool Prefix()
        {
            foreach (data_girls.girls girls in data_girls.girl)
            {
                if (girls.status != data_girls._status.graduated)
                {
                    float num = 0f;
                    int num2;
                    if (policies.GetSelectedPolicyValue(policies._type.social_media).Value == policies._value.social_media_no_restrictions && mainScript.chance(10))
                    {
                        num2 = UnityEngine.Random.Range(1, 39) * -1;
                        NotificationManager.AddNotification(string.Concat(new string[]
                        {
                            girls.GetName(true),
                            "\n",
                            Language.Data["MENTAL_STAMINA"],
                            ": ",
                            ExtensionMethods.color(num2 + Language.Data["PT"], mainScript.red),
                            "\n",
                            ExtensionMethods.AddSquareBrackets(Language.Data["IDOL__POLICY_SNS"])
                        }), mainScript.red32, NotificationManager._notification._type.idol_stat_change);
                        num += (float)num2;
                    }
                    else if (policies.GetSelectedPolicyValue(policies._type.social_media).Value == policies._value.social_media_premoderated && mainScript.chance(10))
                    {
                        num2 = UnityEngine.Random.Range(1, 19) * -1;
                        NotificationManager.AddNotification(string.Concat(new string[]
                        {
                            girls.GetName(true),
                            "\n",
                            Language.Data["MENTAL_STAMINA"],
                            ": ",
                            ExtensionMethods.color(num2 + Language.Data["PT"], mainScript.red),
                            "\n",
                            ExtensionMethods.AddSquareBrackets(Language.Data["IDOL__POLICY_SNS"])
                        }), mainScript.red32, NotificationManager._notification._type.idol_stat_change);
                        num += (float)num2;
                    }
                    if (policies.GetSelectedPolicyValue(policies._type.streaming).Value == policies._value.streaming_no_restrictions && mainScript.chance(5))
                    {
                        num2 = UnityEngine.Random.Range(1, 19) * -1;
                        NotificationManager.AddNotification(string.Concat(new string[]
                        {
                            girls.GetName(true),
                            "\n",
                            Language.Data["MENTAL_STAMINA"],
                            ": ",
                            ExtensionMethods.color(num2 + Language.Data["PT"], mainScript.red),
                            "\n",
                            ExtensionMethods.AddSquareBrackets(Language.Data["IDOL__POLICY_STREAMING"])
                        }), mainScript.red32, NotificationManager._notification._type.idol_stat_change);
                        num += (float)num2;
                    }
                    else if (policies.GetSelectedPolicyValue(policies._type.streaming).Value == policies._value.streaming_controlled && mainScript.chance(5))
                    {
                        num2 = UnityEngine.Random.Range(1, 9) * -1;
                        NotificationManager.AddNotification(string.Concat(new string[]
                        {
                            girls.GetName(true),
                            "\n",
                            Language.Data["MENTAL_STAMINA"],
                            ": ",
                            ExtensionMethods.color(num2 + Language.Data["PT"], mainScript.red),
                            "\n",
                            ExtensionMethods.AddSquareBrackets(Language.Data["IDOL__POLICY_STREAMING"])
                        }), mainScript.red32, NotificationManager._notification._type.idol_stat_change);
                        num += (float)num2;
                    }
                    if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_forbidden && girls.age >= 16 && mainScript.chance(10))
                    {
                        num2 = UnityEngine.Random.Range(1, 19) * -1;
                        NotificationManager.AddNotification(string.Concat(new string[]
                        {
                            girls.GetName(true),
                            "\n",
                            Language.Data["MENTAL_STAMINA"],
                            ": ",
                            ExtensionMethods.color(num2 + Language.Data["PT"], mainScript.red),
                            "\n",
                            ExtensionMethods.AddSquareBrackets(Language.Data["POLICYMOD__IDOL__DATING"])
                        }), mainScript.red32, NotificationManager._notification._type.idol_stat_change);
                        num += (float)num2;
                    }
                    else if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_ambiguous && girls.age >= 16 && mainScript.chance(10))
                    {
                        num2 = UnityEngine.Random.Range(1, 9) * -1;
                        NotificationManager.AddNotification(string.Concat(new string[]
                        {
                            girls.GetName(true),
                            "\n",
                            Language.Data["MENTAL_STAMINA"],
                            ": ",
                            ExtensionMethods.color(num2 + Language.Data["PT"], mainScript.red),
                            "\n",
                            ExtensionMethods.AddSquareBrackets(Language.Data["POLICYMOD__IDOL__DATING"])
                        }), mainScript.red32, NotificationManager._notification._type.idol_stat_change);
                        num += (float)num2;
                    }
                    if (num != 0f)
                    {
                        girls.addParam(data_girls._paramType.mentalStamina, num, false);
                    }
                }
            }

            return false;
        }
    }

    // Streaming: No Restrictions: 5% chance to get money each week
    // Social Media: No Restrictions: 10% chance for 1100 fans/week/girl (max fame)
    // Social Media: Controlled: 10% chance for 220 fans/week/girl (max fame)
    [HarmonyPatch(typeof(data_girls), "PoliciesResources")]
    public class data_girls_PoliciesResources
    {
        public static bool Prefix()
        {
            int moneySum = 0;
            foreach (data_girls.girls girls in data_girls.GetActiveGirls(null))
            {
                if (policies.GetSelectedPolicyValue(policies._type.streaming).Value == policies._value.streaming_no_restrictions && mainScript.chance(5))
                {
                    moneySum += Mathf.RoundToInt((9.5f * (float)girls.GetFameLevel() + 5f) * 1000f);
                }
                long num = 0;
                int num2;
                if (policies.GetSelectedPolicyValue(policies._type.social_media).Value == policies._value.social_media_no_restrictions && mainScript.chance(10))
                {
                    num2 = (int)((float)UnityEngine.Random.Range(50, 150) / 100f * (10f + 10f * (float)girls.GetFameLevel()));
                    NotificationManager.AddNotification(string.Concat(new string[]
                    {
                        girls.GetName(true),
                        "\n",
                        Language.Data["FANS"],
                        ": ",
                        ExtensionMethods.color(num2.ToString(), mainScript.green),
                        "\n",
                        ExtensionMethods.AddSquareBrackets(Language.Data["IDOL__POLICY_SNS"])
                    }), mainScript.red32, NotificationManager._notification._type.idol_stat_change);
                    num += (long)num2;
                }
                else if (policies.GetSelectedPolicyValue(policies._type.social_media).Value == policies._value.social_media_premoderated && mainScript.chance(10))
                {
                    num2 = (int)((float)UnityEngine.Random.Range(50, 150) / 100f * (2f + 2f * (float)girls.GetFameLevel()));
                    NotificationManager.AddNotification(string.Concat(new string[]
                    {
                        girls.GetName(true),
                        "\n",
                        Language.Data["FANS"],
                        ": ",
                        ExtensionMethods.color(num2.ToString(), mainScript.green),
                        "\n",
                        ExtensionMethods.AddSquareBrackets(Language.Data["IDOL__POLICY_SNS"])
                    }), mainScript.red32, NotificationManager._notification._type.resource_change);
                    num += (long)num2;
                }
                if (num != 0f)
                {
                    girls.AddFans(num);
                }
            }
            if (moneySum > 0)
            {
                resources.Add(resources.type.money, moneySum);
                NotificationManager.AddNotification(string.Concat(new string[]
                {
                Language.Data["MONEY"],
                ": ",
                ExtensionMethods.color("+" + ExtensionMethods.formatMoney(moneySum, false, false), mainScript.green),
                "\n",
                ExtensionMethods.AddSquareBrackets(Language.Data["IDOL__POLICY_STREAMING"])
                }), mainScript.green32, NotificationManager._notification._type.resource_change);
            }

            return true;
        }
    }

    // Security: Relaxed: 1.5x concert stamina
    // Security: Restrictive: 0.7x concert stamina
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetStaminaCost")]
    public class SEvent_Concerts__concert__song_GetStaminaCost
    {
        public static void Postfix(ref float __result)
        {
            if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_relaxed)
            {
                __result *= 1.5f;
                __result = Mathf.Round(__result);
            }
            else if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_restrictive)
            {
                __result *= 0.7f;
                __result = Mathf.Round(__result);
            }
        }
    }

    // Security: Relaxed: 1.5x concert stamina
    // Security: Restrictive: 0.7x concert stamina
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetStaminaCost")]
    public class SEvent_Concerts__concert__mc_GetStaminaCost
    {
        public static void Postfix(ref float __result)
        {
            if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_relaxed)
            {
                __result *= 1.5f;
                __result = Mathf.Round(__result);
            }
            else if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_restrictive)
            {
                __result *= 0.7f;
                __result = Mathf.Round(__result);
            }
        }
    }

    // Security: Relaxed: 1.25x concert hype
    // Security: Restrictive: 0.75x concert hype
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetHype")]
    public class SEvent_Concerts__concert__song_GetHype
    {
        public static void Postfix(ref float __result)
        {
            if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_relaxed)
            {
                __result *= 1.25f;
            }
            else if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_restrictive)
            {
                __result *= 0.75f;
            }
        }
    }

    // Security: Relaxed: 1.25x concert hype
    // Security: Restrictive: 0.75x concert hype
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetHype")]
    public class SEvent_Concerts__concert__mc_GetHype
    {
        public static void Postfix(ref float __result)
        {
            if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_relaxed)
            {
                __result *= 1.25f;
            }
            if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_restrictive)
            {
                __result *= 0.75f;
            }
        }
    }

    // Security: Relaxed: 1.5x appeal to handshakes
    // Security: Restrictive: 0.8x appeal to handshakes
    [HarmonyPatch(typeof(singles._param), "GetAppealVal")]
    public class singles__param_GetAppealVal
    {
        public static void Postfix(ref float __result, resources.fanType fanType, singles._param __instance)
        {
            if (__instance.GetAppealParam(fanType) != null && __instance.IsHandshake())
            {
                if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_relaxed)
                {
                    __result /= 2f;
                    __result *= 1.5f;
                }
                if (policies.GetSelectedPolicyValue(policies._type.security).Value == policies._value.security_restrictive)
                {
                    __result /= 0.5f;
                    __result *= 0.8f;
                }
            }
        }
    }
}
