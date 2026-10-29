using ConcertHype.Model;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using _concert = SEvent_Concerts._concert;

namespace ConcertHype.Tests
{
    public class PanelViewTests : IDisposable
    {
        public PanelViewTests() => GameFixture.Reset();

        public void Dispose() => GameFixture.Reset();

        /// <summary>
        /// Cast: Idol0, Idol1, Idol3, Idol4, Idol5 (Idol2 is injured, so song 1's third place must be filled).
        /// </summary>
        private static ShowSession ThreeSongs(out _concert concert, double rollSigma = 0)
        {
            data_girls.girls[] idols = Enumerable.Range(0, 6).Select(i => GameFixture.Idol("Idol" + i, dance: 50 + 5 * i, vocal: 55)).ToArray();
            idols[2].status = data_girls._status.injured;
            concert = GameFixture.Concert(
                GameFixture.Single("S1", idols[0], idols[1], idols[2]),
                GameFixture.Single("S2", idols[3], idols[4], idols[5]),
                GameFixture.Single("S3", idols[0], idols[3], idols[4]));
            concert.Hype = 50;
            return GameFixture.Begin(concert, new Config { Seed = 3, RollSigma = rollSigma, PBase = 0, PRoll = 0, PTired = 0 });
        }

        [Fact]
        public void Slots_ShowRowsBadgesAndSuccessChances()
        {
            ShowSession session = ThreeSongs(out _);
            Assert.True(session.Drop(3, DropPoint.Bench, DropPoint.AtPlace(0)).Changed);
            PanelView view = PanelView.Build(session);

            Assert.Equal(new[] { 0, 1, 1 }, view.Slots.Select(s => s.Row));
            Assert.Equal(new[] { SlotBadge.Substitute, SlotBadge.Original, SlotBadge.Substitute }, view.Slots.Select(s => s.Badge));
            SongPreview preview = session.Preview();
            Assert.Equal(PanelView.SuccessChance(session.State.Config, preview.Slots[1].Expected), view.Slots[1].Success);
            Assert.All(view.Slots, s => Assert.Equal(Highlight.None, s.Highlight));
            Assert.Equal("⇄", PanelView.BadgeText(SlotBadge.Substitute));
            Assert.Equal("★", PanelView.BadgeText(SlotBadge.Original));
        }

        [Fact]
        public void Readouts_AreTitlesOverValues_WithTheRangeAndMoodInTheTip()
        {
            ShowSession session = ThreeSongs(out _);
            PanelView view = PanelView.Build(session);
            SongPreview preview = session.Preview();
            Assert.Equal("Expected Hype:", view.Expected.Title);
            Assert.Equal(PanelView.Signed(preview.Change) + "%", view.Expected.Value);
            Assert.Equal(Math.Round(preview.Change) >= 0 ? Tone.Good : Tone.Bad, view.Expected.Tone);
            Assert.Equal($"Likely {PanelView.Signed(preview.Low)}% to {PanelView.Signed(preview.High)}%, depending on how the idols perform. "
                + "Hype 50%: a changed lineup surprises the crowd.", view.Expected.Tip);
            Assert.Equal("Changed Lineup:", view.Changed.Title);
            Assert.Equal(Math.Round(100 * preview.Novelty) + "%", view.Changed.Value);
            Assert.Equal(Tone.Plain, view.Changed.Tone);
            Assert.Equal("The share of places not held by their own idol from the single; the center counts most. The more changed, the more the crowd's mood raises or lowers the hype change.", view.Changed.Tip);
            Assert.False(view.Last.Shown);  // nothing played yet
            Assert.Equal("", view.Hint);  // no standing instructions: only why a drop was refused
            Assert.Equal("M can't perform right now.", PanelView.Build(session, message: "M can't perform right now.").Hint);

            session.State.Hype = 150;
            Assert.EndsWith("Hype 150%: the crowd wants the hits.", PanelView.Build(session).Expected.Tip);
            session.State.Hype = 95;
            Assert.EndsWith("Hype 95%: the crowd is close to 100%.", PanelView.Build(session).Expected.Tip);
        }

        [Theory]
        [InlineData(0.4, "+0")]
        [InlineData(-0.4, "+0")]
        [InlineData(-0.6, "-1")]
        [InlineData(12.5, "+12")]
        public void Signed_RoundsFirst_SoAlmostNothingIsPlusZero(double change, string shown)
        {
            Assert.Equal(shown, PanelView.Signed(change));
        }

