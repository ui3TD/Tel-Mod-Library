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
        public void Slots_ShowRowsBadgesExpectedRollsAndTheSelection()
        {
            ShowSession session = ThreeSongs(out _);
            Assert.True(session.Assign(0, 3));
            PanelView view = PanelView.Build(session, selectedSlot: 1, pickingHost: false);

            Assert.Equal(new[] { 0, 1, 1 }, view.Slots.Select(s => s.Row));
            Assert.Equal(new[] { SlotBadge.Substitute, SlotBadge.Original, SlotBadge.Fill }, view.Slots.Select(s => s.Badge));
            SongPreview preview = session.Preview();
            Assert.Equal(Math.Round(preview.Slots[1].Expected).ToString(), view.Slots[1].Roll);
            Assert.Equal(new[] { false, true, false }, view.Slots.Select(s => s.Selected));
            Assert.Equal("\u21C4", PanelView.BadgeText(SlotBadge.Substitute));
            Assert.Equal("\u2605", PanelView.BadgeText(SlotBadge.Original));
            Assert.Equal("!", PanelView.BadgeText(SlotBadge.Fill));
            Assert.Equal("\u2716", PanelView.BadgeText(SlotBadge.Empty));
        }

        [Fact]
        public void Lines_StateHypeBandExpectationAndHint()
        {
            ShowSession session = ThreeSongs(out _);
            PanelView view = PanelView.Build(session, -1, false);
            Assert.StartsWith("Hype 50% · a changed lineup surprises the crowd", view.Hype);
            Assert.StartsWith("Changed lineup ", view.Expected);
            Assert.Contains("expected hype ", view.Expected);
            Assert.Equal("", view.Last);
            Assert.StartsWith("Click a place", view.Hint);
            Assert.StartsWith("Now click an idol", PanelView.Build(session, 0, false).Hint);
            Assert.StartsWith("Click an idol on the right to host", PanelView.Build(session, -1, true).Hint);

            session.State.Hype = 150;
            Assert.Equal("Hype 150% · the crowd wants the hits", PanelView.Build(session, -1, false).Hype);
            session.State.Hype = 95;
            Assert.Equal("Hype 95% · the crowd is close to 100%", PanelView.Build(session, -1, false).Hype);
        }

        [Fact]
        public void TalkBreak_ShowsChargesLeft_AndIsOffBeforeTheFirstSong()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            PanelView view = PanelView.Build(session, -1, false);
            Assert.Equal("Talk Break (2 left)", view.TalkBreak);
            Assert.False(view.TalkBreakEnabled);

            new FakeStage(concert).PlaySong(session);
            Assert.True(PanelView.Build(session, -1, false).TalkBreakEnabled);
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
            string last = PanelView.Build(session, -1, false).Last;
            Assert.StartsWith("Last song: " + PanelView.Signed(session.LastResult.Change) + " hype; ", last);
            Assert.Contains("Idol0 is hurt and out for 2 songs", last);

            session.State.Config.PBase = 0;
            session.CallMc(2, stage);
            Assert.StartsWith("Talk break by Idol3: hype ", PanelView.Build(session, -1, false).Last);
        }

        [Fact]
        public void Cast_ShowsEnergyAndWhoCantPerform()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            session.State.Absence[1] = 2;
            session.State.Exhausted[3] = true;
            session.State.Energy[3] = 10;
            PanelView view = PanelView.Build(session, -1, false);
            Assert.Equal("\u271A2", view.Cast[1].State);
            Assert.False(view.Cast[1].Available);
            Assert.Equal("Zz", view.Cast[3].State);
            Assert.Equal(10, view.Cast[3].Energy);
            Assert.True(view.Cast[0].OnStage);
            Assert.Equal("", view.Cast[0].State);
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
