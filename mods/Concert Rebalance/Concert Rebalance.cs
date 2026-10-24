using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
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
        // The game's own Unfair figures: the mod only replaces these, so another mod's own figure is kept
        public const int DOME_CAPACITY_HARD_GAME = 30000;
        public const int DOME_PRICE_HARD_GAME = 100000000;
        public const int OPEN_AIR_PRICE_HARD_GAME = 25000000;

        /// <summary>
        /// The game's attendance rate per fan, as GetAttendanceOfDemo works it out without any mod.
        /// </summary>
        /// <param name="ticketPrice">The ticket price set by the player.</param>
        /// <param name="isHard">Whether the game is on hard difficulty.</param>
        public static float GameAttendance(int ticketPrice, bool isHard)
        {
            int ticketPriceFactor = ticketPrice;
            int lineEnd = GAME_LINE_END;
            if (isHard)
            {
                ticketPriceFactor *= 3;
                lineEnd = GAME_LINE_END_HARD;
            }
            float percent;
            if (ticketPriceFactor < 3000)
                percent = -1f / 75f * (float)ticketPriceFactor + 50f;
            else if (ticketPriceFactor <= lineEnd)
                percent = -0.0007142857f * (float)ticketPriceFactor + 12.142858f;
            else
                percent = 0.8605844f + 4.4355736f / Mathf.Pow(1f + (float)ticketPriceFactor / 27004.95f, 2.641579f);
            return percent / 100f;
        }

        /// <summary>
        /// The mod's attendance rate per fan. The game's straight line runs up to PRICE_THRESHOLD (on Unfair the game
        /// leaves it earlier, so the mod continues it), then attendance falls exponentially towards zero.
        /// </summary>
        /// <param name="ticketPrice">The ticket price set by the player.</param>
        /// <param name="isHard">Whether the game is on hard difficulty.</param>
        public static float ModAttendance(int ticketPrice, bool isHard)
        {
            int ticketPriceFactor = isHard ? ticketPrice * 3 : ticketPrice;
            if (ticketPriceFactor <= PRICE_THRESHOLD)
            {
                if (ticketPriceFactor <= (isHard ? GAME_LINE_END_HARD : GAME_LINE_END))
                    return GameAttendance(ticketPrice, isHard);
                return (-0.0007142857f * ticketPriceFactor + 12.142858f) / 100f;
            }

            return ATTENDANCE_AT_THRESHOLD * (float)Math.Pow(PRICE_SCALING_BASE, PRICE_THRESHOLD - ticketPriceFactor) / 100f;
        }

        /// <summary>
        /// Attendance rate per fan after the price adjustment. The mod's curve is applied relative to the game's, so a
        /// change another mod makes to the rate is kept whichever patch runs first.
        /// </summary>
        /// <param name="incomingAttendance">The attendance rate computed by the game and any mod patched before this one.</param>
        /// <param name="ticketPrice">The ticket price set by the player.</param>
        /// <param name="isHard">Whether the game is on hard difficulty.</param>
        public static float AdjustAttendance(float incomingAttendance, int ticketPrice, bool isHard)
        {
            float game = GameAttendance(ticketPrice, isHard);
            float mod = ModAttendance(ticketPrice, isHard);
            // The game's own value comes back as exactly the mod's, with no float rounding from the ratio
            if (incomingAttendance == game)
                return mod;
            return incomingAttendance * (mod / game);
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

        // Whether the game's StartConcert is running, whose call to UpdateVenueUnlocked would unlock before the concert is played
        internal static bool concertStarting;

        // The concert being played and its idols' physical stamina when it started
        private static SEvent_Concerts._concert staminaConcert;
        private static Dictionary<data_girls.girls, float> startingStamina;

        /// <summary>
        /// Records each concert idol's physical stamina as the concert starts, replacing any earlier record.
        /// </summary>
        /// <param name="concert">The concert that is starting.</param>
        public static void RecordStartingStamina(SEvent_Concerts._concert concert)
        {
            staminaConcert = null;
            startingStamina = null;
            if (concert == null)
                return;

            Dictionary<data_girls.girls, float> stamina = new Dictionary<data_girls.girls, float>();
            foreach (data_girls.girls girl in concert.GetGirls())
            {
                if (girl != null)
                    stamina[girl] = girl.getParam(data_girls._paramType.physicalStamina)._val;
            }
            staminaConcert = concert;
            startingStamina = stamina;
        }

        /// <summary>
        /// Brings any idol back down to the physical stamina she had when this concert started. MC breaks restore stamina
        /// to every idol not in them, so a big cast could otherwise end a concert better rested than it began. Concerts the
        /// mod didn't see start are left alone.
        /// </summary>
        /// <param name="concert">The concert that is finishing.</param>
        public static void CapStaminaAtStart(SEvent_Concerts._concert concert)
        {
            if (startingStamina == null || concert != staminaConcert)
                return;

            Dictionary<data_girls.girls, float> stamina = startingStamina;
            staminaConcert = null;
            startingStamina = null;
            foreach (KeyValuePair<data_girls.girls, float> start in stamina)
            {
                // setParam, not addParam: addParam scales stamina loss by trait and limits it per day
                if (start.Key.getParam(data_girls._paramType.physicalStamina)._val > start.Value)
                    start.Key.setParam(data_girls._paramType.physicalStamina, start.Value);
            }
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
        /// Skips the unlock the game makes as a concert starts; the Finish postfix unlocks once the concert sold out
        /// without a loss. Calls from anywhere else, such as other mods, unlock as usual.
        /// </summary>
        /// <returns>Whether the game's own unlock runs.</returns>
        public static bool Prefix()
        {
            return !concertStarting;
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
        /// <param name="__runOriginal">Whether the game's Finish ran: another mod's prefix may have skipped it.</param>
        public static void Postfix(SEvent_Concerts._concert __instance, bool __runOriginal)
        {
            if (!__runOriginal || __instance.Status != SEvent_Tour.tour._status.finished)
                return;
            if (QualifiesForVenueUnlock(__instance.ProjectedValues.Actual_Attendance, __instance.ProjectedValues.GetActualProfit()))
            {
                SEvent_Concerts.UpdateVenueUnlocked(__instance);
            }

            return;
        }
    }

    /// <summary>
    /// Marks the game's StartConcert as running, so its call to UpdateVenueUnlocked doesn't unlock the next venue.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), nameof(SEvent_Concerts.StartConcert))]
    public class SEvent_Concerts_StartConcert
    {
        public static void Prefix()
        {
            concertStarting = true;
        }

        /// <summary>
        /// Runs before other mods' postfixes, so an unlock they make from theirs goes through.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Postfix()
        {
            concertStarting = false;
        }

        /// <summary>
        /// Also ends the window if StartConcert throws.
        /// </summary>
        public static void Finalizer()
        {
            concertStarting = false;
        }
    }

    /// <summary>
    /// Records the concert idols' stamina when a concert starts, for the cap when it finishes.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), nameof(SEvent_Concerts.StartConcert))]
    public class SEvent_Concerts_StartConcert_Stamina
    {
        /// <summary>
        /// Runs after the game's StartConcert and other mods' patches, so stamina they change as the concert starts counts
        /// as the idol's start.
        /// </summary>
        /// <param name="__instance">The concert manager starting its current concert.</param>
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SEvent_Concerts __instance)
        {
            RecordStartingStamina(__instance.Concert);
        }
    }

    /// <summary>
    /// Stops a concert leaving idols with more stamina than they started with. MC breaks still restore stamina during it.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish))]
    public class SEvent_Concerts__concert_Finish_StaminaCap
    {
        /// <summary>
        /// Runs before other mods' Finish patches, so stamina they give for finishing a concert isn't capped.
        /// </summary>
        /// <param name="__instance">The concert that has finished.</param>
        [HarmonyPriority(Priority.First)]
        public static void Prefix(SEvent_Concerts._concert __instance)
        {
            CapStaminaAtStart(__instance);
        }
    }

    /// <summary>
    /// Increases the capacity of Coliseum-level concert venues in hard mode.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts), nameof(SEvent_Concerts.GetVenueCapacity))]
    public class SEvent_Concerts_GetVenueCapacity
    {
        /// <summary>
        /// Modifies the venue capacity for Tokyo Coliseum in hard mode, unless another mod already set its own.
        /// </summary>
        /// <param name="val">The venue type being checked.</param>
        /// <param name="__result">The original calculated venue capacity.</param>
        public static void Postfix(SEvent_Concerts._venue val, ref int __result)
        {
            if (staticVars.IsHard() && val == SEvent_Concerts._venue.tokyoColiseum && __result == DOME_CAPACITY_HARD_GAME)
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
        /// Sets the hard mode base cost of Tokyo Coliseum to 200,000,000 and of the Open Air Stage to 20,000,000, unless
        /// another mod already set its own.
        /// </summary>
        /// <param name="val">The venue type being checked.</param>
        /// <param name="__result">The original calculated base cost.</param>
        public static void Postfix(SEvent_Concerts._venue val, ref int __result)
        {
            if (!staticVars.IsHard())
                return;
            if (val == SEvent_Concerts._venue.tokyoColiseum && __result == DOME_PRICE_HARD_GAME)
            {
                __result = DOME_PRICE_HARD;
            }
            else if (val == SEvent_Concerts._venue.openAirStage && __result == OPEN_AIR_PRICE_HARD_GAME)
            {
                __result = OPEN_AIR_PRICE_HARD;
            }
        }
    }


}