        [Fact]
        public void TheTalkBreaksTab_IsDisabledWithTheReason_UntilOneCanBeCalled_AndAHostChangesStart()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            PanelView view = PanelView.Build(session);
            Assert.Equal("Talk Break (2 left)", view.TalkTitle);
            Assert.False(view.TalkOpen);  // not before the first song
            Assert.Equal("Talk breaks can start after the first song.", view.TalkTip);
            Assert.Equal(new[] { -1, -1, -1 }, view.Hosts.Select(h => h.Member));
            Assert.Null(view.StartLabel);

            FakeStage stage = new(concert);
            stage.PlaySong(session);
            view = PanelView.Build(session, tab: FormationTab.Talk);
            Assert.True(view.TalkOpen);
            Assert.Equal("", view.TalkTip);
            Assert.Equal("", view.Hint);

            session.Drop(1, DropPoint.Bench, DropPoint.AtHost(1));
            view = PanelView.Build(session, tab: FormationTab.Talk);
            Assert.Equal(new[] { -1, 1, -1 }, view.Hosts.Select(h => h.Member));
            Assert.Equal("Start Talk Break", view.StartLabel);
            Assert.Equal("Start Talk Break", PanelView.Build(session).StartLabel);  // on either tab

            // Once it's played, the next one comes after the song
            stage.PlaySong(session);
            Assert.False(PanelView.Build(session).TalkOpen);
            Assert.Equal("One talk break between songs; the next can come after this song.", PanelView.Build(session).TalkTip);
        }

        [Fact]
        public void Last_IsTheLastSongsOrTalkBreaksChange_GreenUnlessALoss()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            FakeStage stage = new(concert);
            stage.PlaySong(session);
            Readout last = PanelView.Build(session).Last;
            Assert.Equal("Last Song:", last.Title);
            Assert.Equal(PanelView.Signed(session.LastResult.Change) + "%", last.Value);
            Assert.Equal("", last.Tip);  // who got hurt shows on the bench
            session.LastResult.Change = -0.4;
            Assert.Equal(("+0%", Tone.Good), (PanelView.Build(session).Last.Value, PanelView.Build(session).Last.Tone));
            session.LastResult.Change = -3;
            Assert.Equal(("-3%", Tone.Bad), (PanelView.Build(session).Last.Value, PanelView.Build(session).Last.Tone));

