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
    /// An audition the player launched and paid for, as its candidates are shown. The copycat story audition
    /// comes through here unpaid; the game's other story auditions open the popup without it.
    /// </summary>
    [HarmonyPatch(typeof(Auditions), nameof(Auditions.GenerateAudition))]
    public class Auditions_GenerateAudition
    {
        public static void Prefix(Auditions.data _data, bool ShouldPay)
        {
            try
            {
                if (ShouldPay && _data != null)
                {
                    AuditionHires.Start(_data.Type);
                }
                else
                {
                    AuditionHires.Clear();
                }
            }
            catch (Exception)
            {
                // No request
            }
        }
    }

    /// <summary>
    /// An idol hired while a player's audition is open: every hire goes through Hire.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.Hire))]
    public class data_girls_Hire
    {
        public static void Postfix(data_girls.girls _girl, bool __runOriginal)
        {
            try
            {
                AuditionHires.Audition pending = AuditionHires.Pending;
                if (__runOriginal && pending != null && _girl != null && data_girls.girl.Contains(_girl) && !pending.Hired.Contains(_girl))
                {
                    pending.Hired.Add(_girl);
                }
            }
            catch (Exception)
            {
                // Not counted
            }
        }
    }

    /// <summary>
    /// A player's audition, once its popup closes: every hire from it is final then.
    /// </summary>
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.Close))]
    [HarmonyPriority(Priority.Last)]
    public class Popup_Audition_Close
    {
        public static void Postfix(bool __runOriginal)
        {
            try
            {
                // Taken whatever happens, so a later close can't post this audition again
                AuditionHires.Audition audition = AuditionHires.Take();
                if (ShouldPost(audition, __runOriginal))
                {
                    List<data_girls.girls> hired = Joined(audition);
                    Moments.Trigger(() => MomentContext.Audition(hired, audition.Scale));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(AuditionHires.Audition audition, bool ran)
        {
            return ran && audition != null
                && (audition.Scale == Auditions.type.local || audition.Scale == Auditions.type.regional || audition.Scale == Auditions.type.nationwide)
                && Joined(audition).Count > 0;
        }

        /// <summary>The idols hired from the audition who are still at the agency.</summary>
        private static List<data_girls.girls> Joined(AuditionHires.Audition audition)
        {
            return audition.Hired.Where(g => g != null && data_girls.girl.Contains(g) && g.status != data_girls._status.graduated).ToList();
        }
    }

    /// <summary>
    /// An idol going on hiatus, from the player's menu, a failed treatment or a dialogue. The game breaks
    /// her contracts and refills her show slots as she leaves, so both are read before.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.SendOnHiatus))]
    [HarmonyPriority(Priority.Last)]
    public class data_girls_girls_SendOnHiatus
    {
        public sealed class Before
        {
            public bool WasOff;
            public HiatusReason Reason;
            public DateTime LastEnd;
            public List<business.active_proposal> Contracts = new();
            public List<Shows._show> Shows = new();
        }

        public static void Prefix(data_girls.girls __instance, out Before __state)
        {
            __state = new Before();
            try
            {
                __state.WasOff = __instance != null && __instance.status != data_girls._status.hiatus;
                __state.Reason = HiatusReasons.From(__instance.status);
                __state.LastEnd = __instance.HiatusEnd;
                __state.Contracts = ContractBreaks.Snapshot();
                __state.Shows = ShowsOf(__instance, global::Shows.shows);
            }
            catch (Exception)
            {
                // Whatever couldn't be read is left out
            }
        }

        public static void Postfix(data_girls.girls __instance, Before __state, bool __runOriginal)
        {
            try
            {
                data_girls.girls girl = __instance;
                if (ShouldPost(girl, __state, __runOriginal))
                {
                    List<business.active_proposal> ended = ContractBreaks.Ended(__state.Contracts).Where(c => c.Girl == girl).ToList();
                    Moments.Trigger(() => MomentContext.HiatusStart(girl, __state.Reason, __state.LastEnd, ended, __state.Shows));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(data_girls.girls girl, Before before, bool ran)
        {
            return ran && girl != null && before != null && before.WasOff && girl.status == data_girls._status.hiatus;
        }

        /// <summary>The shows on air whose cast slots hold her; one still in production isn't public yet.</summary>
        internal static List<Shows._show> ShowsOf(data_girls.girls girl, IEnumerable<Shows._show> shows)
        {
            return (shows ?? Enumerable.Empty<Shows._show>())
                .Where(s => s != null && (s.status == Shows._show._status.released || s.status == Shows._show._status.relaunching
                    || s.status == Shows._show._status.relaunching_working) && s.girls != null && s.girls.Contains(girl))
                .ToList();
        }
    }

    /// <summary>
    /// An idol back from hiatus: on its planned date, or called back early by the player; both read the
    /// same. The reason is the status she had before it, which the game keeps as previous_status.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.FinishHiatus))]
    [HarmonyPriority(Priority.Last)]
    public class data_girls_girls_FinishHiatus
    {
        public sealed class Before
        {
            public bool WasOn;
            public HiatusReason Reason;
        }

        public static void Prefix(data_girls.girls __instance, out Before __state)
        {
            __state = new Before();
            try
            {
                __state.WasOn = __instance != null && __instance.status == data_girls._status.hiatus;
                __state.Reason = HiatusReasons.From(__instance.previous_status);
            }
            catch (Exception)
            {
                // Said to have no reason
            }
        }

        public static void Postfix(data_girls.girls __instance, Before __state, bool __runOriginal)
        {
            try
            {
                data_girls.girls girl = __instance;
                if (ShouldPost(girl, __state, __runOriginal))
                {
                    Moments.Trigger(() => MomentContext.HiatusEnd(girl, __state.Reason));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(data_girls.girls girl, Before before, bool ran)
        {
            return ran && girl != null && before != null && before.WasOn && girl.status != data_girls._status.hiatus;
        }
    }

    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.Graduate))]
    [HarmonyPriority(Priority.Last)]
    public class data_girls_girls_Graduate
    {
        public sealed class Before
        {
            public Members.Rank Rank;
            public List<data_girls.girls> Friends;
            public List<data_girls.girls> BulliedBy;
            public List<data_girls.girls> Bullied;
            public GraduationNotice Notice;
            /// <summary>The day she announced it; unknown when she didn't, or her date was unknown.</summary>
            public DateTime AnnouncedOn;
        }

        /// <summary>
        /// Her place by fans in her group, her clique and her bullying before graduating: the game moves her
        /// fans to other idols, takes her out of her clique and ends the bullying as she leaves. Also whether
        /// she had announced it: the game graduates an announced idol on the date it set 3 months before.
        /// </summary>
        public static void Prefix(data_girls.girls __instance, out Before __state)
        {
            __state = new Before();
            try
            {
                if (__instance.status == data_girls._status.announced_graduation)
                {
                    __state.Notice = GraduationNotice.Announced;
                    __state.AnnouncedOn = History.Known(__instance.Graduation_Date) ? __instance.Graduation_Date.AddMonths(-3) : default;
                }
                else
                {
                    __state.Notice = Date_Popup_OnClick_Fire_Immediately.Firing == __instance ? GraduationNotice.Fired : GraduationNotice.Sudden;
                }
            }
            catch (Exception)
            {
                // Said to be sudden
            }
            try
            {
                __state.Rank = Members.RankOf(__instance);
            }
            catch (Exception)
            {
                // Her rank is left out
            }
            try
            {
                __state.Friends = Members.Friends(__instance);
            }
            catch (Exception)
            {
                // Her friends are left out
                __state.Friends = new List<data_girls.girls>();
            }
            try
            {
                __state.BulliedBy = Members.BulliedBy(__instance);
                __state.Bullied = Members.Bullied(__instance);
            }
            catch (Exception)
            {
                // The bullying is left out
                __state.BulliedBy = new List<data_girls.girls>();
                __state.Bullied = new List<data_girls.girls>();
            }
        }

        /// <summary>
        /// A graduation without a goodbye scene posts at once. A regular one waits for its goodbye scene to
        /// end, where the player can ask for a successor or offer her a staff job; the game is paused meanwhile.
        /// </summary>
        public static void Postfix(data_girls.girls __instance, bool dialogue, Before __state, bool __runOriginal)
        {
            if (!__runOriginal || __instance == null || __instance.status != data_girls._status.graduated
                || !Moments.FirstGraduationToday(staticVars.dateTime))
            {
                return;
            }
            data_dialogues._dialogue scene = dialogue ? GoodbyeScene() : null;
            if (scene == null)
            {
                GraduationScene.Send(() => MomentContext.Graduation(__instance, __state.Rank, __state.Friends,
                    __state.BulliedBy, __state.Bullied, notice: __state.Notice, announcedOn: __state.AnnouncedOn));
                return;
            }
            // One still waiting is sent first, so none is lost
            if (GraduationScene.Pending != null)
            {
                GraduationScene.Post(GraduationScene.Take());
            }
            GraduationScene.Start(__instance, __state, scene);
        }

        /// <summary>
        /// The dialogue the game just started for her goodbye; null when it couldn't show one.
        /// </summary>
        private static data_dialogues._dialogue GoodbyeScene()
        {
            try
            {
                // Graduation_Goodbye hands the scene to the controller (Set), which keeps it until the next one
                return GraduationScene.CurrentDialogue();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The player's "Fire now" in the date popup: the idol it graduates at once was dismissed by the agency.
    /// The click graduates her itself, so the idol is noted only while it runs.
    /// </summary>
    [HarmonyPatch]
    public class Date_Popup_OnClick_Fire_Immediately
    {
        internal static data_girls.girls Firing;

        public static MethodBase TargetMethod() => AccessTools.Method(typeof(Date_Popup), "OnClick_Fire_Immediately");

        public static bool Prepare()
        {
            if (TargetMethod() != null)
            {
                return true;
            }
            Log.Warn("Couldn't find the date popup's Fire now; fired idols' graduations are posted as sudden ones.");
            return false;
        }

        public static void Prefix(Date_Popup __instance)
        {
            try
            {
                Firing = __instance.Girl;
            }
            catch (Exception)
            {
                Firing = null;
            }
        }

        public static Exception Finalizer(Exception __exception)
        {
            Firing = null;
            return __exception;
        }
    }

    /// <summary>
    /// An idol's graduation announcement, as the game confirms it: after her talk with the producer when he
    /// didn't keep her, after "Let her graduate" in the date popup, or from a story. Asking her to stay
    /// skips the confirmation.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Announce_Confirm))]
    [HarmonyPriority(Priority.Last)]
    public class data_girls_girls_Graduation_Announce_Confirm
    {
        public static void Prefix(data_girls.girls __instance, out data_girls._status __state)
        {
            __state = data_girls._status.graduated;
            try
            {
                __state = __instance.status;
            }
            catch (Exception)
            {
                // No request
            }
        }

        public static void Postfix(data_girls.girls __instance, data_girls._status __state, bool __runOriginal)
        {
            try
            {
                if (ShouldPost(__state, __instance, __runOriginal))
                {
                    Moments.Trigger(() => MomentContext.GraduationAnnouncement(__instance));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        /// <summary>She announced it just now: she hadn't before, and she has after.</summary>
        internal static bool ShouldPost(data_girls._status before, data_girls.girls girl, bool ran)
        {
            return ran && girl != null && before != data_girls._status.announced_graduation && before != data_girls._status.graduated
                && girl.status == data_girls._status.announced_graduation;
        }
    }

    /// <summary>
    /// The sister or successor a graduate brings in from her goodbye scene: the idol the game adds.
    /// </summary>
    [HarmonyPatch(typeof(Date_Graduation), nameof(Date_Graduation.Find_Successor))]
    public class Date_Graduation_Find_Successor
    {
        public static void Prefix(out int __state)
        {
            __state = data_girls.girl.Count;
        }

        public static void Postfix(data_girls.girls Girl, bool Sister, int __state, bool __runOriginal)
        {
            try
            {
                GraduationScene.Waiting waiting = GraduationScene.For(Girl);
                data_girls.girls added = __runOriginal && __state < data_girls.girl.Count ? data_girls.girl[data_girls.girl.Count - 1] : null;
                if (waiting != null && added != null && added != Girl)
                {
                    waiting.Successor = added;
                    waiting.Sister = Sister;
                }
            }
            catch (Exception)
            {
                // Posted without her
            }
        }
    }

    /// <summary>
    /// A graduate staying at the agency as staff, from her goodbye scene: the staffer the game adds.
    /// </summary>
    [HarmonyPatch(typeof(Date_Graduation), nameof(Date_Graduation.Hire_As_Staffer))]
    public class Date_Graduation_Hire_As_Staffer
    {
        public static void Prefix(out int __state)
        {
            __state = staff.Staff.Count;
        }

        public static void Postfix(data_girls.girls Girl, int __state, bool __runOriginal)
        {
            try
            {
                GraduationScene.Waiting waiting = GraduationScene.For(Girl);
                if (waiting != null && __runOriginal && __state < staff.Staff.Count)
                {
                    waiting.Staffer = staff.Staff[staff.Staff.Count - 1];
                }
            }
            catch (Exception)
            {
                // Posted without it
            }
        }
    }

    /// <summary>
    /// The end of a dialogue: a waiting graduation posts when its goodbye scene ends.
    /// </summary>
    [HarmonyPatch(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Hide))]
    [HarmonyPriority(Priority.Last)]
    public class ActiveDialogueController_Hide
    {
        public static void Postfix(ActiveDialogueController __instance)
        {
            try
            {
                GraduationScene.Waiting waiting = GraduationScene.Pending;
                // A reference check: Unity's == on its objects asks the engine
                if (waiting != null && !ReferenceEquals(__instance, null) && __instance.dialogue == waiting.Dialogue)
                {
                    GraduationScene.Post(GraduationScene.Take());
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        /// <summary>
        /// For a goodbye scene that ended some other way (another mod, a scene that couldn't load): the first
        /// time the game clock moves with no dialogue showing, the waiting graduation posts.
        /// </summary>
        public static void OnTimeTick()
        {
            try
            {
                if (GraduationScene.Pending != null && !ActiveDialogueController.ShowingDialogue)
                {
                    GraduationScene.Post(GraduationScene.Take());
                }
            }
            catch (Exception)
            {
                // No request
            }
        }
    }
}
