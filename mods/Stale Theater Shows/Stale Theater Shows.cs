using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using static StaleTheater.StaleTheater;

namespace StaleTheater
{
    public class StaleTheater
    {
        public const float DECAYTIME_HARD = 183f;
        public const float DECAYTIME_NORMAL = 366f;
        public const float STREAM_PENALTY_HARD = 0.9f;
        public const float STREAM_PENALTY_NORMAL = 0.7f;

        public const float THEATER_EVERYONE = 0.3f;
        public const float THEATER_CASUAL = 0.65f;
        public const float THEATER_HC = 1f;
        public const float THEATER_ADULT = 0.95f;
        public const float THEATER_YA = 0.95f;
        public const float THEATER_TEEN = 0.9f;
        public const float THEATER_GENDER = 0.75f;

        public const float MANZAI_EVERYONE = 0.2f;
        public const float MANZAI_NONHC_COEFF = 0.75f;
        public const float MANZAI_STAM_COEFF = 0.5f;
    }

    // Theater show sales and subscriptions start to decay after 30 days
    [HarmonyPatch(typeof(Theaters._theater), "GetPriceCoeff")]
    public class Theaters__theater_GetPriceCoeff
    {
        public static void Postfix(int Price, Theaters._theater __instance, ref float __result)
        {
            float output = __result;
            if (Price > 30000 || staticVars.IsEasy())
                return;

            int daysSinceSingle;
            LinearFunction._function linear = new();
            if(singles.GetLatestReleasedSingle(false, __instance.GetGroup()) != null)
            {
                daysSinceSingle = (staticVars.dateTime - singles.GetLatestReleasedSingle(false, __instance.GetGroup()).ReleaseData.ReleaseDate).Days;
            }
            else
            {
                daysSinceSingle = __instance.GetGroup().GetDaysSinceCreation();
            }
            if (daysSinceSingle > 30)
            {
                if (staticVars.IsHard())
                {
                    linear.Init(30f, 1f, DECAYTIME_HARD, 0.1f);
                    output *= linear.GetY(daysSinceSingle);
                }
                else if(staticVars.IsNormal())
                {
                    linear.Init(30f, 1f, DECAYTIME_NORMAL, 0.1f);
                    output *= linear.GetY(daysSinceSingle);
                }
            }
            if (output < 0.001f)
            {
                output = 0.001f;
            }

            __result = output;
        }
    }

    // Theater subscription revenue decreased by 90% / 70%
    [HarmonyPatch(typeof(Theaters._theater), "GetSubRevenue")]
    public class Theaters__theater_GetSubRevenue
    {
        public static void Postfix(ref long __result)
        {
            if (staticVars.IsHard())
            {
                __result = Mathf.RoundToInt(__result * (1 - STREAM_PENALTY_HARD));
            }
            else if(staticVars.IsNormal())
            {
                __result = Mathf.RoundToInt(__result * (1 - STREAM_PENALTY_NORMAL));
            }
        }
    }

    // Theater attendance increased for 'everyone', 'casual' and age groups
    [HarmonyPatch(typeof(Theaters._theater), "GetNumberOfVisitors")]
    public class Theaters__theater_GetNumberOfVisitors
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ret)
                {
                    index = i;
                }
                if (instructionList[i].opcode == OpCodes.Call && (MethodInfo)instructionList[i].operand == AccessTools.Method(typeof(Theaters._theater), "GetPriceCoeff"))
                {
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Theaters__theater_GetNumberOfVisitors), "Infix")));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Stloc_0));
            }

            return instructionList.AsEnumerable();
        }

        public static float Infix(Theaters._theater __this)
        {
            float multiplier;
            Theaters._theater._schedule schedule = __this.GetSchedule();

            if (schedule.FanType_Everyone)
            {
                multiplier = THEATER_EVERYONE;
            }
            else
            {
                multiplier = schedule.FanType switch
                {
                    resources.fanType.casual => THEATER_CASUAL,
                    resources.fanType.hardcore => THEATER_HC,
                    resources.fanType.adult => THEATER_ADULT,
                    resources.fanType.youngAdult => THEATER_YA,
                    resources.fanType.teen => THEATER_TEEN,
                    _ => THEATER_GENDER
                };

            }

            if (__this.Doing_Now == Theaters._theater._schedule._type.manzai)
            {
                if (schedule.FanType_Everyone)
                {
                    multiplier = MANZAI_EVERYONE;
                }
                else if (schedule.FanType != resources.fanType.hardcore)
                {
                    multiplier *= MANZAI_NONHC_COEFF;
                }
                multiplier *= MANZAI_STAM_COEFF;
            }

            return multiplier;
        }
    }

}
