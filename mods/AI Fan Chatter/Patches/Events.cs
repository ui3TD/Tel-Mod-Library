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
    /// Random events, scandals included, when the answer had a public outcome (EventOutcome). Events that
    /// already post on the feed are left to the game.
    /// </summary>
    [HarmonyPatch(typeof(Event_Manager), nameof(Event_Manager.ConcludeEvent))]
    [HarmonyPriority(Priority.Last)]
    public class Event_Manager_ConcludeEvent
    {
        public sealed class Before
        {
            /// <summary>The event being answered; the game may mark it complete during the method.</summary>
            public Event_Manager._activeEvent Active;
            public List<double> PublicState;
            /// <summary>The contracts running before the answer, to find the ones it ended.</summary>
            public List<business.active_proposal> Contracts;
        }

        public static void Prefix(Event_Manager __instance, out Before __state)
        {
            __state = null;
            try
            {
                __state = new Before { Active = __instance.GetActiveEvent(), PublicState = EventOutcome.PublicState(), Contracts = ContractBreaks.Snapshot() };
            }
            catch (Exception)
            {
                // No event, no request
            }
        }

        public static void Postfix(Event_Manager __instance, Event_Manager._randomEvent._reply reply, Before __state, bool __runOriginal)
        {
            try
            {
                // Read after the other mods' postfixes, so outcomes they apply in code count
                if (__state != null && ShouldPost(reply, __state.Active, __runOriginal, EventOutcome.Changed(__state.PublicState, EventOutcome.PublicState())))
                {
                    List<business.active_proposal> ended = ContractBreaks.Ended(__state.Contracts);
                    Moments.Trigger(() => MomentContext.Event(__state.Active, reply, __instance?.activeEvents, ended));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(Event_Manager._randomEvent._reply reply, Event_Manager._activeEvent active, bool ran, bool publicOutcome)
        {
            return ran && publicOutcome && reply != null && active != null && (reply.SNS == null || reply.SNS.Count == 0);
        }
    }

    /// <summary>
    /// Paid policy changes: the player spends scandal points on them, the agency acting on its image. Free
    /// changes (free policies, and every policy in the first month) and the game's own selections (loading a
    /// save, story scripts) get no posts.
    /// </summary>
    [HarmonyPatch(typeof(policies.value), nameof(policies.value._Select))]
    [HarmonyPriority(Priority.Last)]
    public class policies_value_Select
    {
        public sealed class Before
        {
            public bool Paid;
            public policies.value Previous;
            public List<PolicyChange.Cleared> Cleared = new();
        }

        public static void Prefix(policies.value __instance, bool free, out Before __state)
        {
            __state = null;
            try
            {
                bool paid = !free && __instance.Cost > 0;
                __state = new Before
                {
                    Paid = paid,
                    Previous = policies.GetSelectedPolicyValue(__instance.Type),
                    // The popup has closed by now, but still holds what was chosen
                    Cleared = paid ? PolicyChange.ClearedPoints() : new List<PolicyChange.Cleared>(),
                };
            }
            catch (Exception)
            {
                // No request
            }
        }

        public static void Postfix(policies.value __instance, Before __state, bool __runOriginal)
        {
            if (ShouldPost(__state, __instance, __runOriginal))
            {
                Moments.Trigger(() => MomentContext.PolicyChange(__state.Previous, __instance, __state.Cleared, EventLog()));
            }
        }

        internal static bool ShouldPost(Before before, policies.value value, bool ran)
        {
            return ran && before != null && before.Paid && value != null && value.Value != policies._value.NONE
                && value.Selected && value != before.Previous;
        }

        private static IEnumerable<Event_Manager._activeEvent> EventLog()
        {
            try
            {
                return Camera.main.GetComponent<mainScript>().Data.GetComponent<Event_Manager>().activeEvents;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The player starts firing a staffer: the scandal points popup, or first a talk when it costs more than 2.
    /// Story scripts fire staff without this, and get no posts.
    /// </summary>
    [HarmonyPatch(typeof(Staff_Fire), nameof(Staff_Fire.Fire))]
    public class Staff_Fire_Fire
    {
        public static void Postfix(staff._staff _Staff)
        {
            try
            {
                StaffFiring.Start(_Staff, talk: _Staff != null && _Staff.PointsToFire() > 2);
            }
            catch (Exception)
            {
                StaffFiring.Clear();
            }
        }
    }

    /// <summary>
    /// The talk ends: whether the player accepted the staffer's deal to announce budget problems.
    /// </summary>
    [HarmonyPatch(typeof(Staff_Fire), nameof(Staff_Fire.DoComplete))]
    public class Staff_Fire_DoComplete
    {
        public static void Prefix()
        {
            if (StaffFiring.Talk)
            {
                StaffFiring.Budget = Staff_Fire.Accepted && !Staff_Fire.IsSeverance;
            }
        }
    }

    /// <summary>
    /// A cancelled scandal points popup ends the firing, so a later story firing isn't taken for it.
    /// </summary>
    [HarmonyPatch(typeof(ScandalPoints_Popup), nameof(ScandalPoints_Popup.OnCancel))]
    public class ScandalPoints_Popup_OnCancel
    {
        public static void Postfix() => StaffFiring.Clear();
    }

    /// <summary>
    /// A staffer fired through the game's firing flow, with scandals blamed on them or budget problems
    /// announced: the agency acting on its image.
    /// </summary>
    [HarmonyPatch(typeof(staff._staff), nameof(staff._staff.Fire))]
    [HarmonyPriority(Priority.Last)]
    public class staff_staff_Fire
    {
        public sealed class Before
        {
            public bool Employed;
            public bool Budget;
            public List<PolicyChange.Cleared> Cleared = new();
        }

        public static void Prefix(staff._staff __instance, out Before __state)
        {
            __state = null;
            try
            {
                if (__instance != null && __instance == StaffFiring.Pending)
                {
                    // The popup has closed by now, but still holds what was chosen
                    __state = new Before { Employed = staff.Staff.Contains(__instance), Budget = StaffFiring.Budget, Cleared = PolicyChange.ClearedPoints() };
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        public static void Postfix(staff._staff __instance, Before __state, bool __runOriginal)
        {
            try
            {
                if (ShouldPost(__state, __instance, __runOriginal))
                {
                    StaffFiring.Clear();
                    Moments.Trigger(() => MomentContext.StaffFiring(__instance, __state.Budget, __state.Cleared, EventLog(), Contracts()));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        private static IEnumerable<business.active_proposal> Contracts()
        {
            try
            {
                return ContractBreaks.Running();
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static bool ShouldPost(Before before, staff._staff staffer, bool ran)
        {
            return ran && before != null && before.Employed && staffer != null && !staff.Staff.Contains(staffer);
        }

        private static IEnumerable<Event_Manager._activeEvent> EventLog()
        {
            try
            {
                return Camera.main.GetComponent<mainScript>().Data.GetComponent<Event_Manager>().activeEvents;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Dating scandals: the game's dating scandal template, answered by the player. They end here, not in
    /// Event_Manager.ConcludeEvent, and the game posts nothing for them. Fans react only when the outcome
    /// reached the public.
    /// </summary>
    [HarmonyPatch(typeof(Event_Templates), nameof(Event_Templates.ConcludeEvent))]
    [HarmonyPriority(Priority.Last)]
    public class Event_Templates_ConcludeEvent
    {
        public static void Prefix(out Event_Templates._active_template __state)
        {
            __state = Event_Templates.Active_Template;
            DatingScandal.Reactions.Clear();
            DatingScandal.ContractsBefore = ContractBreaks.Snapshot();
            DatingScandal.Capturing = true;
        }

        public static void Postfix(Event_Templates._template._reply Reply, Event_Templates._active_template __state, bool __runOriginal)
        {
            DatingScandal.Capturing = false;
            List<Event_Templates._dating_reactions._type> reactions = DatingScandal.Reactions.ToList();
            if (ShouldPost(__state, Reply, reactions, __runOriginal))
            {
                List<business.active_proposal> ended = ContractBreaks.Ended(DatingScandal.ContractsBefore);
                Moments.Trigger(() => MomentContext.DatingScandal(__state, Reply, reactions, ended));
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            DatingScandal.Capturing = false;
            return __exception;
        }

        /// <summary>
        /// A dating scandal about an idol, answered with an outcome the game picked, none of which kept it
        /// hidden. No outcome means another mod resolved it its own way, and whether it went public is unknown.
        /// </summary>
        internal static bool ShouldPost(Event_Templates._active_template active, Event_Templates._template._reply reply,
            IList<Event_Templates._dating_reactions._type> reactions, bool ran)
        {
            return ran && reply != null && active?.Template?.ID == DatingScandal.TemplateId
                && active.Actors.Count > 0 && active.Actors[0]?.girl != null
                && reactions.Count > 0 && !reactions.Any(DatingScandal.IsHidden);
        }
    }

    /// <summary>
    /// Notes each outcome the game picks while a dating scandal is resolved. Reads only.
    /// </summary>
    [HarmonyPatch(typeof(Event_Templates), nameof(Event_Templates.GetDatingReaction))]
    public class Event_Templates_GetDatingReaction
    {
        public static void Postfix(Event_Templates._dating_reactions._type Reaction)
        {
            if (DatingScandal.Capturing)
            {
                DatingScandal.Reactions.Add(Reaction);
            }
        }
    }
}
