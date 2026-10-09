using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static ConcertRebalance.ConcertRebalance;

namespace ConcertRebalance
{
    public class ConcertRebalance
    {
        public const float PRICE_SCALING_BASE = 1.0001f;
        public const float ATTENDANCE_MULT_BASE = 4.99f;
        public const float ATTENDANCE_MULT_HARD = 7.85f;
        public const int DOME_CAPACITY_HARD = 50000;
        public const int DOME_PRICE_HARD = 200000000;
        // The game's variable for the FUJI ticket deal (+5% ticket revenue)
        public const string FUJI_TICKETS_VARIABLE = "FUJI_3_TICKETS";

        /// <summary>
        /// Attendance rate per fan after the price adjustment. Above the price threshold, replaces vanilla's curve with an exponential falloff.
        /// </summary>
        /// <param name="vanillaAttendance">The attendance rate computed by vanilla.</param>
        /// <param name="ticketPrice">The ticket price set by the player.</param>
        /// <param name="isHard">Whether the game is on hard difficulty.</param>
        public static float AdjustAttendance(float vanillaAttendance, int ticketPrice, bool isHard)
        {
            int ticketPriceFactor = ticketPrice;
            int basePriceThreshold = 10000;
            float attendanceMultiplier = ATTENDANCE_MULT_BASE;
            if (isHard)
            {
                ticketPriceFactor *= 3;
                basePriceThreshold = 6000;
                attendanceMultiplier = ATTENDANCE_MULT_HARD;
            }

            if (ticketPriceFactor >= 3000 && ticketPriceFactor > basePriceThreshold)
            {
                return attendanceMultiplier * Mathf.Pow(PRICE_SCALING_BASE, -(float)ticketPriceFactor + basePriceThreshold) / 100f;
            }

            return vanillaAttendance;
        }

        /// <summary>
        /// Revenue multiplier for hype above 100%, using the same diminishing curve vanilla applies to non-club venues.
        /// </summary>
        /// <param name="hype">The concert hype, in percent.</param>
        public static float HypeMultiplierAbove100(float hype)
        {
            float num2 = hype - 100f;
            LinearFunction._function function = new();
            function.Init(0f, 50f, 100f, 25f);
            float num3 = function.GetY(num2) / 100f;
            return num2 * num3 / 100f + 1f;
        }

        /// <summary>
        /// Club concert revenue with the diminishing hype multiplier applied.
        /// </summary>
        /// <param name="audience">The number of attendees.</param>
        /// <param name="ticketPrice">The ticket price set by the player.</param>
        /// <param name="hype">The concert hype, in percent (above 100).</param>
        /// <param name="fujiTickets">Whether the FUJI ticket deal (FUJI_TICKETS_VARIABLE) is active.</param>
        public static long ClubRevenue(long audience, int ticketPrice, float hype, bool fujiTickets)
        {
            float num = HypeMultiplierAbove100(hype);
            float num4 = 1f;
            if (fujiTickets)
            {
                num4 = 1.05f;
            }
            return (long)Mathf.Round(audience * ticketPrice * num * num4);
        }

        /// <summary>
        /// Whether a finished concert qualifies to unlock the next venue: sold out and not at a loss.
        /// </summary>
        /// <param name="actualAttendance">The fraction of the venue filled.</param>
        /// <param name="actualProfit">The concert's profit.</param>
        public static bool QualifiesForVenueUnlock(float actualAttendance, long actualProfit)
        {
            return actualAttendance >= 1f && actualProfit >= 0L;
        }
    }

    /// <summary>
    /// Modifies the concert revenue formula to adjust attendance calculations based on ticket price and game difficulty.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), "GetAttendanceOfDemo")]
    public class SEvent_Concerts__concert__projectedValues_GetAttendanceOfDemo
    {
        /// <summary>
        /// Adjusts the projected attendance calculation based on ticket price and game difficulty.
        /// </summary>
        /// <param name="__result">The original calculated attendance value.</param>
        /// <param name="__instance">The instance of the projected values class.</param>
        public static void Postfix(ref float __result, SEvent_Concerts._concert._projectedValues __instance)
        {
            __result = AdjustAttendance(__result, __instance.TicketPrice, staticVars.IsHard());
        }
    }

