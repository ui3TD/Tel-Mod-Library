using HarmonyLib;
using System;
using UnityEngine;
using static ConcertRebalance.ConcertRebalance;

namespace ConcertRebalance
{
    public class ConcertRebalance
    {
        public const float priceScalingBase = 1.0001f;
        public const float attendanceMultiplierBase = 4.99f;
        public const float attendanceMultiplierHard = 7.85f;
        public const int coliseumCapacityHard = 50000;
        public const int coliseumPriceHard = 200000000;

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
            float calculatedAttendance = __result;

            int ticketPriceFactor = __instance.TicketPrice;
            int basePriceThreshold = 10000;
            float attendanceMultiplier = attendanceMultiplierBase;
            if (staticVars.IsHard())
            {
                ticketPriceFactor *= 3;
                basePriceThreshold = 6000;
                attendanceMultiplier = attendanceMultiplierHard;
            }

            if (ticketPriceFactor >= 3000 && ticketPriceFactor > basePriceThreshold)
            {
                calculatedAttendance = attendanceMultiplier * Mathf.Pow(priceScalingBase, -(float)ticketPriceFactor + basePriceThreshold) / 100f;
            }

            __result = calculatedAttendance;
        }
    }

    /// <summary>
    /// Reduces the concert hype multiplier specifically for Club venues to balance smaller concerts.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert), "RecalcProjectedValues")]
    public class SEvent_Concerts__concert_RecalcProjectedValues
    {
        /// <summary>
        /// Recalculates projected values for Club venues, applying a reduced hype multiplier.
        /// </summary>
        /// <param name="__instance">The instance of the concert class.</param>
        public static void Postfix(ref SEvent_Concerts._concert __instance)
        {
            if (__instance.Venue != SEvent_Concerts._venue.club || __instance.Hype <= 100f)
                return;

            float num;
            float num2 = __instance.Hype - 100f;
            LinearFunction._function function = new();
            function.Init(0f, 50f, 100f, 25f);
            float num3 = function.GetY(num2) / 100f;
            num = num2 * num3 / 100f + 1f;
            float num4 = 1f;
            if (variables.Get("FUJI_3_TICKETS") == "true")
            {
                num4 = 1.05f;
            }
            __instance.ProjectedValues.Actual_Revenue = (long)Mathf.Round(__instance.ProjectedValues.Actual_Audience * __instance.ProjectedValues.TicketPrice * num * num4);


            return;
        }
    }

    /// <summary>
    /// Modifies the venue unlocking mechanism to require selling out the previous venue with a profit.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), "UpdateVenueUnlocked")]
    public class SEvent_Concerts_UpdateVenueUnlocked
    {
        /// <summary>
        /// Prevents the default venue unlocking behavior, replaced by a more stringent check in the Finish method.
        /// </summary>
        /// <param name="_Concert">The concert instance being checked for venue unlocking.</param>
        /// <returns>Always returns false to skip the original method.</returns>
        public static bool Prefix(SEvent_Concerts._concert _Concert)
        {
            if (_Concert.Status == SEvent_Tour.tour._status.finished && SEvent_Concerts.UnlockedVenue != SEvent_Concerts._venue.tokyoColiseum && _Concert.Venue == SEvent_Concerts.UnlockedVenue)
            {
                SEvent_Concerts.UnlockedVenue++;
            }

            return false;
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
            if (__instance.ProjectedValues.Actual_Attendance >= 1f && __instance.ProjectedValues.GetActualProfit() >= 0L)
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
                __result = coliseumCapacityHard;
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
                __result = coliseumPriceHard;
            }
        }
    }


}
