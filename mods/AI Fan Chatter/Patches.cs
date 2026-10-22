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
    /// Keeps the API key out of the saves. ModMenus' Apply saves every setting through variables.Set; the
    /// key goes to its file instead and the save gets "". Runs first, so no other patch on Set sees the
    /// key. Any change to the mod's settings lets requests run again and tests the connection, and so does
    /// pressing Apply with nothing changed while requests are stopped.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.Set))]
    [HarmonyPriority(Priority.First)]
    public class variables_Set
    {
        public static void Prefix(string name, ref string value)
        {
            if (name == null || !name.StartsWith(Settings.Prefix, StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                bool changed;
                if (name == Settings.ApiKeyVar)
                {
                    // The provider row comes before the key row, so Apply has already saved the provider
                    changed = KeyStore.Apply(Settings.Provider, value);
                    value = "";
                }
                else
                {
                    changed = name != FeedStore.OlderPostsVar && name != FeedQueue.QueueVar && variables.Get(name) != value;
                }
                if (changed)
                {
                    Dispatcher.Instance.OnSettingsChanged();
                }
                else if (name == Settings.ApiKeyVar)
                {
                    // Apply saves every row, changed or not, and only Apply saves the key row: pressing it
                    // with nothing changed lifts a stop, so the player can try again after fixing the cause
                    Dispatcher.Instance.OnApplied();
                }
            }
            catch (Exception e)
            {
                if (name == Settings.ApiKeyVar)
                {
                    value = "";
                }
                Log.Warn("Couldn't apply setting " + name + ": " + e.Message);
            }
        }
    }

    /// <summary>
    /// A backstop for a key that reached the variables some other way than variables.Set: it moves to the
    /// key file before the save is written.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.SaveFunction))]
    public class variables_SaveFunction
    {
        public static void Postfix()
        {
            try
            {
                KeyStore.Scrub(variables.variable, Settings.Provider);
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't check the saved settings for a key: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Starts a request for a moment, unless the limits stop it first; then the moment isn't even read.
    /// </summary>
    internal static class Moments
    {
        public static void Trigger(Func<Moment> moment)
        {
            try
            {
                Dispatcher dispatcher = Dispatcher.Instance;
                if (dispatcher.Blocked() == null)
                {
                    dispatcher.Request(moment());
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't start a request: " + e);
            }
        }

        private static DateTime lastGraduation = DateTime.MinValue;

        /// <summary>
        /// One graduation a day: bankruptcy and story scenes graduate several idols at once.
        /// </summary>
        public static bool FirstGraduationToday(DateTime now)
        {
            if (now.Date == lastGraduation)
            {
                return false;
            }
            lastGraduation = now.Date;
            return true;
        }

        internal static void ResetGraduations() => lastGraduation = DateTime.MinValue;
    }

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

    /// <summary>
    /// A part of an award's acceptance speech, as the show builds it into the line it's about to show.
    /// </summary>
    [HarmonyPatch(typeof(vn_actions), "DoCustom")]
    public class vn_actions_DoCustom
    {
        public static void Postfix(string formula, ActiveDialogueController ___ADC, bool __runOriginal)
        {
            try
            {
                if (__runOriginal && AwardsShow.IsSpeechPart(formula) && !ReferenceEquals(___ADC, null))
                {
                    AwardsShow.Heard(Award_Speeches_Popup.SelectedSpeech, ___ADC.activeNode?.val);
                }
            }
            catch (Exception)
            {
                // Posted without the speech's words
            }
        }
    }

    /// <summary>
    /// The yearly awards show, as its dialogue ends: the game clears this year's nominations there, so the
    /// prefix copies them first.
    /// </summary>
    [HarmonyPatch(typeof(Awards), nameof(Awards.Reset_After_Awards))]
    [HarmonyPriority(Priority.Last)]
    public class Awards_Reset_After_Awards
    {
        // The date of the last show posted: the show ends once a year
        private static DateTime posted = DateTime.MinValue;

        internal static void Reset() => posted = DateTime.MinValue;

        public static void Prefix(out AwardsShow __state)
        {
            __state = null;
            try
            {
                __state = AwardsShow.Read();
            }
            catch (Exception)
            {
                // No request
            }
        }

        public static void Postfix(AwardsShow __state, bool __runOriginal)
        {
            try
            {
                if (__runOriginal && __state == null)
                {
                    Log.Warn("The awards show ended without nominations to read; no posts for it.");
                }
                if (ShouldPost(__state, __runOriginal, staticVars.dateTime))
                {
                    posted = staticVars.dateTime.Date;
                    Moments.Trigger(() => MomentContext.Awards(__state));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(AwardsShow show, bool ran, DateTime now)
        {
            return ran && show != null && show.Nominations.Count > 0 && now.Date != posted;
        }
    }

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
    /// The outcomes of the dating scandal answered in Event_Templates_ConcludeEvent, as the game picks them.
    /// </summary>
    internal static class DatingScandal
    {
        public const string TemplateId = "dating_scandal";

        /// <summary>The contracts running before the answer, to find the ones it ended.</summary>
        public static List<business.active_proposal> ContractsBefore = new();

        /// <summary>
        /// The endings that keep the scandal from the public: the photos are bought back, the police stop the
        /// blackmailer, the blackmail was a bluff, or the editor drops the story. Every other ending is public.
        /// </summary>
        private static readonly HashSet<Event_Templates._dating_reactions._type> Hidden = new()
        {
            Event_Templates._dating_reactions._type.Pay_blackmail,
            Event_Templates._dating_reactions._type.Pay_to_publication,
            Event_Templates._dating_reactions._type.Report_to_police__success,
            Event_Templates._dating_reactions._type.Report_to_police__success_2,
            Event_Templates._dating_reactions._type.Do_nothing__blackmail__no_damage,
            Event_Templates._dating_reactions._type.Deny_identity__success__unpublished,
        };

        /// <summary>Set while the game resolves a template's answer.</summary>
        public static bool Capturing;

        public static readonly List<Event_Templates._dating_reactions._type> Reactions = new();

        public static bool IsHidden(Event_Templates._dating_reactions._type reaction) => Hidden.Contains(reaction);
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

    /// <summary>
    /// Moves keys saved before the shared key folder into it, from the main menu on. Releases queued
    /// posts as the game clock runs, from the same tick the game's own systems use. staticVars.SetTime
    /// isn't patched: it's a one-line setter the runtime may inline.
    /// </summary>
    [HarmonyPatch(typeof(mainScript), nameof(mainScript.Start))]
    public class mainScript_Start
    {
        public static void Postfix(mainScript __instance)
        {
            KeyStore.MoveLegacyKeys();
            try
            {
                if (!mainScript.IsMainMenu())
                {
                    __instance.onTimeTick += FeedRelease.OnTimeTick;
                    __instance.onTimeTick += ActiveDialogueController_Hide.OnTimeTick;
                    __instance.onTimeTick += SEvent_Concerts_concert_Finish.OnTimeTick;
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't follow the game clock; queued posts won't appear: " + e.Message);
            }
        }
    }

    /// <summary>
    /// A new game or scene starts with an empty feed, so the queue empties too.
    /// </summary>
    [HarmonyPatch(typeof(SNS_Manager), nameof(SNS_Manager.Awake))]
    public class SNS_Manager_Awake
    {
        public static void Postfix()
        {
            FeedQueue.Clear();
            FeedRelease.NewSession();
            Moments.ResetGraduations();
        }
    }

    /// <summary>
    /// Raises the feed's limit from 20 posts to 100 by changing only the 20 in "if (Messages.Count == 20)".
    /// When that check isn't there exactly once, another mod has already changed the method, and it's
    /// left as it is.
    /// </summary>
    [HarmonyPatch(typeof(SNS_Manager), nameof(SNS_Manager.AddMessage))]
    public class SNS_Manager_AddMessage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return RaiseCap(instructions.ToList(), out _);
        }

        internal static List<CodeInstruction> RaiseCap(List<CodeInstruction> code, out bool changed)
        {
            changed = false;
            FieldInfo messages = AccessTools.Field(typeof(SNS_Manager), nameof(SNS_Manager.Messages));
            MethodInfo count = AccessTools.PropertyGetter(typeof(List<SNS_Manager._message>), nameof(List<SNS_Manager._message>.Count));
            List<int> matches = new();
            for (int i = 0; i + 2 < code.Count; i++)
            {
                if (code[i].LoadsField(messages) && code[i + 1].Calls(count) && code[i + 2].LoadsConstant(FeedStore.VanillaCap))
                {
                    matches.Add(i + 2);
                }
            }
            if (matches.Count != 1)
            {
                Log.Warn("The feed's limit check isn't the game's own (another mod changed it), so the feed keeps that limit.");
                return code;
            }
            CodeInstruction limit = code[matches[0]];
            code[matches[0]] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FeedStore), nameof(FeedStore.CapValue)))
            {
                labels = limit.labels,
                blocks = limit.blocks
            };
            changed = true;
            return code;
        }
    }

    /// <summary>
    /// Saves the newest 20 posts where the game saves them, the older ones and the queue in the mod's variables.
    /// </summary>
    [HarmonyPatch(typeof(SNS_Manager), nameof(SNS_Manager.SaveFunction))]
    [HarmonyPriority(Priority.Last)]
    public class SNS_Manager_SaveFunction
    {
        public static void Postfix()
        {
            try
            {
                SaveManager.SavedData data = Camera.main.GetComponent<mainScript>().GetSavedData();
                data.SNS_Manager__Messages = SaveFeed(data.SNS_Manager__Messages, variables.variable);
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't save the older posts and the queue: " + e.Message);
            }
        }

        /// <summary>
        /// The list for the game's own field. It splits what's in the field now, so another mod's change to it is kept.
        /// Variables go in the game's live list, which is the one the save stores, whichever event runs first.
        /// </summary>
        internal static List<SNS_Manager._message> SaveFeed(List<SNS_Manager._message> saved, List<variables._variable> vars)
        {
            List<SNS_Manager._message> kept = FeedStore.SplitForSave(saved, out List<SNS_Manager._message> older);
            FeedStore.SetVariable(vars, FeedStore.OlderPostsVar, older == null ? null : FeedStore.Serialize(older));
            FeedStore.SetVariable(vars, FeedQueue.QueueVar, FeedQueue.Items.Count == 0 ? null : FeedQueue.Serialize());
            return kept;
        }
    }

    /// <summary>
    /// Puts the older posts back in front of the loaded feed and reloads the queue.
    /// </summary>
    [HarmonyPatch(typeof(SNS_Manager), nameof(SNS_Manager.LoadFunction))]
    public class SNS_Manager_LoadFunction
    {
        public static void Postfix(SNS_Manager __instance)
        {
            try
            {
                FeedRelease.NewSession();
                // Read from the save itself: the variables may not be loaded yet
                SaveManager.SavedData data = Camera.main.GetComponent<mainScript>().GetSavedData();
                if (LoadFeed(SNS_Manager.Messages, data.variables__variables))
                {
                    __instance.RenderMessages();
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't load the older posts and the queue: " + e.Message);
            }
        }

        /// <summary>
        /// Returns whether the feed changed.
        /// </summary>
        internal static bool LoadFeed(List<SNS_Manager._message> feed, List<variables._variable> vars)
        {
            FeedQueue.Load(FeedStore.GetVariable(vars, FeedQueue.QueueVar));
            if (feed == null)
            {
                return false;
            }
            int before = feed.Count;
            int added = FeedStore.MergeOnLoad(feed, FeedStore.Deserialize(FeedStore.GetVariable(vars, FeedStore.OlderPostsVar)));
            FeedStore.TrimToCap(feed);
            return added > 0 || feed.Count != before;
        }
    }

    /// <summary>
    /// Keeps the notification of a later post in a batch from showing: only its Type changes, to the one
    /// the game never shows. The game shows that type only when the player has turned every notification
    /// off, and then it's left alone.
    /// </summary>
    [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.AddNotification),
        new[] { typeof(string), typeof(Color32), typeof(NotificationManager._notification._type) })]
    public class NotificationManager_AddNotification
    {
        public static void Prefix(ref NotificationManager._notification._type Type)
        {
            if (!FeedRelease.Quiet)
            {
                return;
            }
            try
            {
                Type = QuietType(Type, staticVars.Settings?.notification_enabled);
            }
            catch (Exception)
            {
                // The notification shows
            }
        }

        internal static NotificationManager._notification._type QuietType(NotificationManager._notification._type type, List<NotificationManager._notification._type> enabled)
        {
            return enabled != null && enabled.Contains(NotificationManager._notification._type.none) ? type : NotificationManager._notification._type.none;
        }
    }
}
