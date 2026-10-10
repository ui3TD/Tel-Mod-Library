using HarmonyLib;
using System;
using static ConcertRebalance.ConcertRebalance;

namespace ConcertRebalance
{
    public class ConcertRebalance
    {
        // Attendance above PRICE_THRESHOLD falls by this factor per yen (×3 on Unfair): it halves about every ¥8,700
        public const double PRICE_SCALING_BASE = 1.00008;
        // Attendance in percent just above PRICE_THRESHOLD, a small step down from the game's straight line (5.71%).
        // Set so the Unfair Tokyo Coliseum sells out at a profit from about 2.2M fans.
        public const float ATTENDANCE_AT_THRESHOLD = 5.43f;
        // Price (×3 on Unfair) where the exponential falloff starts
        public const int PRICE_THRESHOLD = 9000;
        // Where the game leaves its straight line for its own high-price curve, which never falls to zero
        public const int GAME_LINE_END = 10000;
        public const int GAME_LINE_END_HARD = 6000;
        public const int DOME_CAPACITY_HARD = 50000;
        public const int DOME_PRICE_HARD = 200000000;
        // Unfair's ¥25M made the Open Air Stage the dearest seat in the game, so it never paid more than both its neighbours
        public const int OPEN_AIR_PRICE_HARD = 20000000;

        /// <summary>
        /// Attendance rate per fan after the price adjustment. The game's straight line runs up to PRICE_THRESHOLD
        /// (on Unfair the game leaves it earlier, so the mod continues it), then attendance falls exponentially towards zero.
        /// </summary>
        /// <param name="vanillaAttendance">The attendance rate computed by vanilla.</param>
        /// <param name="ticketPrice">The ticket price set by the player.</param>
        /// <param name="isHard">Whether the game is on hard difficulty.</param>
        public static float AdjustAttendance(float vanillaAttendance, int ticketPrice, bool isHard)
        {
            int ticketPriceFactor = isHard ? ticketPrice * 3 : ticketPrice;
            if (ticketPriceFactor <= PRICE_THRESHOLD)
            {
                if (ticketPriceFactor <= (isHard ? GAME_LINE_END_HARD : GAME_LINE_END))
                    return vanillaAttendance;
                return (-0.0007142857f * ticketPriceFactor + 12.142858f) / 100f;
            }

            return ATTENDANCE_AT_THRESHOLD * (float)Math.Pow(PRICE_SCALING_BASE, PRICE_THRESHOLD - ticketPriceFactor) / 100f;
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
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetAttendanceOfDemo))]
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
    /// Modifies the venue unlocking mechanism to require selling out the previous venue with a profit.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), nameof(SEvent_Concerts.UpdateVenueUnlocked))]
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
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish))]
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
    [HarmonyPatch(typeof(SEvent_Concerts), nameof(SEvent_Concerts.GetVenueCapacity))]
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
    /// Changes base costs in hard mode: Tokyo Coliseum up for more endgame challenge, Open Air Stage down so it has its turn.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), nameof(SEvent_Concerts.GetVenueBaseCost))]
    public class SEvent_Concerts_GetVenueBaseCost
    {
        /// <summary>
        /// Sets the hard mode base cost of Tokyo Coliseum to 200,000,000 and of the Open Air Stage to 20,000,000.
        /// </summary>
        /// <param name="val">The venue type being checked.</param>
        /// <param name="__result">The original calculated base cost.</param>
        public static void Postfix(SEvent_Concerts._venue val, ref int __result)
        {
            if (!staticVars.IsHard())
                return;
            if (val == SEvent_Concerts._venue.tokyoColiseum)
            {
                __result = DOME_PRICE_HARD;
            }
            else if (val == SEvent_Concerts._venue.openAirStage)
            {
                __result = OPEN_AIR_PRICE_HARD;
            }
        }
    }


}
