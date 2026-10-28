using ConcertHype.Model;
using System.Linq;
using Xunit;

namespace ConcertHype.Tests
{
    /// <summary>
    /// The drag-and-drop table: 8 members, songs of 3 places held by members 0, 1 and 2; members 3 to 7 on the bench.
    /// </summary>
    public class DropRulesTests
    {
        private static readonly DropPoint Bench = DropPoint.Bench, Host = DropPoint.Host;

        private static DropPoint Place(int j) => DropPoint.AtPlace(j);

        private static ShowState Start() => Shows.Start(Shows.Setup());

        private static ShowState AfterFirstSong()
        {
            ShowState state = Start();
            Engine.Play(state, Engine.DefaultLineup(state));
            return state;
        }

        private static void Allowed(DropResult result, int[] lineup, int host = -1, bool changed = true)
        {
            Assert.True(result.Allowed, result.Message);
            Assert.Equal("", result.Message);
            Assert.Equal(changed, result.Changed);
            Assert.Equal(lineup, result.Lineup);
            Assert.Equal(host, result.Host);
        }

        private static void Refused(DropResult result, int[] lineup, int host, string message)
        {
            Assert.False(result.Allowed);
            Assert.False(result.Changed);
            Assert.Equal(lineup, result.Lineup);
            Assert.Equal(host, result.Host);
            Assert.Equal(message, result.Message);
        }