    /// <summary>
    /// Gives Club venues the game's diminishing hype multiplier above 100%. The game works out a concert's
    /// revenue with <c>if (Hype &lt;= 100f || Venue == _venue.club)</c> linear hype, else the curve; this removes
    /// the club condition, so clubs take the curve like every other venue.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.RecalcProjectedValues))]
    public class SEvent_Concerts__concert_RecalcProjectedValues
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo hype = AccessTools.Field(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Hype));
            FieldInfo venue = AccessTools.Field(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Venue));

            // Hype <= 100f (to the linear branch), then this.Venue == club (0): brtrue skips to the curve
            CodeMatcher matcher = new CodeMatcher(instructions).MatchStartForward(
                new CodeMatch(ci => ci.LoadsField(hype)),
                new CodeMatch(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float value && value == 100f),
                new CodeMatch(ci => ci.opcode == OpCodes.Ble || ci.opcode == OpCodes.Ble_S || ci.opcode == OpCodes.Ble_Un || ci.opcode == OpCodes.Ble_Un_S),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(ci => ci.LoadsField(venue)),
                new CodeMatch(ci => ci.opcode == OpCodes.Brtrue || ci.opcode == OpCodes.Brtrue_S));
            if (matcher.IsInvalid)
            {
                // Already gone: this patch ran twice
                if (new CodeMatcher(instructions).MatchStartForward(
                        new CodeMatch(ci => ci.LoadsField(hype)),
                        new CodeMatch(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float value && value == 100f),
                        new CodeMatch(ci => ci.opcode == OpCodes.Ble || ci.opcode == OpCodes.Ble_S || ci.opcode == OpCodes.Ble_Un || ci.opcode == OpCodes.Ble_Un_S),
                        new CodeMatch(OpCodes.Br)).IsValid)
                    return instructions;

                Debug.LogError("[Concert Rebalance] Couldn't find the club hype check in RecalcProjectedValues; clubs keep the game's linear hype payout");
                return instructions;
            }

            // ldarg.0; ldfld Venue; brtrue curve  becomes  br curve
            object curve = matcher.InstructionAt(5).operand;
            matcher.Advance(3);
            matcher.Instruction.opcode = OpCodes.Br;
            matcher.Instruction.operand = curve;
            matcher.Advance(1).RemoveInstructions(2);
            return matcher.InstructionEnumeration();
        }
    }

    /// <summary>
    /// Applies the reduced club hype multiplier to the concert's projected revenue, so the estimate matches what the club pays.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), "GetRevenue")]
    public class SEvent_Concerts__concert__projectedValues_GetRevenue
    {
        /// <summary>
        /// Recalculates projected revenue for Club venues above 100% hype. A postfix, so it overrides any other mod's estimate for clubs.
        /// </summary>
        /// <param name="__result">The projected revenue from the game or other mods.</param>
        /// <param name="__instance">The instance of the projected values class.</param>
        public static void Postfix(ref long __result, SEvent_Concerts._concert._projectedValues __instance)
        {
            float hype = __instance.GetHype() * 100f;
            if (__instance.Parent.Venue != SEvent_Concerts._venue.club || hype <= 100f)
                return;

            __result = ClubRevenue(
                __instance.GetNumberOfSoldTickets(),
                __instance.TicketPrice,
                hype,
                variables.Get(FUJI_TICKETS_VARIABLE) == "true");
        }
    }

    /// <summary>
    /// Modifies the venue unlocking mechanism to require selling out the previous venue with a profit.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), "UpdateVenueUnlocked")]
    public class SEvent_Concerts_UpdateVenueUnlocked
    {
        /// <summary>
        /// Lets the game unlock the next venue only for a finished concert. The game calls this when a concert
        /// starts; the Finish postfix calls it again once the concert sold out without a loss.
        /// </summary>
        /// <param name="_Concert">The concert instance being checked for venue unlocking.</param>
        /// <returns>Whether the game's own unlock runs.</returns>
        public static bool Prefix(SEvent_Concerts._concert _Concert)
        {
            return _Concert.Status == SEvent_Tour.tour._status.finished;
        }
    }

    /// <summary>
    /// Implements the new venue unlocking criteria based on selling out and profitability.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert), "Finish")]
    public class SEvent_Concerts__concert_Finish
    {
        /// <summary>
        /// Checks if the concert sold out and was profitable before unlocking the next venue.
        /// </summary>
        /// <param name="__instance">The instance of the concert that has finished.</param>
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            if (QualifiesForVenueUnlock(__instance.ProjectedValues.Actual_Attendance, __instance.ProjectedValues.GetActualProfit()))
            {
                SEvent_Concerts.UpdateVenueUnlocked(__instance);
            }

            return;
        }
    }

    /// <summary>
    /// Increases the capacity of Coliseum-level concert venues in hard mode.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), "GetVenueCapacity")]
    public class SEvent_Concerts_GetVenueCapacity
    {
        /// <summary>
        /// Modifies the venue capacity for Tokyo Coliseum in hard mode.
        /// </summary>
        /// <param name="val">The venue type being checked.</param>
        /// <param name="__result">The original calculated venue capacity.</param>
        public static void Postfix(SEvent_Concerts._venue val, ref int __result)
        {
            if (staticVars.IsHard() && val == SEvent_Concerts._venue.tokyoColiseum)
            {
                __result = DOME_CAPACITY_HARD;
            }
        }
    }

    /// <summary>
    /// Increases the base cost of Coliseum-level concert venues in hard mode.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), "GetVenueBaseCost")]
    public class SEvent_Concerts_GetVenueBaseCost
    {
        /// <summary>
        /// Modifies the base cost for Tokyo Coliseum in hard mode to 200,000,000.
        /// </summary>
        /// <param name="val">The venue type being checked.</param>
        /// <param name="__result">The original calculated base cost.</param>
        public static void Postfix(SEvent_Concerts._venue val, ref int __result)
        {
            if (staticVars.IsHard() && val == SEvent_Concerts._venue.tokyoColiseum)
            {
                __result = DOME_PRICE_HARD;
            }
        }
    }


}
