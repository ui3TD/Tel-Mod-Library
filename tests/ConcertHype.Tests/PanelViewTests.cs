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
        private static ShowSession ThreeSongs(out _concert concert)
        {
            data_girls.girls[] idols = Enumerable.Range(0, 6).Select(i => GameFixture.Idol("Idol" + i, dance: 50 + 5 * i, vocal: 55)).ToArray();
            idols[2].status = data_girls._status.injured;
            concert = GameFixture.Concert(
                GameFixture.Single("S1", idols[0], idols[1], idols[2]),
                GameFixture.Single("S2", idols[3], idols[4], idols[5]),
                GameFixture.Single("S3", idols[0], idols[3], idols[4]));
            concert.Hype = 50;
            return GameFixture.Begin(concert, new Config { Seed = 3, RollSigma = 0, PBase = 0, PRoll = 0, PTired = 0 });
        }

        [Fact]
        public void Slots_ShowRowsBadgesAndExpectedRolls()
        {
            ShowSession session = ThreeSongs(out _);
            Assert.True(session.Drop(3, DropPoint.Bench, DropPoint.AtPlace(0)).Changed);
            PanelView view = PanelView.Build(session);

            Assert.Equal(new[] { 0, 1, 1 }, view.Slots.Select(s => s.Row));
            Assert.Equal(new[] { SlotBadge.Substitute, SlotBadge.Original, SlotBadge.Substitute }, view.Slots.Select(s => s.Badge));
            SongPreview preview = session.Preview();
            Assert.Equal(Math.Round(preview.Slots[1].Expected).ToString(), view.Slots[1].Roll);
            Assert.All(view.Slots, s => Assert.Equal(Highlight.None, s.Highlight));
            Assert.Equal("⇄", PanelView.BadgeText(SlotBadge.Substitute));
            Assert.Equal("★", PanelView.BadgeText(SlotBadge.Original));
        }

        [Fact]
        public void Lines_StateHypeBandExpectationAndHint()
        {
            ShowSession session = ThreeSongs(out _);
            PanelView view = PanelView.Build(session);
            Assert.StartsWith("Hype 50% · a changed lineup surprises the crowd", view.Hype);
            Assert.StartsWith("Changed lineup ", view.Expected);
            Assert.Contains("expected hype ", view.Expected);
            Assert.Equal("", view.Last);
            Assert.Equal("", view.Hint);  // no standing instructions: only why a drop was refused
            Assert.Equal("M can't perform right now.", PanelView.Build(session, message: "M can't perform right now.").Hint);

            session.State.Hype = 150;
            Assert.Equal("Hype 150% · the crowd wants the hits", PanelView.Build(session).Hype);
            session.State.Hype = 95;
            Assert.Equal("Hype 95% · the crowd is close to 100%", PanelView.Build(session).Hype);
        }

        [Fact]
        public void TheTalkBreakPlace_OpensAfterTheFirstSong_AndAHostChangesStart()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            PanelView view = PanelView.Build(session);
            Assert.Equal("Talk Break (2 left)", view.TalkTitle);
            Assert.False(view.TalkOpen);
            Assert.False(view.TalkShown);  // no talk break place before the first song
            Assert.Equal(-1, view.Host);
            Assert.Null(view.StartLabel);

            new FakeStage(concert).PlaySong(session);
            Assert.True(PanelView.Build(session).TalkOpen);
            Assert.True(PanelView.Build(session).TalkShown);

            session.Drop(1, DropPoint.Bench, DropPoint.Host);
            view = PanelView.Build(session);
            Assert.Equal(1, view.Host);
            Assert.Equal("Start Talk Break", view.StartLabel);
        }

        [Fact]
        public void LastLine_ReportsTheSongsChange_AccidentsAndTalkBreaks()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            session.State.Config.PBase = 1;
            session.State.Config.PMax = 1;
            session.State.Config.AbsenceMin = session.State.Config.AbsenceMax = 2;
            FakeStage stage = new(concert);
            stage.PlaySong(session);
            string last = PanelView.Build(session).Last;
            Assert.StartsWith("Last song: " + PanelView.Signed(session.LastResult.Change) + " hype; ", last);
            Assert.Equal("Last song: " + PanelView.Signed(session.LastResult.Change) + " hype; 3 hurt", last);  // names show on the bench
            session.LastResult.Exhausted.Add(1);
            Assert.EndsWith("; 3 hurt, 1 exhausted", PanelView.Build(session).Last);
            session.LastResult.Exhausted.Clear();

            session.State.Config.PBase = 0;
            session.Drop(2, DropPoint.AtPlace(0), DropPoint.Host);
            stage.PlaySong(session);
            Assert.StartsWith("Talk break by Idol3: hype ", PanelView.Build(session).Last);
        }

        [Fact]
        public void TheBench_IsTheCastNotOnStageOrHosting_WithWhoCantPerform()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            Assert.Equal(new[] { 0, 1, 4 }, session.Lineup);  // Idol5 (4) fills the injured idol's place
            Assert.Equal(new[] { 2, 3 }, PanelView.Build(session).Bench.Select(b => b.Member));

            new FakeStage(concert).PlaySong(session);
            session.Drop(0, DropPoint.Bench, DropPoint.Host);
            session.State.Absence[1] = 2;
            PanelView view = PanelView.Build(session);
            Assert.Equal(new[] { 1 }, view.Bench.Select(b => b.Member));
            Assert.Equal(IconKind.Hurt, view.Bench[0].Icon);
            Assert.Equal("2", view.Bench[0].Count);
            Assert.Equal("Hurt: out for 2 songs. A talk break counts as one song.", view.Bench[0].Tip);
            Assert.False(view.Bench[0].Available);
            session.State.Absence[1] = 1;
            Assert.Equal("Hurt: out for 1 song. A talk break counts as one song.", PanelView.Build(session).Bench[0].Tip);

            session.State.Absence[1] = 0;
            session.State.Exhausted[1] = true;
            session.State.Energy[1] = 10;
            view = PanelView.Build(session);
            Assert.Equal(IconKind.Resting, view.Bench[0].Icon);
            Assert.Equal("", view.Bench[0].Count);
            Assert.Equal($"Exhausted: resting until her energy is back to {session.State.Config.ReturnThreshold}%.", view.Bench[0].Tip);
            Assert.Equal(10, view.Bench[0].Energy);

            session.State.Exhausted[1] = false;
            Assert.Equal(IconKind.None, PanelView.Build(session).Bench[0].Icon);
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
        public void WithATalkBreakFirst_TheFormationWaits_AndTheHostsPlaceShowsEmpty()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            PanelView before = PanelView.Build(session);
            Assert.False(before.Faded);
            Assert.Equal("", before.FormationNote);
            Assert.StartsWith("Changed lineup ", before.Expected);

            // Song 2 is 2, 3, 4: the center hosts, and stays in the lineup for the song
            session.Drop(2, DropPoint.AtPlace(0), DropPoint.Host);
            PanelView view = PanelView.Build(session);
            string pull = Math.Round(Engine.McHype(session.State, 2)).ToString();
            Assert.True(view.Faded);
            Assert.Equal("This lineup plays after the talk break.", view.FormationNote);
            Assert.Equal($"Talk break first: hype goes to about {pull}%.", view.Expected);
            Assert.True(view.Slots[0].Hosting);
            Assert.Equal(2, view.Slots[0].Member);
            Assert.Equal(IconKind.Hosting, view.Slots[0].Icon);
            Assert.Equal("", view.Slots[0].Roll);
            Assert.Equal("Idol3 hosts the talk break first and is back here for the song.", view.Slots[0].Tip);
            Assert.False(view.Slots[1].Hosting);
            Assert.Equal(new[] { 2, 3, 4 }, session.Lineup);
        }

        [Fact]
        public void DraggingAnIdolOverTheHostPlace_PreviewsTheWaitingFormation()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            PanelView view = PanelView.Build(session, new DragView { Member = 3, From = DropPoint.AtPlace(1), Over = DropPoint.Host });
            Assert.True(view.Faded);
            Assert.True(view.Slots[1].Hosting);
            Assert.Equal(-1, session.Host);

            view = PanelView.Build(session, new DragView { Member = 0, From = DropPoint.Bench, Over = DropPoint.Host });
            Assert.True(view.Faded);
            Assert.DoesNotContain(view.Slots, s => s.Hosting);
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
            Assert.NotEqual(before.Expected, view.Expected);
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
            PanelView view = PanelView.Build(session, new DragView { Member = 2, From = DropPoint.Bench, Over = DropPoint.Host });
            Assert.Equal(Highlight.Refused, view.HostHighlight);
            Assert.Equal("Talk breaks can start after the first song.", view.Hint);
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
        public void Dragging_TheHostAway_PreviewsTheCancel()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            session.Drop(0, DropPoint.Bench, DropPoint.Host);
            PanelView view = PanelView.Build(session, new DragView { Member = 0, From = DropPoint.Host });
            Assert.Equal(-1, view.Host);
            Assert.True(view.TalkOpen);
            Assert.Equal("Start Talk Break", view.StartLabel);  // still placed until she's dropped
        }

        [Theory]
        [InlineData(0, 1, 1f)]
        [InlineData(7, 1, 1f)]
        [InlineData(30, 4, 0.81)]
        [InlineData(60, 4, 0.49)]
        public void TheBenchGrid_PicksTheColumnsThatGiveTheLargestCells(int count, int columns, double scale)
        {
            float fit = PanelView.BenchGrid(count, 201, 487, 62, 66, 4, out int fitColumns);
            Assert.Equal(columns, fitColumns);
            Assert.Equal(scale, fit, 2);
            int rows = (count + fitColumns - 1) / fitColumns;
            Assert.True(count == 0 || (fitColumns * 62 * fit <= 201.01 && rows * 66 * fit <= 487.01));
        }

        [Fact]
        public void ScoreLine_StatesTheBlend()
        {
            Assert.Equal("Show score: 132% (average 120%, best 165%, finale 140%)",
                PanelView.ScoreLine(new Score { Total = 132.4, Average = 120, Peak = 165, Finale = 140 }));
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
