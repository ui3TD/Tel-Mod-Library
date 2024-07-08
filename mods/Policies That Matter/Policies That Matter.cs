using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using static PoliciesThatMatter.PoliciesThatMatter;

namespace PoliciesThatMatter
{
    public class PoliciesThatMatter
    {
        public const float PERFORMANCE_ENERGETIC_BONUS = 1.5f;
        public const float PERFORMANCE_QUALITY_MENTAL = 0.5f;
        public const float PERFORMANCE_QUALITY_BONUS = 2f;
        public const float IMAGE_REBEL_PENALTY = 0.4f;
        public const float DATING_ALLOWED_PENALTY = 0.25f;


        public const int SNS_MENTAL_CHANCE = 10;
        public const int SNS_MENTAL_PENALTYCRIT = 40;
        public const int SNS_MENTAL_PENALTY = 20;
        public const int STREAMING_MENTAL_CHANCE = 5;
        public const int STREAMING_MENTAL_PENALTYCRIT = 20;
        public const int STREAMING_MENTAL_PENALTY = 10;
        public const int DATING_MENTAL_CHANCE = 10;
        public const int DATING_MENTAL_PENALTYCRIT = 20;
        public const int DATING_MENTAL_PENALTY = 10;

        public const int STREAMING_MONEY_CHANCE = 5;
        public const float STREAMING_MONEY_MAX = 100000;
        public const int SNS_FAN_CHANCE = 10;
        public const float SNS_FAN_MAX_HIGH = 100;
        public const float SNS_FAN_MAX_LOW = 20;

        public const float SECURITY_RELAX_STAM = 1.5f;
        public const float SECURITY_RESTRICT_STAM = 0.7f;
        public const float SECURITY_RELAX_HYPE = 1.25f;
        public const float SECURITY_RESTRICT_HYPE = 0.75f;
        public const float SECURITY_RELAX_HS = 1.5f;
        public const float SECURITY_RESTRICT_HS = 0.8f;

        public const int datingAge = 16;

        public const string DATING_NOTIF_LABEL = "POLICYMOD__IDOL__DATING";
    }

    // Performance: Energetic: 1.5x increase in performance profit
    [HarmonyPatch(typeof(Activities), "GetPerformanceMoneyPerLevel")]
    public class Activities_GetPerformanceMoneyPerLevel
    {
        public static void Postfix(bool forceHard, ref int __result)
        {
            if (forceHard || policies.GetSelectedPolicyValue(policies._type.performances).Value != policies._value.performances_energy)
                return;

            __result = Mathf.FloorToInt(__result * PERFORMANCE_ENERGETIC_BONUS);
        }
    }

    // Performance: Quality: -0.5 mental stamina each week when training
    [HarmonyPatch(typeof(agency._room), "DoGirlTraining")]
    public class agency__room_DoGirlTraining
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

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
            if (__this.girl == null)
                return;

            if (policies.GetSelectedPolicyValue(policies._type.performances).Value != policies._value.performances_quality)
                return;

