using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// When an idol leaves a show, her replacement is never someone already in the cast.
    /// </summary>
    public class ShowCastRefillTests
    {
        public ShowCastRefillTests() => TestGame.Reset();

        [Fact]
        public void DuplicateRefillIsSwappedForAFreeIdol()
        {
            data_girls.girls leaver = TestGame.Registered(1, data_girls._status.graduated), b = TestGame.Registered(2), c = TestGame.Registered(3), d = TestGame.Registered(4);
            // The game refilled the leaver's slot 0 with b, who is already in slot 1
            Shows._show show = Show(b, b, c);

            Shows__show_RemoveGirl.Postfix(show, leaver, new List<int> { 0 }, __runOriginal: true);

            Assert.Equal(new[] { d, b, c }, show.girls);
        }

        [Fact]
        public void DuplicateRefillLeavesTheSlotEmptyWhenNoIdolIsFree()
        {
            data_girls.girls leaver = TestGame.Registered(1, data_girls._status.graduated), b = TestGame.Registered(2), c = TestGame.Registered(3);
            Shows._show show = Show(b, b, c);

            Shows__show_RemoveGirl.Postfix(show, leaver, new List<int> { 0 }, __runOriginal: true);

            Assert.Equal(new[] { null, b, c }, show.girls);
        }

        [Fact]
        public void SlotEmptiedByAnotherModIsRefilled()
        {
            data_girls.girls leaver = TestGame.Registered(1, data_girls._status.graduated), b = TestGame.Registered(2), c = TestGame.Registered(3), d = TestGame.Registered(4);
            // Another mod emptied the duplicate refill before this postfix ran
            Shows._show show = Show(null, b, c);

            Shows__show_RemoveGirl.Postfix(show, leaver, new List<int> { 0 }, __runOriginal: true);

            Assert.Equal(new[] { d, b, c }, show.girls);
        }

        [Fact]
        public void RefillThatIsNotADuplicateIsKept()
        {
            data_girls.girls leaver = TestGame.Registered(1, data_girls._status.graduated), b = TestGame.Registered(2), c = TestGame.Registered(3), d = TestGame.Registered(4);
            Shows._show show = Show(d, b, c);

            Shows__show_RemoveGirl.Postfix(show, leaver, new List<int> { 0 }, __runOriginal: true);

            Assert.Equal(new[] { d, b, c }, show.girls);
        }

        [Fact]
        public void SlotEmptiedByAModThatSkipsTheGameIsKeptEmpty()
        {
            data_girls.girls leaver = TestGame.Registered(1), b = TestGame.Registered(2), c = TestGame.Registered(3);
            TestGame.Registered(4);
            // Another mod empties the slot in its prefix and skips the game's RemoveGirl
            Shows._show show = Show(null, b, c);

            Shows__show_RemoveGirl.Postfix(show, leaver, new List<int> { 0 }, __runOriginal: false);

            Assert.Equal(new[] { null, b, c }, show.girls);
        }

        private static Shows._show Show(params data_girls.girls[] cast)
        {
            Shows._show show = new() { castType = Shows._show._castType.permanentCast };
            show.girls = cast.ToArray();
            return show;
        }
    }

    /// <summary>
    /// The show tooltip's next episode is next week on the show's weekday, not today.
    /// </summary>
    public class NextEpisodeDateTests
    {
        public NextEpisodeDateTests() => TestGame.Reset();

        [Fact]
        public void NextEpisodeIsNextWeekOnTheShowsWeekday()
        {
            DateTime monday = new(2023, 3, 6);
            Shows._show show = new() { LaunchDate = new DateTime(2023, 1, 2) };

            Assert.Equal(new DateTime(2023, 3, 13), show.GetNextEpisodeDate(monday));
            Assert.Equal(new DateTime(2023, 3, 13), show.GetNextEpisodeDate(monday.AddDays(2)));
        }
    }

    /// <summary>
    /// The discarded-date fix alongside another mod's fix of the same call, in either order.
    /// </summary>
    public class DiscardedDateTests
    {
        public class Holder
        {
            public DateTime Date = new(2023, 7, 1);
        }

        private static readonly FieldInfo DateField = AccessTools.Field(typeof(Holder), nameof(Holder.Date));

        public DiscardedDateTests() => TestGame.Reset();

        // Each test patches its own copy, so patches don't carry over between tests
        public static void ShortenA(Holder h) { h.Date.AddMonths(-6); }
        public static void ShortenB(Holder h) { h.Date.AddMonths(-6); }
        public static void ShortenC(Holder h) { h.Date.AddMonths(-6); }

        public static IEnumerable<CodeInstruction> Ours(IEnumerable<CodeInstruction> instructions)
            => DiscardedDate.Store(instructions, ci => ci.opcode == OpCodes.Ldflda && ReferenceEquals(ci.operand, DateField), typeof(DiscardedDateTests));

        // Another mod's version: also rewrites call + pop, with its own helper
        public static IEnumerable<CodeInstruction> Theirs(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo addMonths = AccessTools.Method(typeof(DateTime), nameof(DateTime.AddMonths));
            List<CodeInstruction> codes = new(instructions);
            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (ReferenceEquals(codes[i].operand, addMonths) && codes[i + 1].opcode == OpCodes.Pop)
                {
                    codes[i].operand = AccessTools.Method(typeof(DiscardedDateTests), nameof(TheirStore));
                    codes[i + 1].opcode = OpCodes.Nop;
                }
            }
            return codes;
        }

        public static void TheirStore(ref DateTime date, int months) => date = date.AddMonths(months);

        [Fact]
        public void FixedAlone()
        {
            Assert.Equal(new DateTime(2023, 1, 1), Run(nameof(ShortenA), theirs: null));
        }

        [Fact]
        public void FixedOnceWhenTheOtherModRunsFirst()
        {
            Assert.Equal(new DateTime(2023, 1, 1), Run(nameof(ShortenB), theirs: Priority.First));
        }

        [Fact]
        public void FixedOnceWhenTheOtherModRunsLast()
        {
            Assert.Equal(new DateTime(2023, 1, 1), Run(nameof(ShortenC), theirs: Priority.Last));
        }

        private static DateTime Run(string method, int? theirs)
        {
            MethodInfo original = AccessTools.Method(typeof(DiscardedDateTests), method);
            Harmony harmony = new("tests.UnofficialPatch.DiscardedDate." + method);
            try
            {
                harmony.Patch(original, transpiler: new HarmonyMethod(typeof(DiscardedDateTests), nameof(Ours)));
                if (theirs.HasValue)
                    harmony.Patch(original, transpiler: new HarmonyMethod(AccessTools.Method(typeof(DiscardedDateTests), nameof(Theirs)), theirs.Value));
                Holder holder = new();
                original.Invoke(null, new object[] { holder });
                return holder.Date;
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }
    }

    /// <summary>
    /// Concerts in production keep their place in the Special Events list after loading.
    /// </summary>
    public class ConcertListPlaceTests
    {
        public ConcertListPlaceTests() => TestGame.Reset();

        [Theory]
        [InlineData(SEvent_Tour.tour._status.working)]
        [InlineData(SEvent_Tour.tour._status.normal)]
        [InlineData(SEvent_Tour.tour._status.finished)]
        public void LoadedConcertKeepsItsDate(SEvent_Tour.tour._status status)
        {
            staticVars.SetTime(new DateTime(2023, 6, 1));
            DateTime created = new(2023, 4, 10);
            SEvent_Concerts._concert concert = new() { ID = 5, Status = status, FinishDate = created };

            SEvent_Concerts_LoadFunction.Prefix();
            try
            {
                concert.Initiate();
            }
            finally
            {
                SEvent_Concerts_LoadFunction.Finalizer();
            }

            Assert.Equal(created, concert.FinishDate);
        }

        [Fact]
        public void NewConcertStillGetsTodaysDate()
        {
            staticVars.SetTime(new DateTime(2023, 6, 1));
            SEvent_Concerts._concert concert = new() { ID = 5, FinishDate = new DateTime(2023, 4, 10) };

            concert.Initiate();

            Assert.Equal(new DateTime(2023, 6, 1), concert.FinishDate);
        }
    }

    /// <summary>
    /// Cancel in the concert editor puts back everything the editor changed.
    /// </summary>
    public class ConcertEditorCancelTests
    {
        public ConcertEditorCancelTests() => TestGame.Reset();

        [Theory]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        public void CancelRestoresOnlyWhenClosingTheEditor(bool pickingIdol, bool pickingSingle, bool restored)
        {
            SEvent_Concerts._concert concert = Concert();
            Concert_New_Popup popup = TestGame.Component<Concert_New_Popup>();
            popup.Concert = concert;
            Concert_New_Popup_Reset.Prefix(concert);
            concert.Title = "Changed";

            Concert_New_Popup_OnCancel.Prefix(popup, pickingIdol, pickingSingle, ___IsEditing: true);

            Assert.Equal(restored, concert.Title == "Spring");
        }

        [Fact]
        public void CancelledEditRestoresTheConcert()
        {
            data_girls.girls a = new(), b = new(), c = new();
            SEvent_Concerts._concert._song song = new() { Single = new singles._single(), Center = a };
            SEvent_Concerts._concert._mc mc = new();
            mc.SetGirl(b, 1);
            mc.SetTitle("Talk #1");
            SEvent_Concerts._concert concert = Concert();
            concert.SetListItems.AddRange(new SEvent_Concerts._concert.ISetlistItem[] { song, mc });
            concert.ProjectedValues.TicketPrice = 4000;
            ConcertSnapshot snapshot = new(concert);

            // Edits made in the editor
            concert.SetListItems.Remove(song);
            song.SetGirl(c);
            mc.SetGirl(c, 1);
            mc.SetTitle("Talk #9");
            concert.SetListItems.Add(new SEvent_Concerts._concert._mc());
            concert.Venue = SEvent_Concerts._venue.stadium;
            concert.ProjectedValues.TicketPrice = 9000;
            concert.Title = "Changed";
            concert.Cast_Changed = true;
            snapshot.Restore();

            Assert.Equal(new SEvent_Concerts._concert.ISetlistItem[] { song, mc }, concert.SetListItems);
            Assert.Same(a, song.Center);
            Assert.Equal(new[] { null, b, null }, mc.Girls);
            Assert.Equal("Talk #1", mc.GetTitle());
            Assert.Equal(SEvent_Concerts._venue.concertHall, concert.Venue);
            Assert.Equal(4000, concert.ProjectedValues.TicketPrice);
            Assert.Equal("Spring", concert.Title);
            Assert.False(concert.Cast_Changed);
        }

        [Fact]
        public void CancelledEditRestoresTheFanAppeal()
        {
            SEvent_Concerts._concert._song male = Song(resources.fanType.male), female = Song(resources.fanType.female);
            SEvent_Concerts._concert concert = Concert();
            concert.SetListItems.Add(male);
            concert.RecalcFanAppeal();
            ConcertSnapshot snapshot = new(concert);

            // The editor swaps the song and recalculates the appeal
            concert.SetListItems.Clear();
            concert.SetListItems.Add(female);
            concert.RecalcFanAppeal();
            snapshot.Restore();

            Assert.Equal(1f, concert.GetFanAppeal(resources.fanType.male).ratio);
            Assert.Equal(0f, concert.GetFanAppeal(resources.fanType.female).ratio);
        }

        private static SEvent_Concerts._concert._song Song(resources.fanType appeal)
        {
            singles._single single = new();
            single.ReleaseData.FanAppeal.Add(new singles._fanAppeal { type = appeal, ratio = 1f });
            return new SEvent_Concerts._concert._song { Single = single };
        }

        // An initiated concert with the game data its projected values read
        private static SEvent_Concerts._concert Concert()
        {
            SEvent_Concerts._concert concert = new() { ID = 5, Venue = SEvent_Concerts._venue.concertHall, Title = "Spring" };
            concert.Initiate();
            return concert;
        }
    }
}
