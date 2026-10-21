using AICore;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static AIFanChatter.Tests.TestEnv;
using NotificationType = NotificationManager._notification._type;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// What happens alongside other mods that patch the same methods or change the same values. Each case
    /// stands in for an unknown mod; none depends on a mod's name.
    /// </summary>
    public class CompatibilityTests
    {
        private readonly FakeTransport transport = new();

        public CompatibilityTests()
        {
            Reset();
            Configure();
            Dispatcher.Instance = new Dispatcher(new FakeHost(), transport, new Random(1));
        }

        private static List<CodeInstruction> AddMessageIL() =>
            PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(SNS_Manager), nameof(SNS_Manager.AddMessage)));

        [Fact]
        public void Limit_ChangesOnlyTheGamesTwenty()
        {
            List<CodeInstruction> before = AddMessageIL();
            List<CodeInstruction> after = SNS_Manager_AddMessage.RaiseCap(AddMessageIL(), out bool changed);
            Assert.True(changed);
            Assert.Equal(before.Count, after.Count);
            int differences = Enumerable.Range(0, before.Count).Count(i => before[i].opcode != after[i].opcode || !Equals(before[i].operand, after[i].operand));
            Assert.Equal(1, differences);
            Assert.Contains(after, c => c.Calls(AccessTools.Method(typeof(FeedStore), nameof(FeedStore.CapValue))));
        }

        [Fact]
        public void Limit_AlreadyChangedByAnotherMod_IsLeftAlone()
        {
            // Another mod's transpiler made the limit 50
            List<CodeInstruction> theirs = AddMessageIL();
            int index = theirs.FindIndex(c => c.LoadsConstant(20));
            theirs[index] = new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)50);
            List<CodeInstruction> after = SNS_Manager_AddMessage.RaiseCap(theirs.ToList(), out bool changed);
            Assert.False(changed);
            Assert.True(after[index].LoadsConstant(50));
            Assert.Contains(Logs, l => l.Contains("limit"));
        }

        [Fact]
        public void Limit_AppliedTwice_ChangesOnce()
        {
            List<CodeInstruction> once = SNS_Manager_AddMessage.RaiseCap(AddMessageIL(), out _);
            SNS_Manager_AddMessage.RaiseCap(once, out bool again);
            Assert.False(again);
        }

        [Fact]
        public void SkippedSingleRelease_SendsNothing()
        {
            singles._single single = new() { title = "Song", status = singles._single._status.released };
            singles_ReleaseSingle.Postfix(single, __runOriginal: false);
            Assert.Empty(transport.Sent);

            singles_ReleaseSingle.Postfix(single, __runOriginal: true);
            Assert.Single(transport.Sent);
        }

        [Fact]
        public void SingleNotReleased_SendsNothing()
        {
            singles_ReleaseSingle.Postfix(new singles._single { status = singles._single._status.working }, __runOriginal: true);
            Assert.Empty(transport.Sent);
        }

        [Fact]
        public void SkippedConcertFinish_SendsNothing()
        {
            SEvent_Concerts._concert concert = new() { Status = SEvent_Tour.tour._status.finished };
            SEvent_Concerts_concert_Finish.Postfix(concert, __runOriginal: false);
            concert.Status = SEvent_Tour.tour._status.normal;
            SEvent_Concerts_concert_Finish.Postfix(concert, __runOriginal: true);
            Assert.Empty(transport.Sent);
        }

        [Fact]
        public void EventsThatPostThemselves_AreLeftToTheGame()
        {
            Event_Manager._activeEvent active = new();
            Event_Manager._randomEvent._reply quiet = new();
            Event_Manager._randomEvent._reply posting = new();
            posting.SNS.Add(new Event_Manager._randomEvent._reply._SNS());

            Assert.True(Event_Manager_ConcludeEvent.ShouldPost(quiet, active, ran: true, publicOutcome: true));
            Assert.False(Event_Manager_ConcludeEvent.ShouldPost(posting, active, ran: true, publicOutcome: true));
            Assert.False(Event_Manager_ConcludeEvent.ShouldPost(quiet, active, ran: false, publicOutcome: true));
            Assert.False(Event_Manager_ConcludeEvent.ShouldPost(quiet, null, ran: true, publicOutcome: true));
        }

        /// <summary>
        /// An answer whose outcome the public doesn't see gets no posts.
        /// </summary>
        [Fact]
        public void EventsWithAPrivateOutcome_GetNoPosts()
        {
            Assert.False(Event_Manager_ConcludeEvent.ShouldPost(new Event_Manager._randomEvent._reply(), new Event_Manager._activeEvent(), ran: true, publicOutcome: false));
        }

        [Fact]
        public void BatchGraduations_SendOneRequestADay()
        {
            DateTime day = new(2020, 6, 3, 9, 0, 0);
            Assert.True(Moments.FirstGraduationToday(day));
            Assert.False(Moments.FirstGraduationToday(day.AddHours(5)));
            Assert.True(Moments.FirstGraduationToday(day.AddDays(1)));
        }

        [Fact]
        public void SaveFieldChangedByAnotherMod_IsWhatGetsSplit()
        {
            // Another mod put its own list in the save field before this postfix ran
            List<SNS_Manager._message> theirs = Feed(25, "theirs");
            List<variables._variable> vars = new();
            List<SNS_Manager._message> kept = SNS_Manager_SaveFunction.SaveFeed(theirs, vars);
            Assert.Equal("theirs 5", kept[0].Message);
            Assert.Equal(5, FeedStore.Deserialize(FeedStore.GetVariable(vars, FeedStore.OlderPostsVar)).Count);
        }

        [Fact]
        public void HistoryRestoredByAnotherMod_IsNotMergedAgain()
        {
            List<SNS_Manager._message> loaded = Feed(40);
            Assert.Equal(0, FeedStore.MergeOnLoad(loaded, Feed(30, "old")));
            Assert.Equal(40, loaded.Count);
        }

        [Fact]
        public void KeyWrittenStraightIntoTheVariables_LeavesBeforeTheSave()
        {
            variables.variable.Add(new variables._variable { name = Settings.ApiKeyVar, value = "written-directly" });
            variables_SaveFunction.Postfix();
            Assert.Equal("", variables.Get(Settings.ApiKeyVar));
            Assert.Equal("written-directly", KeyStore.Get(Provider.Anthropic));
        }

        [Fact]
        public void QuietPosts_HideTheirNotification()
        {
            List<NotificationType> normal = new() { NotificationType.other, NotificationType.resource_change };
            Assert.Equal(NotificationType.none, NotificationManager_AddNotification.QuietType(NotificationType.other, normal));
        }

        [Fact]
        public void QuietPosts_WithEveryNotificationOff_AreLeftAlone()
        {
            // The game lists only "none" when the player turns everything off, and then shows "none"
            List<NotificationType> allOff = new() { NotificationType.none };
            Assert.Equal(NotificationType.other, NotificationManager_AddNotification.QuietType(NotificationType.other, allOff));
        }

        [Fact]
        public void OtherNotifications_AreNeverChanged()
        {
            NotificationType type = NotificationType.idol_stat_change;
            NotificationManager_AddNotification.Prefix(ref type);
            Assert.Equal(NotificationType.idol_stat_change, type);
        }

        [Fact]
        public void KeyPrefix_RunsFirst()
        {
            HarmonyPriority priority = typeof(variables_Set).GetCustomAttribute<HarmonyPriority>();
            Assert.Equal(Priority.First, priority.info.priority);
        }

        [Fact]
        public void MomentPatches_RunLast()
        {
            foreach (Type patch in new[] { typeof(singles_ReleaseSingle), typeof(SEvent_Concerts_concert_Finish), typeof(data_girls_girls_Graduate), typeof(Event_Manager_ConcludeEvent), typeof(Event_Templates_ConcludeEvent) })
            {
                Assert.Equal(Priority.Last, patch.GetCustomAttribute<HarmonyPriority>().info.priority);
            }
        }

        /// <summary>
        /// No patch skips a game method: none is a bool prefix.
        /// </summary>
        [Fact]
        public void NoPatch_SkipsAGameMethod()
        {
            IEnumerable<MethodInfo> prefixes = typeof(Settings).Assembly.GetTypes()
                .Where(t => t.Namespace == "AIFanChatter")
                .Select(t => t.GetMethod("Prefix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(m => m != null);
            Assert.NotEmpty(prefixes);
            Assert.All(prefixes, m => Assert.Equal(typeof(void), m.ReturnType));
        }
    }
}
