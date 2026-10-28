using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ConcertHype
{
    // The payout: the hype multiplier reads Revenue.RevenueHype instead of the concert's hype. The first read of
    // Hype (the recorded Actual_Hype) is kept, so the records show the show's score.
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.RecalcProjectedValues))]
    public class SEvent_Concerts__concert_RecalcProjectedValues
    {
        private const int ExpectedReads = 4;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            Revenue.TranspilerApplied = false;
            try
            {
                FieldInfo hype = AccessTools.Field(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Hype));
                MethodInfo revenueHype = AccessTools.Method(typeof(Revenue), nameof(Revenue.RevenueHype));
                if (code.Any(c => c.Calls(revenueHype)))
                {
                    // Already changed (the patch applied twice)
                    Revenue.TranspilerApplied = true;
                    return code;
                }
                List<int> reads = Enumerable.Range(0, code.Count).Where(i => code[i].LoadsField(hype)).ToList();
                if (reads.Count != ExpectedReads)
                {
                    Log.Warn($"the concert payout reads hype {reads.Count} times, not {ExpectedReads} (another mod changed it?); fixing the payout afterwards instead");
                    return code;
                }
                foreach (int i in reads.Skip(1))
                {
                    code[i].opcode = OpCodes.Call;
                    code[i].operand = revenueHype;
                }
                Revenue.TranspilerApplied = true;
            }
            catch (Exception ex)
            {
                Log.Warn("couldn't change the concert payout; fixing it afterwards instead: " + ex.Message);
            }
            return code;
        }

        // Only when the transpiler couldn't apply: the payout again at 200% hype.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            try
            {
                if (Revenue.TranspilerApplied || !Revenue.PaysFixedHype(__instance))
                    return;
                SEvent_Concerts._concert._projectedValues values = __instance.ProjectedValues;
                float tickets = variables.Get("FUJI_3_TICKETS") == "true" ? 1.05f : 1f;
                values.Actual_Revenue = (long)Mathf.Round(values.Actual_Audience * values.TicketPrice * Revenue.MultiplierAtFixedHype(__instance.Venue) * tickets);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("payout", "couldn't fix the concert payout: " + ex.Message);
            }
        }
    }

    // The setup popup's revenue estimate: while the game works it out, the projected hype reads 200%. Other mods
    // that reshape the estimate's hype (a curve, a ticket bonus) still apply to it; the Projected Hype label,
    // read outside the estimate, keeps the game's value.
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetRevenue))]
    public class SEvent_Concerts__concert__projectedValues_GetRevenue
    {
        [ThreadStatic]
        internal static int Depth;

        public static void Prefix() => Depth++;

        public static Exception Finalizer(Exception __exception)
        {
            Depth--;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetHype))]
    public class SEvent_Concerts__concert__projectedValues_GetHype
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SEvent_Concerts._concert._projectedValues __instance, ref float __result)
        {
            try
            {
                if (SEvent_Concerts__concert__projectedValues_GetRevenue.Depth > 0 && Revenue.PaysFixedHype(__instance.Parent))
                    __result = Revenue.FixedHype / 100f;
            }
            catch (Exception ex)
            {
                Log.WarnOnce("estimate", "couldn't change the concert revenue estimate: " + ex.Message);
            }
        }
    }
}
