using AICore;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace AIFanChatter
{
    // Every patch reads, or changes an argument or a constant; none skips a game method. Each catches its
    // own errors, so a problem in the mod never stops the game's method.

    // The moment patches run last, so they read the values after other mods' postfixes changed them, and
    // check that the moment really happened, in case another mod's prefix skipped the game's method.

    [HarmonyPatch(typeof(singles), nameof(singles.ReleaseSingle))]
    [HarmonyPriority(Priority.Last)]
    public class singles_ReleaseSingle
    {
        public static void Postfix(singles._single single, bool __runOriginal)
        {
            if (__runOriginal && single != null && single.status == singles._single._status.released)
            {
                Moments.Trigger(() => MomentContext.Single(single));
            }
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish))]
    [HarmonyPriority(Priority.Last)]
    public class SEvent_Concerts_concert_Finish
    {
        public static void Postfix(SEvent_Concerts._concert __instance, bool __runOriginal)
        {
            if (__runOriginal && __instance != null && __instance.Status == SEvent_Tour.tour._status.finished)
            {
                // Its election starts as the concert's screen closes: both go out together when the results end
                if (__instance.SSK != null && __instance.SSK.Status != SEvent_Tour.tour._status.finished)
                {
                    ConcertElection.Wait(__instance, staticVars.dateTime);
                    return;
                }
                Moments.Trigger(() => MomentContext.Concert(__instance));
            }
        }

        /// <summary>
        /// A concert still waiting a day after it ended: its election never finished, so it goes out alone.
        /// </summary>
        public static void OnTimeTick()
        {
            try
            {
                SEvent_Concerts._concert concert = ConcertElection.TakeOverdue(staticVars.dateTime);
                if (concert != null)
                {
                    Moments.Trigger(() => MomentContext.Concert(concert));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }
    }

    /// <summary>
    /// A tour, when the player closes its results: every stop has played by then. The game clears its
    /// current tour on the way out, so the prefix keeps it.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.FinishTour))]
    [HarmonyPriority(Priority.Last)]
    public class SEvent_Tour_FinishTour
    {
        // The last tour posted, so a second call for the same tour posts nothing. The tour itself rather than its
        // ID: a reloaded save can reuse an ID for a different tour.
        private static SEvent_Tour.tour posted;

        internal static void Reset() => posted = null;

        public static void Prefix(SEvent_Tour __instance, out SEvent_Tour.tour __state)
        {
            __state = null;
            try
            {
                __state = __instance?.Tour;
            }
            catch (Exception)
            {
                // No request
            }
        }

        public static void Postfix(SEvent_Tour.tour __state, bool __runOriginal)
        {
            try
            {
                if (__runOriginal && __state == null)
                {
                    Log.Warn("A tour finished, but the game had no current tour to read; no posts for it.");
                }
                if (ShouldPost(__state, __runOriginal))
                {
                    posted = __state;
                    Moments.Trigger(() => MomentContext.Tour(__state));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(SEvent_Tour.tour tour, bool ran)
        {
            return ran && tour != null && tour.Status == SEvent_Tour.tour._status.finished && tour != posted;
        }
    }
}