            session.Drop(2, DropPoint.Bench, DropPoint.AtHost(0));
            stage.PlaySong(session);
            last = PanelView.Build(session).Last;
            Assert.Equal("Last Talk Break:", last.Title);
            Assert.Equal(PanelView.Signed(session.LastMc.HypeAfter - session.LastMc.HypeBefore) + "%", last.Value);
        }

        [Fact]
        public void TheScore_IsATitleOverAValue_WithItsPartsInTheTip()
        {
            Readout score = PanelView.ScoreReadout(new Score { Total = 140.88, Average = 142.72, Peak = 190.71, Finale = 87.38 });
            Assert.Equal("Show Score:", score.Title);
            Assert.Equal("141%", score.Value);
            Assert.Equal(Tone.Plain, score.Tone);
            Assert.Equal("Average 143%, best 191%, finale 87%.", score.Tip);
        }

        [Fact]
        public void TheBench_IsTheCastNotInTheTabsSetUp()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            Assert.Equal(new[] { 0, 1, 4 }, session.Lineup);  // Idol5 (4) fills the injured idol's place
            Assert.Equal(new[] { 2, 3 }, PanelView.Build(session).Bench.Select(b => b.Member));

            // Song 2 is 2, 3, 4: a performer and a bench idol host
            new FakeStage(concert).PlaySong(session);
            session.Drop(2, DropPoint.Bench, DropPoint.AtHost(0));
            session.Drop(0, DropPoint.Bench, DropPoint.AtHost(1));
            // On the song's tab a host who doesn't perform is on the bench, free to perform too
            Assert.Equal(new[] { 0, 1 }, PanelView.Build(session).Bench.Select(b => b.Member));
            // On the talk break's tab everyone but the hosts, performers included
            Assert.Equal(new[] { 1, 3, 4 }, PanelView.Build(session, tab: FormationTab.Talk).Bench.Select(b => b.Member));
        }

        [Fact]
        public void TheBench_ShowsWhoCantPerform()
        {
            BenchView Idol1(PanelView view) => view.Bench.Single(b => b.Member == 1);
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            session.State.Absence[1] = 2;
            PanelView view = PanelView.Build(session);
            Assert.Equal(new[] { 0, 1 }, view.Bench.Select(b => b.Member));
            Assert.Equal(IconKind.Hurt, Idol1(view).Icon);
            Assert.Equal("2", Idol1(view).Count);
            Assert.Equal("Hurt: out for 2 songs. A talk break counts as one song.", Idol1(view).Tip);
            Assert.False(Idol1(view).Available);
            session.State.Absence[1] = 1;
            Assert.Equal("Hurt: out for 1 song. A talk break counts as one song.", Idol1(PanelView.Build(session)).Tip);

            session.State.Absence[1] = 0;
            session.State.Exhausted[1] = true;
            session.State.Energy[1] = 10;
            view = PanelView.Build(session);
            Assert.Equal(IconKind.Resting, Idol1(view).Icon);
            Assert.Equal("", Idol1(view).Count);
            Assert.Equal($"Exhausted: resting until her energy is back to {session.State.Config.ReturnThreshold}%.", Idol1(view).Tip);
            Assert.Equal(10, Idol1(view).Energy);

            session.State.Exhausted[1] = false;
            Assert.Equal(IconKind.None, Idol1(PanelView.Build(session)).Icon);
        }

        [Fact]
        public void EachPlace_HasTheIconAndTooltipOfItsRole()
        {
            ShowSession session = ThreeSongs(out _);
            session.Drop(3, DropPoint.Bench, DropPoint.AtPlace(0));
            PanelView view = PanelView.Build(session);
            Assert.Equal(new[] { IconKind.Substitute, IconKind.Original, IconKind.Substitute }, view.Slots.Select(s => s.Icon));
            Assert.Equal("Substitute: performs in another idol's place, without the senbatsu bonus.", view.Slots[0].Tip);
            Assert.Equal("Original: her own place in the single, where she gets the senbatsu bonus.", view.Slots[1].Tip);
            Assert.Equal(view.Slots[0].Tip, view.Slots[2].Tip);  // standing in for the injured idol is a substitute too
            // An empty place shows the game's empty senbatsu place, with no icon
            Assert.Equal(IconKind.None, PanelView.IconOf(SlotBadge.Empty));
            Assert.Equal("", PanelView.TipOf(SlotBadge.Empty));
            Assert.Equal("", view.Hint);  // the meanings are in the icons' tooltips, not on screen
        }

        [Fact]
        public void TheBars_ExplainEnergyAndSuccess_WhenHovered()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            ShowState state = session.State;
            Config config = state.Config;
            string Whole(double value) => Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Song 2 is 3, 4, 5 in rows 0, 1, 1: a front place costs more than a back one
            PanelView view = PanelView.Build(session);
            SongPreview preview = session.Preview();
            for (int j = 0; j < view.Slots.Count; j++)
            {
                SlotView slot = view.Slots[j];
                double cost = config.EnergyCostBase * config.RowCostOf(slot.Row);
                Assert.Equal($"Energy {Whole(state.Energy[slot.Member])}%. This song uses about {Whole(cost)}.", slot.EnergyTip);
                double[] chances = Outcomes.Chances(config, preview.Slots[j].Expected);
                Assert.Equal($"Success {Whole(chances[(int)Outcome.Success] + chances[(int)Outcome.CriticalSuccess])}% "
                    + $"(critical {Whole(chances[(int)Outcome.CriticalSuccess])}%). Critical failure {Whole(chances[(int)Outcome.CriticalFailure])}%.", slot.SuccessTip);
            }
            Assert.Equal("12", Whole(config.EnergyCostBase * config.RowCostOf(0)));
            Assert.NotEqual(view.Slots[0].EnergyTip, view.Slots[1].EnergyTip);
            BenchView bench = view.Bench.First(b => b.Member == 0);
            Assert.Equal($"Energy {Whole(state.Energy[0])}%. Recovers {Whole(config.BenchRecovery)} per song sat out.", bench.EnergyTip);
            Assert.False(view.Dragging);

            // A host's energy doesn't come back during the talk break; an empty host place has no tip
            session.Drop(0, DropPoint.Bench, DropPoint.AtHost(1));
            PanelView talk = PanelView.Build(session, tab: FormationTab.Talk);
            Assert.Equal($"Energy {Whole(state.Energy[0])}%. Hosts don't recover.", talk.Hosts[1].EnergyTip);
            Assert.Equal("", talk.Hosts[0].EnergyTip);

            // While an idol is dragged, the strips let the mouse through to the places
            Assert.True(PanelView.Build(session, new DragView { Member = 1, From = DropPoint.Bench }).Dragging);
        }

        [Fact]
        public void EachTab_HasItsReadouts_AndAHostingPerformerShowsAsUsualInTheSong()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            PanelView talk = PanelView.Build(session, tab: FormationTab.Talk);
            Assert.False(talk.Expected.Shown);  // nobody hosts yet
            Assert.False(talk.Changed.Shown);

            // Song 2 is 2, 3, 4: the center hosts with a bench idol, and stays in the lineup for the song
            PanelView before = PanelView.Build(session);
            session.Drop(2, DropPoint.Bench, DropPoint.AtHost(0));
            session.Drop(0, DropPoint.Bench, DropPoint.AtHost(1));
            talk = PanelView.Build(session, tab: FormationTab.Talk);
            string pull = Math.Round(Engine.McHype(session.State, 2, 0)).ToString();
            Assert.Equal("After Talk Break:", talk.Expected.Title);
            Assert.Equal(pull + "%", talk.Expected.Value);
            Assert.Equal(Tone.Plain, talk.Expected.Tone);
            Assert.Equal("The hype expected after the talk break. The song plays after it.", talk.Expected.Tip);
            Assert.False(talk.Changed.Shown);

            PanelView song = PanelView.Build(session);
            Assert.Equal((before.Expected.Title, before.Expected.Value, before.Changed.Value), (song.Expected.Title, song.Expected.Value, song.Changed.Value));
            Assert.Equal(2, song.Slots[0].Member);
            Assert.Equal(before.Slots[0].Success, song.Slots[0].Success);
            Assert.Equal((before.Slots[0].Icon, before.Slots[0].Tip), (song.Slots[0].Icon, song.Slots[0].Tip));
            Assert.Equal(new[] { 2, 3, 4 }, session.Lineup);
        }

        [Fact]
        public void Dragging_OverAHostPlace_PreviewsTheRow()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            session.Drop(1, DropPoint.Bench, DropPoint.AtHost(0));
            PanelView view = PanelView.Build(session, new DragView { Member = 0, From = DropPoint.Bench, Over = DropPoint.AtHost(0) }, tab: FormationTab.Talk);
            Assert.Equal(new[] { 0, -1, -1 }, view.Hosts.Select(h => h.Member));
            Assert.Equal(new[] { Highlight.Allowed, Highlight.None, Highlight.None }, view.Hosts.Select(h => h.Highlight));
            Assert.Equal(new[] { 1, -1, -1 }, session.Hosts);  // nothing changed yet
            Assert.Contains(0, view.Bench.Select(b => b.Member));  // still on the bench until she's dropped
        }

        [Theory]
        [InlineData(true, false, true, false, true)]
        [InlineData(true, false, false, true, true)]
        [InlineData(true, false, false, false, false)]
        [InlineData(true, true, true, false, false)]
        [InlineData(false, false, true, false, false)]
        public void TheProfileCard_MovesOnlyForTheSameIdolOnAnotherOfTheShowsPortraits(bool sameGirl, bool sameButton, bool newIsOurs, bool cachedGone, bool moves)
        {
            Assert.Equal(moves, ProfileCard.NeedsMove(sameGirl, sameButton, newIsOurs, cachedGone));
        }

        [Fact]
        public void Dragging_OverAPlace_PreviewsTheDrop_AndTheBenchStaysPut()
        {
            ShowSession session = ThreeSongs(out _);
            PanelView before = PanelView.Build(session);
            DragView drag = new() { Member = 3, From = DropPoint.Bench, Over = DropPoint.AtPlace(1) };
            PanelView view = PanelView.Build(session, drag);

            Assert.Equal(3, view.Slots[1].Member);
            Assert.Equal(SlotBadge.Substitute, view.Slots[1].Badge);
            Assert.Equal(Highlight.Allowed, view.Slots[1].Highlight);
            Assert.NotEqual((before.Expected.Value, before.Expected.Tip, before.Changed.Value), (view.Expected.Value, view.Expected.Tip, view.Changed.Value));
            Assert.Equal(before.Bench.Select(b => b.Member), view.Bench.Select(b => b.Member));
            Assert.Equal(new[] { 0, 1, before.Slots[2].Member }, session.Lineup);  // nothing changed yet
        }

        [Fact]
        public void Dragging_OneIdolOnStageOverAnother_PreviewsTheSwap()
        {
            ShowSession session = ThreeSongs(out _);
            PanelView view = PanelView.Build(session, new DragView { Member = 0, From = DropPoint.AtPlace(0), Over = DropPoint.AtPlace(1) });
            Assert.Equal(Highlight.Allowed, view.Slots[1].Highlight);
            Assert.Equal(new[] { 1, 0 }, view.Slots.Take(2).Select(s => s.Member));
            Assert.Equal(new[] { SlotBadge.Substitute, SlotBadge.Substitute }, view.Slots.Take(2).Select(s => s.Badge));
            Assert.Equal("", view.Hint);
            Assert.Equal(new[] { 0, 1 }, session.Lineup.Take(2));  // nothing changed yet
        }

        [Fact]
        public void Dragging_OverARefusingSpot_SaysWhy()
        {
            ShowSession session = ThreeSongs(out _);
            PanelView view = PanelView.Build(session, new DragView { Member = 2, From = DropPoint.Bench, Over = DropPoint.AtHost(1) }, tab: FormationTab.Talk);
            Assert.Equal(Highlight.Refused, view.Hosts[1].Highlight);
            Assert.Equal("Talk breaks can start after the first song.", view.Hint);
        }

        [Fact]
        public void TheSuccessChance_IsASuccessOrACriticalSuccess_AndFollowsADragPreview()
        {
            Config config = new Config();
            double[] chances = Outcomes.Chances(config, 55);
            Assert.Equal(100 - chances[(int)Outcome.CriticalFailure] - chances[(int)Outcome.Failure], PanelView.SuccessChance(config, 55), 9);
            Assert.True(PanelView.SuccessChance(config, 40) < PanelView.SuccessChance(config, 70));
            Assert.Equal(-1, new SlotView().Success);

            ShowSession session = ThreeSongs(out _, rollSigma: 10);
            PanelView view = PanelView.Build(session);
            Assert.All(view.Slots, slot => Assert.InRange(slot.Success, 0.001, 99.999));
            // An idol from the bench held over the third place shows her chance there, not the idol's who's in it
            int held = view.Bench.Where(b => b.Available).Select(b => b.Member).Last();
            PanelView over = PanelView.Build(session, new DragView { Member = held, From = DropPoint.Bench, Over = DropPoint.AtPlace(2) });
            SongPreview preview = Engine.Preview(session.State, over.Slots.Select(s => s.Member).ToArray());
            Assert.Equal(held, over.Slots[2].Member);
            Assert.Equal(PanelView.SuccessChance(session.State.Config, preview.Slots[2].Expected), over.Slots[2].Success, 9);
            Assert.NotEqual(view.Slots[2].Success, over.Slots[2].Success);
        }

        [Fact]
        public void Dragging_OffAPlaceOverNothing_PreviewsTheBenchDrop()
        {
            ShowSession session = ThreeSongs(out _);
            Assert.StartsWith("Idol0 can't just leave", PanelView.Build(session, new DragView { Member = 0, From = DropPoint.AtPlace(0) }).Hint);

            session.Drop(3, DropPoint.Bench, DropPoint.AtPlace(0));
            PanelView view = PanelView.Build(session, new DragView { Member = 3, From = DropPoint.AtPlace(0) });
            Assert.Equal(0, view.Slots[0].Member);
            Assert.False(view.Slots[0].Dragged);
        }

        [Fact]
        public void Dragging_AHostOffTheRow_PreviewsHerLeaving()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            session.Drop(0, DropPoint.Bench, DropPoint.AtHost(1));
            PanelView view = PanelView.Build(session, new DragView { Member = 0, From = DropPoint.AtHost(1) }, tab: FormationTab.Talk);
            Assert.Equal(new[] { -1, -1, -1 }, view.Hosts.Select(h => h.Member));
            Assert.True(view.TalkOpen);
            Assert.Equal("Start Talk Break", view.StartLabel);  // still placed until she's dropped
        }

        [Theory]
        [InlineData(0, 0, false)]
        [InlineData(13, 596.1, false)]  // 7 rows fit the 600
        [InlineData(15, 681.3, true)]  // 8 rows don't: it scrolls
        public void TheBench_IsTwoColumnsFillingItsWidth_AndScrollsWhenTaller(int count, double height, bool scrolls)
        {
            float scale = PanelView.TwoColumnBench(count, 160, 600, 62, 66, out float contentHeight, out bool scrolling);
            Assert.Equal(160 / 124.0, scale, 3);
            Assert.Equal(height, contentHeight, 1);
            Assert.Equal(scrolls, scrolling);
        }

        [Fact]
        public void RowSizes_CountEachRowsPlaces_UpToTheHighestRow()
        {
            // The game's full triangle: rows of 1, 2, 3, 4 and 5
            int[] triangle = Enumerable.Range(0, 5).SelectMany(row => Enumerable.Repeat(row, row + 1)).ToArray();
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, PanelView.RowSizes(triangle));
            Assert.Empty(PanelView.RowSizes(new int[0]));
            Assert.Equal(new[] { 1, 0, 0, 0, 0, 2 }, PanelView.RowSizes(new[] { 5, 0, 5 }));
        }

        /// <summary>
        /// Main (Idol0-2), Sister (Idol3-4), a disbanded group (Idol5); song 1 is 0, 1, 2 and song 2 is 3, 4, 0.
        /// </summary>
        private static ShowSession TwoGroups()
        {
            data_girls.girls[] idols = Enumerable.Range(0, 6).Select(i => GameFixture.Idol("Idol" + i)).ToArray();
            // The game's order, with the sister group before the main one: the dropdown still lists the main group first
            GameFixture.Group(1, "Sister", false, idols[3], idols[4]);
            GameFixture.Group(0, "Main", false, idols[0], idols[1], idols[2]);
            GameFixture.Group(2, "Old", true, idols[5]);
            _concert concert = GameFixture.Concert(
                GameFixture.Single("S1", idols[0], idols[1], idols[2]),
                GameFixture.Single("S2", idols[3], idols[4], idols[0]));
            return GameFixture.Begin(concert, new Config { Seed = 3 });
        }

        [Fact]
        public void TheGroupDropdown_ListsEveryoneThenTheMainGroupThenTheOthersInTheCast()
        {
            ShowSession session = TwoGroups();
            Assert.Equal(new[] { 0, 0, 0, 1, 1, 2 }, session.State.Setup.Members.Select(m => m.Group));
            Assert.Equal(new[] { (0, "Main"), (1, "Sister") }, session.Show.Groups.Select(g => (g.Id, g.Title)));  // the disbanded one is left out
            Assert.Equal(new[] { "All Groups", "Main", "Sister" }, PanelView.GroupChoices(session.Show.Groups));
            Assert.Equal(new[] { -1, 0, 1, -1 }, new[] { 0, 1, 2, 3 }.Select(i => PanelView.GroupAt(session.Show.Groups, i)));
            Assert.Empty(PanelView.GroupChoices(session.Show.Groups.Take(1).ToList()));  // one group: nothing to filter
        }

        [Fact]
        public void TheGroupFilter_KeepsOnlyThatGroupOnTheBench_OnEitherTab()
        {
            ShowSession session = TwoGroups();
            Assert.Equal(new[] { 0, 1, 2 }, session.Lineup);
            Assert.Equal(new[] { 3, 4, 5 }, PanelView.Build(session).Bench.Select(b => b.Member));
            Assert.Equal(new[] { 3, 4 }, PanelView.Build(session, group: 1).Bench.Select(b => b.Member));
            Assert.Empty(PanelView.Build(session, group: 0).Bench);  // the main group is all on stage
            Assert.Equal(new[] { 0, 1, 2 }, PanelView.Build(session, tab: FormationTab.Talk, group: 0).Bench.Select(b => b.Member));
            // Only the bench is filtered
            Assert.Equal(PanelView.Build(session).Slots.Select(s => s.Member), PanelView.Build(session, group: 1).Slots.Select(s => s.Member));
        }

        private static readonly PanelView.FormationSpace Space = new()
        {
            ReadoutsRight = 520, ReadoutsBottom = 380, ButtonsRight = 380, ButtonsTop = 400, BenchLeft = 815,
            Top = 160, Bottom = 624, Margin = 35, TabsHalf = 105,
        };

        [Fact]
        public void TheFormation_GrowsToFillTheSpace_CentredBetweenTheReadoutsAndTheBench()
        {
            PanelView.FormationFrame frame = PanelView.FitFormation(new[] { 3, 2, 3 }, Space);
            Assert.Equal(1.4f, frame.Scale);
            Assert.Equal(667.5f, frame.CenterX, 3);  // the middle of 520 to 815
        }

        [Theory]
        [InlineData(new[] { 3, 2, 3, 4, 5 })]
        [InlineData(new[] { 3, 2 })]
        [InlineData(new[] { 3, 2, 3, 4 })]
        public void EveryRow_StaysClearOfTheReadoutsButtonsAndBench_AtTheLargestScaleThatFits(int[] widths)
        {
            static bool Fits(int[] widths, float scale, float center, PanelView.FormationSpace space)
            {
                if (center - space.TabsHalf < space.ReadoutsRight - 1e-3 || center + space.TabsHalf > space.BenchLeft + 1e-3)
                    return false;
                for (int row = 0; row < widths.Length; row++)
                {
                    float top = space.Top + row * PanelView.RowHeight * scale, bottom = top + PanelView.PlaceHeight * scale;
                    float half = ((widths[row] - 1) / 2f * PanelView.CellWidth + PanelView.PlaceWidth / 2f) * scale;
                    float left = top < space.ReadoutsBottom ? space.ReadoutsRight : bottom > space.ButtonsTop ? space.ButtonsRight : space.Margin;
                    if (center - half < left - 1e-3 || center + half > space.BenchLeft + 1e-3 || bottom > space.Bottom + 1e-3)
                        return false;
                }
                return true;
            }

            PanelView.FormationFrame frame = PanelView.FitFormation(widths, Space);
            Assert.True(Fits(widths, frame.Scale, frame.CenterX, Space), $"scale {frame.Scale} at {frame.CenterX}");
            // A step larger doesn't fit anywhere between the readouts and the bench
            if (frame.Scale < PanelView.MaxScale)
                Assert.DoesNotContain(Enumerable.Range(0, 300).Select(x => Space.ReadoutsRight + x), center => Fits(widths, frame.Scale + 0.05f, center, Space));
        }

        [Fact]
        public void LanguageFile_HoldsEveryTextInEnglish()
        {
            string path = Path.Combine(ConfigFileTests.RepoRoot(), "mods", "Concert Hype", "assets", "JSON", "Constants", "constants.json");
            JSONNode constants = mainScript.ProcessInboundData(File.ReadAllText(path));
            Dictionary<string, string> shipped = Enumerable.Range(0, constants.Count).ToDictionary(i => constants[i]["id"].Value, i => constants[i]["text"].Value);
            Assert.Equal(Texts.English.OrderBy(p => p.Key), shipped.OrderBy(p => p.Key));
        }

        [Fact]
        public void Texts_UseTheLanguageEntry_WhenTheGameHasOne()
        {
            Language.Data["CONCERTHYPE__RESET_LINEUP"] = "Aufstellung zurücksetzen";
            try
            {
                Assert.Equal("Aufstellung zurücksetzen", Texts.Get("CONCERTHYPE__RESET_LINEUP"));
            }
            finally
            {
                Language.Data.Remove("CONCERTHYPE__RESET_LINEUP");
            }
            Assert.Equal("Reset Lineup", Texts.Get("CONCERTHYPE__RESET_LINEUP"));
            Language.Data["CONCERTHYPE__SONGS"] = "{0} {1} broken";
            try
            {
                Assert.Equal("3 songs", Texts.Format("CONCERTHYPE__SONGS", 3));
            }
            finally
            {
                Language.Data.Remove("CONCERTHYPE__SONGS");
            }
        }
    }
}