        [Fact]
        public void BenchToPlace_TakesIt_AndHerPredecessorGoesBack()
        {
            ShowState state = Start();
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 5, Bench, Place(0)), new[] { 5, 1, 2 });
        }

        [Fact]
        public void ASecondChange_PutsTheFirstBack()
        {
            ShowState state = Start();
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, -1, 6, Bench, Place(2)), new[] { 0, 1, 6 });
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, -1, 6, Bench, Place(0)), new[] { 6, 1, 2 });
        }

        [Fact]
        public void FillingAPlaceWhoseIdolCantPerform_IsFree()
        {
            ShowState state = Start();
            state.Absence[1] = 2;
            int[] lineup = Engine.DefaultLineup(state);
            Assert.Equal(3, lineup[1]);  // the best fill: every bench member is alike, so the first
            Allowed(DropRules.Drop(state, lineup, -1, 5, Bench, Place(0)), new[] { 5, 3, 2 });
            Allowed(DropRules.Drop(state, new[] { 5, 3, 2 }, -1, 6, Bench, Place(1)), new[] { 5, 6, 2 });
        }

        [Fact]
        public void ABenchIdolWithHerOwnPlace_TakesOnlyThatOne()
        {
            ShowState state = Start();
            Refused(DropRules.Drop(state, new[] { 5, 1, 2 }, -1, 0, Bench, Place(2)), new[] { 5, 1, 2 }, -1, "M0 has her own place in this song.");
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, -1, 0, Bench, Place(0)), new[] { 0, 1, 2 });
        }

        [Fact]
        public void AnIdolWhoCantPerform_IsRefusedEverywhere()
        {
            ShowState state = AfterFirstSong();
            state.Absence[6] = 1;
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 6, Bench, Place(0)), new[] { 0, 1, 2 }, -1, "M6 can't perform right now.");
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 6, Bench, Host), new[] { 0, 1, 2 }, -1, "M6 can't perform right now.");
        }

        [Fact]
        public void ASubstituteDraggedOff_BringsTheOriginalBack()
        {
            ShowState state = Start();
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, -1, 5, Place(0), Bench), new[] { 0, 1, 2 });
        }

        [Fact]
        public void AnOriginalOrAFill_CantJustLeave()
        {
            ShowState state = Start();
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 1, Place(1), Bench), new[] { 0, 1, 2 }, -1,
                "M1 can't just leave: drag an idol from the bench onto her place.");
            state.Absence[1] = 2;
            Refused(DropRules.Drop(state, new[] { 0, 3, 2 }, -1, 3, Place(1), Bench), new[] { 0, 3, 2 }, -1,
                "M3 can't just leave: drag an idol from the bench onto her place.");
        }

        [Fact]
        public void PlacesCantBeSwapped_AndDroppingBackChangesNothing()
        {
            ShowState state = Start();
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 0, Place(0), Place(1)), new[] { 0, 1, 2 }, -1,
                "Idols can't swap places: drag an idol from the bench instead.");
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 0, Place(0), Place(0)), new[] { 0, 1, 2 }, changed: false);
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 5, Bench, Bench), new[] { 0, 1, 2 }, changed: false);
        }

        [Fact]
        public void TheHostPlace_TakesABenchIdolOrAPerformer_WhoKeepsHerPlace()
        {
            ShowState state = AfterFirstSong();
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 4, Bench, Host), new[] { 0, 1, 2 }, host: 4);
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, 4, 1, Place(1), Host), new[] { 0, 1, 2 }, host: 1);
        }

        [Fact]
        public void DraggingTheHostAnywhere_CancelsTheTalkBreak()
        {
            ShowState state = AfterFirstSong();
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, 4, 4, Host, Bench), new[] { 0, 1, 2 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, 4, 4, Host, Place(0)), new[] { 0, 1, 2 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, 4, 4, Host, Host), new[] { 0, 1, 2 }, host: 4, changed: false);
        }

        [Fact]
        public void TheHostPlace_RefusesBeforeTheFirstSong_AfterATalkBreakInThisGap_AndWithNoneLeft()
        {
            ShowState state = Start();
            Assert.Equal("CONCERTHYPE__TALK_NOT_YET", DropRules.TalkBlocked(state));
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 4, Bench, Host), new[] { 0, 1, 2 }, -1, "Talk breaks can start after the first song.");

            Engine.Play(state, Engine.DefaultLineup(state));
            Assert.Null(DropRules.TalkBlocked(state));
            Engine.CallMc(state, 4);
            Assert.Equal("CONCERTHYPE__TALK_DONE", DropRules.TalkBlocked(state));
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 5, Bench, Host), new[] { 0, 1, 2 }, -1,
                "One talk break between songs; the next can come after this song.");

            Engine.Play(state, Engine.DefaultLineup(state));
            state.McCharges = 0;
            Assert.Equal("CONCERTHYPE__TALK_NONE_LEFT", DropRules.TalkBlocked(state));
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 5, Bench, Host), new[] { 0, 1, 2 }, -1, "No talk breaks left in this show.");
        }

        [Fact]
        public void AClickOnABenchIdol_FillsTheFirstEmptyPlace_OrSaysToDrag()
        {
            ShowState state = Start();
            state.Absence[1] = 2;
            Allowed(DropRules.Click(state, new[] { 0, -1, 2 }, -1, 6), new[] { 0, 6, 2 });
            Refused(DropRules.Click(state, new[] { 0, 3, 2 }, -1, 6), new[] { 0, 3, 2 }, -1, "Drag M6 onto a place, or onto the talk break.");
        }

        [Fact]
        public void AfterTheShow_NothingMoves()
        {
            ShowState state = Shows.Start(Shows.Setup(songs: 1));
            Engine.Play(state, Engine.DefaultLineup(state));
            DropResult result = DropRules.Drop(state, new[] { 0, 1, 2 }, -1, 5, Bench, Place(0));
            Assert.False(result.Changed);
            Assert.Equal(new[] { 0, 1, 2 }, result.Lineup);
        }

        [Fact]
        public void EveryRefusal_HasItsText()
        {
            foreach (string key in new[] { "NO_SWAP", "CANT_LEAVE", "OWN_PLACE", "CANT_PERFORM", "NOT_ALLOWED", "CLICK_DRAG", "TALK_NOT_YET", "TALK_DONE", "TALK_NONE_LEFT" })
                Assert.True(Texts.English.ContainsKey("CONCERTHYPE__" + key), key);
            Assert.All(new[] { "NO_SWAP", "NOT_ALLOWED", "TALK_NOT_YET" }.Select(k => Texts.English["CONCERTHYPE__" + k]), text => Assert.DoesNotContain("{0}", text));
        }
    }
}
