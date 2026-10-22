using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AIFanChatter
{
    /// <summary>
    /// A paid policy change: the scandal points the player spent on it, as the game's scandal points popup
    /// holds them until it's next opened.
    /// </summary>
    public static class PolicyChange
    {
        /// <summary>Scandal points the player spent: an idol's own, or the group's.</summary>
        public sealed class Cleared
        {
            public data_girls.girls Girl;
            public bool Group;
            public int Points;
        }

        /// <summary>The popup's private list of the points chosen (ScandalPoints_Popup.Points).</summary>
        internal static readonly FieldInfo PointsField = AccessTools.Field(typeof(ScandalPoints_Popup), "Points");

        /// <summary>
        /// The points the player chose to spend in the popup, once each idol or the group; empty when the
        /// list can't be read.
        /// </summary>
        public static List<Cleared> ClearedPoints()
        {
            List<Cleared> cleared = new();
            try
            {
                if (PointsField?.GetValue(null) is not IEnumerable points)
                {
                    return cleared;
                }
                foreach (ScandalPoints_Popup._points point in points.OfType<ScandalPoints_Popup._points>())
                {
                    if (point.ToRemove > 0 && (point.Group || point.Girl != null))
                    {
                        cleared.Add(new Cleared { Girl = point.Group ? null : point.Girl, Group = point.Group, Points = point.ToRemove });
                    }
                }
            }
            catch (Exception)
            {
                // No reason line
            }
            return cleared;
        }
    }

    /// <summary>
    /// The staffer the player is firing through the game's firing flow (Staff_Fire), until the firing is
    /// done or the scandal points popup is cancelled. The mod's own notes; the game's state is only read.
    /// </summary>
    internal static class StaffFiring
    {
        public static staff._staff Pending;
        /// <summary>Whether the firing went through the talk with the staffer (more than 2 points).</summary>
        public static bool Talk;
        /// <summary>Whether the player accepted the staffer's deal to announce budget problems.</summary>
        public static bool Budget;

        public static void Start(staff._staff staffer, bool talk)
        {
            Pending = staffer;
            Talk = talk;
            Budget = false;
        }

        public static void Clear() => Start(null, false);
    }

    /// <summary>
    /// A concert whose election starts as its final screen closes, waiting for the results, so both go out as
    /// one moment. The mod's own notes; the game's state is only read.
    /// </summary>
    internal static class ConcertElection
    {
        public static SEvent_Concerts._concert Pending;
        public static DateTime Since;

        public static void Wait(SEvent_Concerts._concert concert, DateTime now)
        {
            Pending = concert;
            Since = now;
        }

        public static void Clear() => Pending = null;

        /// <summary>The waiting concert when it's this election's; null otherwise, and then it keeps waiting.</summary>
        public static SEvent_Concerts._concert TakeFor(SEvent_SSK._SSK election)
        {
            if (Pending == null || election == null || election.Concert != Pending)
            {
                return null;
            }
            SEvent_Concerts._concert concert = Pending;
            Pending = null;
            return concert;
        }

        /// <summary>
        /// For an election that never finished: the waiting concert, once a full game day has passed. The clock
        /// runs for a moment between the concert's screen and the results, so a shorter wait isn't enough.
        /// </summary>
        public static SEvent_Concerts._concert TakeOverdue(DateTime now)
        {
            if (Pending == null || now < Since.AddDays(1))
            {
                return null;
            }
            SEvent_Concerts._concert concert = Pending;
            Pending = null;
            return concert;
        }
    }

    /// <summary>
    /// A regular graduation waiting for its goodbye scene to end: the player can still ask her for a
    /// successor or offer her a staff job there. The mod's own notes; the game's state is only read.
    /// </summary>
    internal static class GraduationScene
    {
        public sealed class Waiting
        {
            public data_girls.girls Girl;
            public data_girls_girls_Graduate.Before Before;
            /// <summary>The goodbye scene, as the dialogue controller holds it.</summary>
            public data_dialogues._dialogue Dialogue;
            public data_girls.girls Successor;
            public bool Sister;
            public staff._staff Staffer;
        }

        public static Waiting Pending;

        /// <summary>Sends a graduation's request; tests replace it to see what's sent.</summary>
        internal static Action<Func<Moment>> Send = Moments.Trigger;

        /// <summary>The dialogue the game has open; tests replace it, as they have no dialogue controller.</summary>
        internal static Func<data_dialogues._dialogue> CurrentDialogue = () =>
            Camera.main.GetComponent<mainScript>().Data.GetComponent<ActiveDialogueController>().dialogue;

        public static void Start(data_girls.girls girl, data_girls_girls_Graduate.Before before, data_dialogues._dialogue dialogue) =>
            Pending = new Waiting { Girl = girl, Before = before, Dialogue = dialogue };

        public static void Clear() => Pending = null;

        /// <summary>
        /// The waiting graduation, now posted; null when there was none.
        /// </summary>
        public static Waiting Take()
        {
            Waiting waiting = Pending;
            Pending = null;
            return waiting;
        }

        /// <summary>
        /// The graduation waiting for this idol's goodbye scene; null for another idol or none.
        /// </summary>
        public static Waiting For(data_girls.girls girl) => Pending != null && girl != null && Pending.Girl == girl ? Pending : null;

        /// <summary>
        /// Sends the waiting graduation's request, with what the scene added.
        /// </summary>
        public static void Post(Waiting waiting)
        {
            if (waiting == null)
            {
                return;
            }
            data_girls_girls_Graduate.Before before = waiting.Before ?? new data_girls_girls_Graduate.Before();
            Send(() => MomentContext.Graduation(waiting.Girl, before.Rank, before.Friends, before.BulliedBy, before.Bullied,
                waiting.Successor, waiting.Sister, waiting.Staffer, before.Notice, before.AnnouncedOn));
        }
    }
}
