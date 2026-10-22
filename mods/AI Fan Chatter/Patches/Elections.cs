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
    /// An election, when its last place (the winner) has been revealed and its results are final.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_SSK._SSK), nameof(SEvent_SSK._SSK.Finish))]
    [HarmonyPriority(Priority.Last)]
    public class SEvent_SSK_SSK_Finish
    {
        // The last election posted, as for tours
        private static SEvent_SSK._SSK posted;

        internal static void Reset() => posted = null;

        public static void Postfix(SEvent_SSK._SSK __instance, bool __runOriginal)
        {
            try
            {
                if (ShouldPost(__instance, __runOriginal))
                {
                    posted = __instance;
                    SEvent_Concerts._concert concert = ConcertElection.TakeFor(__instance);
                    if (concert != null)
                    {
                        Moments.Trigger(() => MomentContext.ConcertElection(concert, __instance));
                    }
                    else
                    {
                        Moments.Trigger(() => MomentContext.Election(__instance));
                    }
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(SEvent_SSK._SSK election, bool ran)
        {
            return ran && election != null && election.Status == SEvent_Tour.tour._status.finished && election != posted;
        }
    }

    /// <summary>
    /// An election cancelled after its voting single came out: by the player, or with its results concert.
    /// The game unlinks the single and the concert and drops the election, so all of it is read before. Its
    /// fans' opinions fall inside the call, before the postfix builds the prompt.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_SSK), nameof(SEvent_SSK.CancelSSK))]
    [HarmonyPriority(Priority.Last)]
    public class SEvent_SSK_CancelSSK
    {
        public sealed class Before
        {
            public SEvent_SSK._SSK Election;
            public string Title = "";
            public singles._single Single;
            public bool Released;
            public SEvent_Concerts._concert Concert;
            /// <summary>Its concert is being cancelled too: the game's CancelConcert unlinks the election first.</summary>
            public bool ConcertCancelled;
            public SEvent_SSK._broadcast Broadcast;
        }

        public static void Prefix(SEvent_SSK __instance, out Before __state)
        {
            __state = new Before();
            try
            {
                SEvent_SSK._SSK election = __instance?.SSK;
                if (election == null)
                {
                    return;
                }
                __state.Election = election;
                __state.Single = election.Single;
                __state.Released = election.Single != null && election.Single.status == singles._single._status.released;
                __state.Concert = election.Concert;
                __state.ConcertCancelled = election.Concert != null && election.Concert.SSK == null;
                __state.Broadcast = election.Broadcast;
                __state.Title = Moment.Clean(election.GetTitle());
            }
            catch (Exception)
            {
                // Whatever couldn't be read is left out
            }
        }

        public static void Postfix(Before __state, bool __runOriginal)
        {
            try
            {
                if (ShouldPost(__state, __runOriginal, SEvent_SSK.Elections))
                {
                    Before cancelled = __state;
                    Moments.Trigger(() => MomentContext.ElectionCancelled(cancelled));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        /// <summary>
        /// The call dropped an election whose voting single fans could buy; before that, fans never heard of it.
        /// </summary>
        internal static bool ShouldPost(Before before, bool ran, IEnumerable<SEvent_SSK._SSK> elections)
        {
            return ran && before?.Election != null && before.Released && before.Single != null
                && !(elections ?? Enumerable.Empty<SEvent_SSK._SSK>()).Contains(before.Election);
        }
    }
}
