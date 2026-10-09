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
        public const float STALE_START = 30f;
        public const float STALE_MIN = 0.1f;
        public const float DECAYTIME_HARD = 183f;
        public const float DECAYTIME_NORMAL = 366f;
        public const double STREAM_PENALTY_HARD = 0.9;
        public const double STREAM_PENALTY_NORMAL = 0.7;

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

    // Theater show sales and subscriptions start to decay after 30 days, levelling off at 10%
    [HarmonyPatch(typeof(Theaters._theater), "GetPriceCoeff")]
    public class Theaters__theater_GetPriceCoeff
    {
        public static void Postfix(int Price, Theaters._theater __instance, ref float __result)
        {
            float output = __result;
            if (Price > 30000 || staticVars.IsEasy())
                return;

            int daysSinceSingle;
            singles._single latestSingle = singles.GetLatestReleasedSingle(false, __instance.GetGroup());
            if (latestSingle != null)
            {
                daysSinceSingle = (staticVars.dateTime - latestSingle.ReleaseData.ReleaseDate).Days;
            }
            else
            {
                daysSinceSingle = __instance.GetGroup().GetDaysSinceCreation();
            }
            if (daysSinceSingle > STALE_START)
            {
                if (staticVars.IsHard())
                {
                    output *= GetStaleness(daysSinceSingle, DECAYTIME_HARD);
                }
                else if(staticVars.IsNormal())
                {
                    output *= GetStaleness(daysSinceSingle, DECAYTIME_NORMAL);
                }
            }
            if (output < 0.001f)
            {
                output = 0.001f;
            }

            __result = output;
        }

        // Starts flat, falls through the middle and levels off at STALE_MIN.
        // 90% of the drop is done by decayTime; 99.4% by 1.5x that.
        public static float GetStaleness(int daysSinceSingle, float decayTime)
        {
            float t = (daysSinceSingle - STALE_START) / (decayTime - STALE_START);
            return STALE_MIN + (1f - STALE_MIN) * Mathf.Pow(10f, -t * t);
        }
    }

    // Theater subscription revenue decreased by 90% / 70%
    // Kept as long via double: revenue can pass int's ~2.1 billion limit
    [HarmonyPatch(typeof(Theaters._theater), "GetSubRevenue")]
    public class Theaters__theater_GetSubRevenue
    {
        public static void Postfix(ref long __result)
        {
            if (staticVars.IsHard())
            {
                __result = (long)Math.Round(__result * (1 - STREAM_PENALTY_HARD));
            }
            else if(staticVars.IsNormal())
            {
                __result = (long)Math.Round(__result * (1 - STREAM_PENALTY_NORMAL));
            }
        }
    }

    // Theater attendance increased for 'everyone', 'casual' and age groups
    [HarmonyPatch(typeof(Theaters._theater), "GetNumberOfVisitors")]
    public class Theaters__theater_GetNumberOfVisitors
    {
        // Before num *= GetPriceCoeff(Ticket_Price), replace the game's fan-type multiplier: num = Infix(this).
        // That statement is where the ticket-price check (over 40000 sells nothing) jumps to.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getPriceCoeff = AccessTools.Method(typeof(Theaters._theater), "GetPriceCoeff");
            FieldInfo ticketPrice = AccessTools.Field(typeof(Theaters._theater), nameof(Theaters._theater.Ticket_Price));

            CodeMatcher matcher = new CodeMatcher(instructions).MatchStartForward(
                new CodeMatch(ci => ci.IsLdloc()),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(ci => ci.LoadsField(ticketPrice)),
                new CodeMatch(ci => ci.Calls(getPriceCoeff)),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(ci => ci.IsStloc()));
            if (matcher.IsInvalid)
            {
                Debug.LogError("[Stale Theater Shows] Couldn't find where the game applies the ticket price to visitors; attendance uses the game's multipliers");
                return instructions;
            }
            CodeInstruction storeMultiplier = matcher.InstructionAt(6);

            // Take over the jump target, so the normal path runs the new multiplier
            CodeInstruction loadThis = new(OpCodes.Ldarg_0);
            loadThis.MoveLabelsFrom(matcher.Instruction);
            matcher.Insert(
                loadThis,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Theaters__theater_GetNumberOfVisitors), nameof(Infix))),
                new CodeInstruction(storeMultiplier.opcode, storeMultiplier.operand));
            return matcher.InstructionEnumeration();
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
