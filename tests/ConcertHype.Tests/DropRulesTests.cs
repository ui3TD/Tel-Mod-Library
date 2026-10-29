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
        private static readonly DropPoint Bench = DropPoint.Bench, Host = DropPoint.AtHost(0);

        /// <summary>
        /// The talk break's row with nobody on it.
        /// </summary>
        private static readonly int[] No = { -1, -1, -1 };

        private static DropPoint Place(int j) => DropPoint.AtPlace(j);

        private static DropPoint HostPlace(int k) => DropPoint.AtHost(k);

        private static ShowState Start() => Shows.Start(Shows.Setup());

        private static ShowState AfterFirstSong()
        {
            ShowState state = Start();
            Engine.Play(state, Engine.DefaultLineup(state));
            return state;
        }

        private static void Allowed(DropResult result, int[] lineup, int[] hosts = null, bool changed = true)
        {
            Assert.True(result.Allowed, result.Message);
            Assert.Equal("", result.Message);
            Assert.Equal(changed, result.Changed);
            Assert.Equal(lineup, result.Lineup);
            Assert.Equal(hosts ?? No, result.Hosts);
        }

        private static void Refused(DropResult result, int[] lineup, int[] hosts, string message)
        {
            Assert.False(result.Allowed);
            Assert.False(result.Changed);
            Assert.Equal(lineup, result.Lineup);
            Assert.Equal(hosts, result.Hosts);
            Assert.Equal(message, result.Message);
        }

        [Fact]
        public void BenchToPlace_TakesIt_AndHerPredecessorGoesBack()
        {
            ShowState state = Start();
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 5, Bench, Place(0)), new[] { 5, 1, 2 });
        }

        [Fact]
        public void ASongTakesAnyNumberOfSubstitutes()
        {
            ShowState state = Start();
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, No, 6, Bench, Place(2)), new[] { 5, 1, 6 });
            Allowed(DropRules.Drop(state, new[] { 5, 1, 6 }, No, 7, Bench, Place(1)), new[] { 5, 7, 6 });
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, No, 6, Bench, Place(0)), new[] { 6, 1, 2 });
        }

        [Fact]
        public void APlaceWhoseIdolCantPerform_TakesAnyBenchIdol()
        {
            ShowState state = Start();
            state.Absence[1] = 2;
            int[] lineup = Engine.DefaultLineup(state);
            Assert.Equal(3, lineup[1]);  // the best on the bench: every bench member is alike, so the first
            Allowed(DropRules.Drop(state, lineup, No, 5, Bench, Place(0)), new[] { 5, 3, 2 });
            Allowed(DropRules.Drop(state, new[] { 5, 3, 2 }, No, 6, Bench, Place(1)), new[] { 5, 6, 2 });
        }

        [Fact]
        public void ABenchIdolTakesAnyPlace_HerOwnOrAnother()
        {
            ShowState state = Start();
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, No, 0, Bench, Place(2)), new[] { 5, 1, 0 });
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, No, 0, Bench, Place(0)), new[] { 0, 1, 2 });
        }

        [Fact]
        public void MovedOntoAnEmptyPlace_HerOwnPlaceGetsTheBestOnTheBench()
        {
            ShowState state = Start();
            state.Absence[1] = 2;
            // Place 1's idol can't perform and nobody stands in: moving the center there gives the center to the bench's best
            Allowed(DropRules.Drop(state, new[] { 0, -1, 2 }, No, 0, Place(0), Place(1)), new[] { 3, 0, 2 });
            // With nobody left on the bench, her own place would be empty: refused
            ShowState small = Shows.Start(Shows.Setup(members: 3));
            small.Absence[1] = 2;
            Refused(DropRules.Drop(small, new[] { 0, -1, 2 }, No, 0, Place(0), Place(1)), new[] { 0, -1, 2 }, No,
                "That change isn't allowed in this song.");
        }

        [Fact]
        public void AnIdolWhoCantPerform_IsRefusedEverywhere()
        {
            ShowState state = AfterFirstSong();
            state.Absence[6] = 1;
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 6, Bench, Place(0)), new[] { 0, 1, 2 }, No, "M6 can't perform right now.");
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 6, Bench, Host), new[] { 0, 1, 2 }, No, "M6 can't perform right now.");
        }

        [Fact]
        public void ASubstituteDraggedOff_BringsTheOriginalBack()
        {
            ShowState state = Start();
            Allowed(DropRules.Drop(state, new[] { 5, 1, 2 }, No, 5, Place(0), Bench), new[] { 0, 1, 2 });
        }

        [Fact]
        public void AnIdolWhosePlaceCantGoBackToItsOwnIdol_CantJustLeave()
        {
            ShowState state = Start();
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 1, Place(1), Bench), new[] { 0, 1, 2 }, No,
                "M1 can't just leave: drag an idol from the bench onto her place.");
            // A swapped original: the place's own idol is on stage elsewhere
            Refused(DropRules.Drop(state, new[] { 1, 0, 2 }, No, 1, Place(0), Bench), new[] { 1, 0, 2 }, No,
                "M1 can't just leave: drag an idol from the bench onto her place.");
            state.Absence[1] = 2;
            Refused(DropRules.Drop(state, new[] { 0, 3, 2 }, No, 3, Place(1), Bench), new[] { 0, 3, 2 }, No,
                "M3 can't just leave: drag an idol from the bench onto her place.");
        }

        [Fact]
        public void TwoIdolsOnStage_SwapPlaces_AndDroppingBackChangesNothing()
        {
            ShowState state = Start();
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 0, Place(0), Place(1)), new[] { 1, 0, 2 });
            Allowed(DropRules.Drop(state, new[] { 1, 0, 2 }, No, 0, Place(1), Place(0)), new[] { 0, 1, 2 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 0, Place(0), Place(0)), new[] { 0, 1, 2 }, changed: false);
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 5, Bench, Bench), new[] { 0, 1, 2 }, changed: false);
        }

        [Fact]
        public void AHostPlace_TakesABenchIdolOrAPerformer_WhoKeepsHerPlace_AndItsHostGoesBack()
        {
            ShowState state = AfterFirstSong();
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 4, Bench, Host), new[] { 0, 1, 2 }, new[] { 4, -1, -1 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, new[] { 4, -1, -1 }, 1, Bench, HostPlace(2)), new[] { 0, 1, 2 }, new[] { 4, -1, 1 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, new[] { 4, -1, 1 }, 5, Bench, HostPlace(0)), new[] { 0, 1, 2 }, new[] { 5, -1, 1 });
        }

        [Fact]
        public void HostsMoveAlongTheRow_AndSwap()
        {
            ShowState state = AfterFirstSong();
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, new[] { 4, -1, 5 }, 4, HostPlace(0), HostPlace(1)), new[] { 0, 1, 2 }, new[] { -1, 4, 5 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, new[] { 4, -1, 5 }, 4, HostPlace(0), HostPlace(2)), new[] { 0, 1, 2 }, new[] { 5, -1, 4 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, new[] { 4, -1, 5 }, 4, HostPlace(0), HostPlace(0)), new[] { 0, 1, 2 }, new[] { 4, -1, 5 }, changed: false);
        }

        [Fact]
        public void AHostDraggedOffTheRow_DoesntHost_AndSongPlacesAndHostPlacesDontMix()
        {
            ShowState state = AfterFirstSong();
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, new[] { 4, -1, 5 }, 5, HostPlace(2), Bench), new[] { 0, 1, 2 }, new[] { 4, -1, -1 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, new[] { 4, -1, 5 }, 4, HostPlace(0), Place(0)), new[] { 0, 1, 2 }, new[] { -1, -1, 5 });
            Allowed(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 1, Place(1), Host), new[] { 0, 1, 2 }, changed: false);
        }

        [Fact]
        public void TheHostPlace_RefusesBeforeTheFirstSong_AfterATalkBreakInThisGap_AndWithNoneLeft()
        {
            ShowState state = Start();
            Assert.Equal("CONCERTHYPE__TALK_NOT_YET", DropRules.TalkBlocked(state));
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 4, Bench, Host), new[] { 0, 1, 2 }, No, "Talk breaks can start after the first song.");

            Engine.Play(state, Engine.DefaultLineup(state));
            Assert.Null(DropRules.TalkBlocked(state));
            Engine.CallMc(state, 4);
            Assert.Equal("CONCERTHYPE__TALK_DONE", DropRules.TalkBlocked(state));
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 5, Bench, Host), new[] { 0, 1, 2 }, No,
                "One talk break between songs; the next can come after this song.");

            Engine.Play(state, Engine.DefaultLineup(state));
            state.McCharges = 0;
            Assert.Equal("CONCERTHYPE__TALK_NONE_LEFT", DropRules.TalkBlocked(state));
            Refused(DropRules.Drop(state, new[] { 0, 1, 2 }, No, 5, Bench, Host), new[] { 0, 1, 2 }, No, "No talk breaks left in this show.");
        }

        [Fact]
        public void AClickOnABenchIdol_FillsTheFirstEmptyPlace_OrSaysToDrag()
        {
            ShowState state = Start();
            state.Absence[1] = 2;
            Allowed(DropRules.Click(state, new[] { 0, -1, 2 }, No, 6), new[] { 0, 6, 2 });
            Refused(DropRules.Click(state, new[] { 0, 3, 2 }, No, 6), new[] { 0, 3, 2 }, No, "Drag M6 onto a place.");
        }

        [Fact]
        public void AClickOnABenchIdolOnTheTalkBreaksTab_FillsTheFirstEmptyHostPlace_OrSaysWhyNot()
        {
            ShowState state = Start();
            Refused(DropRules.ClickHost(state, new[] { 0, 1, 2 }, No, 4), new[] { 0, 1, 2 }, No, "Talk breaks can start after the first song.");
            Engine.Play(state, Engine.DefaultLineup(state));
            Allowed(DropRules.ClickHost(state, new[] { 0, 1, 2 }, new[] { 4, -1, 5 }, 1), new[] { 0, 1, 2 }, new[] { 4, 1, 5 });
            Refused(DropRules.ClickHost(state, new[] { 0, 1, 2 }, new[] { 4, 1, 5 }, 6), new[] { 0, 1, 2 }, new[] { 4, 1, 5 }, "Drag M6 onto a talk break place.");
            state.Absence[6] = 1;
            Refused(DropRules.ClickHost(state, new[] { 0, 1, 2 }, No, 6), new[] { 0, 1, 2 }, No, "M6 can't perform right now.");
        }

        [Fact]
        public void AfterTheShow_NothingMoves()
        {
            ShowState state = Shows.Start(Shows.Setup(songs: 1));
            Engine.Play(state, Engine.DefaultLineup(state));
            DropResult result = DropRules.Drop(state, new[] { 0, 1, 2 }, No, 5, Bench, Place(0));
            Assert.False(result.Changed);
            Assert.Equal(new[] { 0, 1, 2 }, result.Lineup);
        }

        [Fact]
        public void EveryRefusal_HasItsText()
        {
            foreach (string key in new[] { "CANT_LEAVE", "CANT_PERFORM", "NOT_ALLOWED", "CLICK_DRAG", "CLICK_DRAG_HOST", "TALK_NOT_YET", "TALK_DONE", "TALK_NONE_LEFT" })
                Assert.True(Texts.English.ContainsKey("CONCERTHYPE__" + key), key);
            Assert.All(new[] { "NOT_ALLOWED", "TALK_NOT_YET" }.Select(k => Texts.English["CONCERTHYPE__" + k]), text => Assert.DoesNotContain("{0}", text));
        }
    }
}
