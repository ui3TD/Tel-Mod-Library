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

    /// <summary>
    /// A show, when the player launches it: its first episode airs at once.
    /// </summary>
    [HarmonyPatch(typeof(Shows), nameof(Shows.ReleaseShow))]
    [HarmonyPriority(Priority.Last)]
    public class Shows_ReleaseShow
    {
        // The last show posted, so a second call for it posts nothing
        private static Shows._show posted;

        internal static void Reset() => posted = null;

        // The game names the argument __show, a name Harmony keeps for its own values, so it's taken by position
        public static void Postfix(Shows._show __0, bool __runOriginal)
        {
            try
            {
                Shows._show show = __0;
                if (ShouldPost(show, __runOriginal))
                {
                    posted = show;
                    Moments.Trigger(() => MomentContext.ShowStart(show));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(Shows._show show, bool ran)
        {
            return ran && show != null && show.status == Shows._show._status.released && show.episodeCount == 1 && show != posted;
        }
    }

    /// <summary>
    /// A show that aired for a year or more, when it really ends: the call that cancels it, at once or at its
    /// 12th episode, not the one that only marks a TV or radio show to end later. A show whose whole cast
    /// left ends here too, with no one to post about.
    /// </summary>
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.Cancel))]
    [HarmonyPriority(Priority.Last)]
    public class Shows__show_Cancel
    {
        public static void Prefix(Shows._show __instance, out bool __state)
        {
            __state = __instance != null && __instance.status != Shows._show._status.canceled;
        }

        public static void Postfix(Shows._show __instance, bool __state, bool __runOriginal)
        {
            try
            {
                Shows._show show = __instance;
                if (ShouldPost(show, __state, __runOriginal, staticVars.dateTime))
                {
                    Moments.Trigger(() => MomentContext.ShowEnd(show));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        /// <summary>
        /// The call ended a show that was on air, with a cast, a year or more after its first episode.
        /// A cancelled show is final, so each show passes once.
        /// </summary>
        internal static bool ShouldPost(Shows._show show, bool wasOn, bool ran, DateTime now)
        {
            return ran && wasOn && show != null && show.status == Shows._show._status.canceled
                && History.Known(show.LaunchDate) && now >= show.LaunchDate.AddYears(1)
                && show.GetCast().Any(g => g != null);
        }
    }

    /// <summary>
    /// A lasting business contract (an ad or a TV drama), when the player accepts it: the game adds it to
    /// the running contracts. One-off deals never come here.
    /// </summary>
    [HarmonyPatch(typeof(business), nameof(business.AddActiveProposal))]
    [HarmonyPriority(Priority.Last)]
    public class business_AddActiveProposal
    {
        public static void Prefix(business __instance, out int __state)
        {
            __state = __instance?.ActiveProposals?.Count ?? -1;
        }

        public static void Postfix(business __instance, int __state, bool __runOriginal)
        {
            try
            {
                business.active_proposal added = Added(__instance?.ActiveProposals, __state, __runOriginal);
                if (added != null)
                {
                    List<business.active_proposal> running = __instance.ActiveProposals.ToList();
                    Moments.Trigger(() => MomentContext.ContractStart(added, running, business.History));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        /// <summary>The contract the game just added; null when it added none.</summary>
        internal static business.active_proposal Added(List<business.active_proposal> running, int before, bool ran)
        {
            return ran && running != null && before >= 0 && running.Count > before && running[running.Count - 1]?.Girl != null
                ? running[running.Count - 1]
                : null;
        }
    }
}