            float mental = PERFORMANCE_QUALITY_MENTAL / 7 / Mathf.Floor(1440f / (float)(staticVars.dateTimeAddMinutesPerSecond / staticVars.dateTimeDivider));
            __this.girl.addParam(data_girls._paramType.mentalStamina, -mental, false);
        }
    }


    // Performance: Quality: 2x training speed
    [HarmonyPatch(typeof(data_girls.girls.param), "GetDuration")]
    public class data_girls_girls_param_GetDuration
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (__instance.type == data_girls._paramType.physicalStamina || __instance.type == data_girls._paramType.mentalStamina)
                return;

            if (policies.GetSelectedPolicyValue(policies._type.performances).Value != policies._value.performances_quality)
                return;

            __result /= PERFORMANCE_QUALITY_BONUS;

        }
    }


    // Background Check: Extensive: 100% for positive trait
    [HarmonyPatch(typeof(traits), "GetRandomTraitType")]
    public class traits_GetRandomTraitType
    {
        public static void Postfix(ref traits._trait._type __result)
        {
            if (policies.GetSelectedPolicyValue(policies._type.background_check).Value != policies._value.background_check_extensive)
                return;

            List<traits._trait> list = traits.GetPositiveTraits();
            __result = list[UnityEngine.Random.Range(0, list.Count)].Type;
            while (__result == traits._trait._type.Moonlighter || __result == traits._trait._type.Spoiled)
            {
                __result = list[UnityEngine.Random.Range(0, list.Count)].Type;
            }
        }
    }

    // Image: Rebellious: -40 appeal to adult
    // Dating: Allowed: -25 appeal to hardcore
    [HarmonyPatch(typeof(Shows), "GetBaseAppeal")]
    public class Shows_GetBaseAppeal
    {
        public static void Postfix(ref float __result, resources.fanType fanType)
        {
            if (policies.GetSelectedPolicyValue(policies._type.image).Value == policies._value.image_rebellious)
            {
                if (fanType == resources.fanType.adult)
                {
                    __result -= IMAGE_REBEL_PENALTY;
                }
            }
            
            if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_allowed)
            {
                if (fanType == resources.fanType.hardcore)
                {
                    __result -= DATING_ALLOWED_PENALTY;
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
                    __result -= IMAGE_REBEL_PENALTY;
                }
            }

            if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_allowed)
            {
                if (fanType == resources.fanType.hardcore)
                {
                    __result -= DATING_ALLOWED_PENALTY;
                }
            }
        }
    }

    // Dating: Forbidden: 10% chance for -10 mental each week after turning 16 (-1 /wk)
    // Dating: Ambiguous: 10% chance for -5 mental each week after turning 16 (-0.5 /wk)
    // Streaming: Controlled: 5% chance for -5 mental each week (-0.25 /wk)
    // Streaming: No Restrictions: 5% chance for -10 mental each week (-0.5 /wk)
    // Social Media: Controlled: 10% chance for -10 mental each week (-1 /wk)
    // Social Media: No Restrictions: 10% chance for -20 mental each week (-2 /wk)
    [HarmonyPatch(typeof(data_girls), "PoliciesStamina")]
    public class data_girls_PoliciesStamina
    {
        private static void AddStaminaNotification(data_girls.girls girl, int staminaDecrease, string policyName)
        {
            NotificationManager.AddNotification(
                $"{girl.GetName(true)}\n" +
                $"{Language.Data["MENTAL_STAMINA"]}: " +
                $"{ExtensionMethods.color(staminaDecrease + Language.Data["PT"], mainScript.red)}\n" +
                $"{ExtensionMethods.AddSquareBrackets(policyName)}",
                mainScript.red32,
                NotificationManager._notification._type.idol_stat_change
            );
        }

        public static bool Prefix()
        {
            policies._value SNS = policies.GetSelectedPolicyValue(policies._type.social_media).Value;
            policies._value streaming = policies.GetSelectedPolicyValue(policies._type.streaming).Value;
            policies._value dating = policies.GetSelectedPolicyValue(policies._type.dating).Value;

            foreach (data_girls.girls girls in data_girls.GetActiveGirls())
            {
                float cumulativeChange = 0f;
                int incrementChange = 0;

                if(mainScript.chance(SNS_MENTAL_CHANCE))
                {
                    if (SNS == policies._value.social_media_no_restrictions)
                    {
                        incrementChange = UnityEngine.Random.Range(1, SNS_MENTAL_PENALTYCRIT - 1) * -1;
                        AddStaminaNotification(girls, incrementChange, Language.Data["IDOL__POLICY_SNS"]);
                    }
                    else if (SNS == policies._value.social_media_premoderated)
                    {
                        incrementChange = UnityEngine.Random.Range(1, SNS_MENTAL_PENALTY - 1) * -1;
                        AddStaminaNotification(girls, incrementChange, Language.Data["IDOL__POLICY_SNS"]);
                    }
                    cumulativeChange += incrementChange;
                }

                incrementChange = 0;
                if (mainScript.chance(STREAMING_MENTAL_CHANCE))
                {
                    if (streaming == policies._value.streaming_no_restrictions)
                    {
                        incrementChange = UnityEngine.Random.Range(1, STREAMING_MENTAL_PENALTYCRIT - 1) * -1;
                        AddStaminaNotification(girls, incrementChange, Language.Data["IDOL__POLICY_STREAMING"]);
                    }
                    else if (streaming == policies._value.streaming_controlled)
                    {
                        incrementChange = UnityEngine.Random.Range(1, STREAMING_MENTAL_PENALTY - 1) * -1;
                        AddStaminaNotification(girls, incrementChange, Language.Data["IDOL__POLICY_STREAMING"]);
                    }
                    cumulativeChange += incrementChange;
                }

                incrementChange = 0;
                if (girls.age >= datingAge && mainScript.chance(DATING_MENTAL_CHANCE))
                {
                    if (dating == policies._value.dating_forbidden)
                    {
                        incrementChange = UnityEngine.Random.Range(1, DATING_MENTAL_PENALTYCRIT - 1) * -1;
                        AddStaminaNotification(girls, incrementChange, Language.Data[DATING_NOTIF_LABEL]);
                    }
                    else if (dating == policies._value.dating_ambiguous)
                    {
                        incrementChange = UnityEngine.Random.Range(1, DATING_MENTAL_PENALTY - 1) * -1;
                        AddStaminaNotification(girls, incrementChange, Language.Data[DATING_NOTIF_LABEL]);
                    }
                    cumulativeChange += incrementChange;

                }

                if (cumulativeChange != 0f)
                {
                    girls.addParam(data_girls._paramType.mentalStamina, cumulativeChange, false);
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
        private static void AddBonusNotification(data_girls.girls girl, int valueIncrease, string policyName)
        {
            NotificationManager.AddNotification(
                $"{girl.GetName(true)}\n" +
                $"{Language.Data["FANS"]}: " +
                $"{ExtensionMethods.color(valueIncrease + Language.Data["PT"], mainScript.green)}\n" +
                $"{ExtensionMethods.AddSquareBrackets(policyName)}",
                mainScript.green32,
                NotificationManager._notification._type.resource_change
            );
        }

        public static bool Prefix()
        {
            policies._value SNS = policies.GetSelectedPolicyValue(policies._type.social_media).Value;
            policies._value streaming = policies.GetSelectedPolicyValue(policies._type.streaming).Value;


            int moneySum = 0;
            float moneyRandom;
            foreach (data_girls.girls girls in data_girls.GetActiveGirls())
            {
                if (streaming == policies._value.streaming_no_restrictions && mainScript.chance(STREAMING_MONEY_CHANCE))
                {
                    moneyRandom = UnityEngine.Random.Range(80, 120) / 100f * STREAMING_MONEY_MAX;
                    moneySum += Mathf.RoundToInt((9.5f * girls.GetFameLevel() + 5f) / 100 * moneyRandom);
                }

                int incrementFans = 0;
                if(mainScript.chance(SNS_FAN_CHANCE))
                {
                    if (SNS == policies._value.social_media_no_restrictions)
                    {
                        incrementFans = Mathf.RoundToInt(UnityEngine.Random.Range(50, 150) / 100f * SNS_FAN_MAX_HIGH / 10 * (1 + girls.GetFameLevel()));
                        AddBonusNotification(girls, incrementFans, Language.Data["IDOL__POLICY_SNS"]);
                    }
                    else if (SNS == policies._value.social_media_premoderated)
                    {
                        incrementFans = Mathf.RoundToInt(UnityEngine.Random.Range(50, 150) / 100f * SNS_FAN_MAX_LOW / 10 * (1 + girls.GetFameLevel()));
                        AddBonusNotification(girls, incrementFans, Language.Data["IDOL__POLICY_SNS"]);
                    }
                }

                if (incrementFans != 0f)
                {
                    girls.AddFans(incrementFans);
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
            policies._value security = policies.GetSelectedPolicyValue(policies._type.security).Value;
            if (security == policies._value.security_relaxed)
            {
                __result = Mathf.Round(__result * SECURITY_RELAX_STAM);
            }
            else if (security == policies._value.security_restrictive)
            {
                __result = Mathf.Round(__result * SECURITY_RESTRICT_STAM);
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
            policies._value security = policies.GetSelectedPolicyValue(policies._type.security).Value;
            if (security == policies._value.security_relaxed)
            {
                __result = Mathf.Round(__result * SECURITY_RELAX_STAM);
            }
            else if (security == policies._value.security_restrictive)
            {
                __result = Mathf.Round(__result * SECURITY_RESTRICT_STAM);
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
            policies._value security = policies.GetSelectedPolicyValue(policies._type.security).Value;
            if (security == policies._value.security_relaxed)
            {
                __result *= SECURITY_RELAX_HYPE;
            }
            else if (security == policies._value.security_restrictive)
            {
                __result *= SECURITY_RESTRICT_HYPE;
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
            policies._value security = policies.GetSelectedPolicyValue(policies._type.security).Value;
            if (security == policies._value.security_relaxed)
            {
                __result *= SECURITY_RELAX_HYPE;
            }
            else if (security == policies._value.security_restrictive)
            {
                __result *= SECURITY_RESTRICT_HYPE;
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
            if (__instance.GetAppealParam(fanType) == null || !__instance.IsHandshake())
                return;

            policies._value security = policies.GetSelectedPolicyValue(policies._type.security).Value;
            if (security == policies._value.security_relaxed)
            {
                __result /= 2f;
                __result *= SECURITY_RELAX_HS;
            }
            else if (security == policies._value.security_restrictive)
            {
                __result /= 0.5f;
                __result *= SECURITY_RESTRICT_HS;
            }
        }
    }
}
