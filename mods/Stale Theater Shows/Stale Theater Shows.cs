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
        public const double STREAM_PENALTY_HARD = 0.95;
        public const double STREAM_PENALTY_NORMAL = 0.7;

        // Attendance per targeted fan scales with how hard single and show appeal make that audience to build: the
        // hardcore share it can reach with optimal play (~92%) / the target's (casual ~90%, a gender ~85-88%, adult ~85%,
        // teen ~70%), so every fully built audience pays about the same. Young adults (~40-45%: nothing targets them)
        // get teen's value; full compensation would make an unbuilt young adult target the best pick.
        // Everyone is for not steering the fanbase: a little more than the best target of untargeted play
        // (~42% hardcore, so casual at 0.58 of all fans), and well under any built audience.
        public const float THEATER_EVERYONE = 0.6f;
        public const float THEATER_CASUAL = 1f;
        public const float THEATER_HC = 1f;
        public const float THEATER_GENDER = 1.05f;
        public const float THEATER_ADULT = 1.1f;
        public const float THEATER_TEEN = 1.3f;
        public const float THEATER_YA = 1.3f;

        // Manzai is for saving stamina and training funny/smart: just below parity with performances late, where
        // full houses favour the cheaper show (parity ~0.19 for a built audience), and about half of them early.
        public const float MANZAI_STAM_COEFF = 0.18f;
    }

    // Theater show sales and subscriptions start to decay after 30 days, levelling off at 10%
    [HarmonyPatch(typeof(Theaters._theater), nameof(Theaters._theater.GetPriceCoeff))]
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
            // Staleness stops at 0.1%, or at the incoming value if another mod already set it lower
            __result = Mathf.Max(output, Mathf.Min(__result, 0.001f));
        }

        // Starts flat, falls through the middle and levels off at STALE_MIN.
        // 90% of the drop is done by decayTime; 99.4% by 1.5x that.
        public static float GetStaleness(int daysSinceSingle, float decayTime)
        {
            float t = (daysSinceSingle - STALE_START) / (decayTime - STALE_START);
            return STALE_MIN + (1f - STALE_MIN) * Mathf.Pow(10f, -t * t);
        }
    }

    // Theater subscription revenue decreased by 95% / 70%
    // Kept as long via double: revenue can pass int's ~2.1 billion limit
    [HarmonyPatch(typeof(Theaters._theater), nameof(Theaters._theater.GetSubRevenue))]
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

    // Theater attendance per targeted fan depends on how hard that audience is to build, so any built audience pays
    [HarmonyPatch(typeof(Theaters._theater), nameof(Theaters._theater.GetNumberOfVisitors))]
    public class Theaters__theater_GetNumberOfVisitors
    {
        // Before num *= GetPriceCoeff(Ticket_Price), adjust the game's fan-type multiplier: num = Infix(num, this).
        // That statement is where the ticket-price check (over 40000 sells nothing) jumps to.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getPriceCoeff = AccessTools.Method(typeof(Theaters._theater), nameof(Theaters._theater.GetPriceCoeff));
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
            CodeInstruction loadMultiplier = matcher.Instruction;
            CodeInstruction storeMultiplier = matcher.InstructionAt(6);

            // Take over the jump target, so the normal path runs the new multiplier
            CodeInstruction loadGameMultiplier = new(loadMultiplier.opcode, loadMultiplier.operand);
            loadGameMultiplier.MoveLabelsFrom(loadMultiplier);
            matcher.Insert(
                loadGameMultiplier,
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Theaters__theater_GetNumberOfVisitors), nameof(Infix))),
                new CodeInstruction(storeMultiplier.opcode, storeMultiplier.operand));
            return matcher.InstructionEnumeration();
        }

        /// <summary>
        /// The mod's multiplier when the game's is vanilla's. If another mod changed the game's multiplier,
        /// its change is kept in proportion: 1.5x vanilla becomes 1.5x the mod's.
        /// </summary>
        public static float Infix(float gameMultiplier, Theaters._theater __this)
        {
            float multiplier = ModMultiplier(__this);
            float vanilla = VanillaMultiplier(__this);
            if (Math.Abs(gameMultiplier - vanilla) <= vanilla * 1e-6f)
            {
                return multiplier;
            }
            return gameMultiplier * multiplier / vanilla;
        }

        /// <summary>
        /// Vanilla's fan-type multiplier in GetNumberOfVisitors. Never 0.
        /// </summary>
        public static float VanillaMultiplier(Theaters._theater __this)
        {
            Theaters._theater._schedule schedule = __this.GetSchedule();
            bool manzai = __this.Doing_Now == Theaters._theater._schedule._type.manzai;
            if (schedule.FanType_Everyone)
            {
                return manzai ? 0.1f : 0.2f;
            }

            float multiplier = schedule.FanType switch
            {
                resources.fanType.casual => 0.3f,
                resources.fanType.teen => 0.5f,
                resources.fanType.hardcore => 1f,
                _ => 0.75f
            };
            if (manzai && schedule.FanType != resources.fanType.hardcore)
            {
                multiplier *= 0.75f;
            }
            return multiplier;
        }

        public static float ModMultiplier(Theaters._theater __this)
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

            // Manzai draws the same share of every audience
            if (__this.Doing_Now == Theaters._theater._schedule._type.manzai)
            {
                multiplier *= MANZAI_STAM_COEFF;
            }

            return multiplier;
        }
    }

}
