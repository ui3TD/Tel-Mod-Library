using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using _concert = SEvent_Concerts._concert;

namespace ConcertHype.Tests
{
    /// <summary>
    /// The next-song screen's left block (Venue, Performance, Production and their total, each a bar with its parts in
    /// the tip) and what the mod draws on the game's hype bar.
    /// </summary>
    public class BarRowTests : IDisposable
    {
        public BarRowTests() => GameFixture.Reset();

        public void Dispose() => GameFixture.Reset();

        /// <summary>
        /// Cast: Idol0, Idol1, Idol3, Idol4, Idol5 (Idol2 is injured, so song 1's third place must be filled). Every
        /// single is Pop, Love and Cute, so from the second song on each shares all three with the one before.
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

        private static double Number(string value) => double.Parse(value.TrimEnd('%'), CultureInfo.InvariantCulture);

        [Fact]
        public void RoundedParts_AddUpToTheRoundedTotal()
        {
            Assert.Equal(new double[] { 0, 1, 0 }, PanelView.RoundedParts(0, 0.4, 0.4, 0.4));
            Assert.Equal(new double[] { 0 }, PanelView.RoundedParts(0.6, 0.6));
            Random random = new(4);
            for (int i = 0; i < 2000; i++)
            {
                double start = 40 * (random.NextDouble() - 0.5);
                double[] parts = Enumerable.Range(0, 1 + random.Next(4)).Select(_ => 30 * (random.NextDouble() - 0.5)).ToArray();
                double[] rounded = PanelView.RoundedParts(start, parts);
                Assert.All(rounded, r => Assert.Equal(Math.Round(r), r));
                Assert.Equal(Math.Round(start + parts.Sum()) - Math.Round(start), rounded.Sum(), 9);
                Assert.All(rounded.Zip(parts, (r, p) => Math.Abs(r - p)), off => Assert.True(off <= 1 + 1e-9));
            }
        }

        [Fact]
        public void BarScale_IsTheSmallestRoundStepThatHoldsEveryAmount_AndOnlyGrows()
        {
            Assert.Equal(5, PanelView.BarScale(double.NaN, new double[0]));
            Assert.Equal(5, PanelView.BarScale(double.NaN, new[] { 3.0, -4.9 }));
            Assert.Equal(15, PanelView.BarScale(double.NaN, new[] { 12.0, -3, double.NaN }));
            Assert.Equal(75, PanelView.BarScale(double.NaN, new[] { -51.0 }));
            Assert.Equal(200, PanelView.BarScale(double.NaN, new[] { 250.0 }));
            Assert.Equal(20, PanelView.BarScale(20, new[] { 3.0 }));
            Assert.Equal(30, PanelView.BarScale(20, new[] { 22.0 }));
        }

        [Fact]
        public void BarSpan_RunsFromTheMiddle_RightForGainsAndLeftForLosses_CutOffAtTheEnds()
        {
            void Span(double from, double to, float left, float right)
            {
                PanelView.Stretch stretch = PanelView.BarSpan(from, to, 20);
                Assert.Equal((left, right), (stretch.From, stretch.To));
            }
            Span(0, 10, 0.5f, 0.75f);
            Span(0, -10, 0.25f, 0.5f);
            Span(0, 50, 0.5f, 1f);
            Span(-45, 0, 0f, 0.5f);
            Span(-5, 15, 0.375f, 0.875f);
            Assert.Equal((0.5f, 1f, 0f), (PanelView.HypeShare(100), PanelView.HypeShare(250), PanelView.HypeShare(-5)));
        }

        [Fact]
        public void TheSongsRows_AreTheHypeEachConsiderationAdds_AndAddUpToTheTotal()
        {
            ShowSession session = ThreeSongs(out _);
            PanelView view = PanelView.Build(session);
            SongPreview preview = session.Preview();

            Assert.Equal(new[] { "Venue:", "Performance:", "Production:", "Expected Hype:" }, view.Rows.Select(r => r.Title));
            Assert.Equal(new[] { preview.Parts.Venue, preview.Parts.Performance, preview.Parts.Production, preview.Change }, view.Rows.Select(r => r.Amount));
            Assert.Equal(Number(view.Total.Value), view.Rows.Take(3).Sum(r => Number(r.Value)));
            Assert.Equal(Math.Round(preview.Change), Number(view.Total.Value));
            foreach (BarRow row in view.Rows)
            {
                double shown = Number(row.Value);
                Assert.Equal(shown > 0 ? Tone.Good : shown < 0 ? Tone.Bad : Tone.Plain, row.Tone);
                Assert.Equal(row == view.Total, row.HasRange);
            }
            string low = PanelView.Signed(preview.Low) + "%", high = PanelView.Signed(preview.High) + "%";
            Assert.Equal((preview.Low, preview.High), (view.Total.Low, view.Total.High));
            Assert.Equal($"likely {low} to {high}", view.Total.Caption);
            Assert.Equal($"Likely {low} to {high}, depending on how the idols perform.", view.Total.Tip);  // nothing played yet
            Assert.Equal("0%", view.Venue.Value.TrimStart('+'));  // the fixture's venue is full
        }

        [Fact]
        public void TheRows_FollowADragPreview()
        {
            ShowSession session = ThreeSongs(out _);
            PanelView before = PanelView.Build(session);
            // A bench idol held over the center, who loses the original's senbatsu bonus there
            int bench = Enumerable.Range(0, session.State.Setup.Members.Count).First(m => !session.Lineup.Contains(m) && session.State.IsAvailable(m));
            DragView drag = new() { Member = bench, From = DropPoint.Bench, Over = DropPoint.AtPlace(0) };
            PanelView over = PanelView.Build(session, drag);
            int[] hovered = DropRules.Drop(session.State, session.Lineup, session.Hosts, bench, DropPoint.Bench, DropPoint.AtPlace(0)).Lineup;
            Assert.Equal(Engine.Preview(session.State, hovered).Parts.Performance, over.Performance.Amount, 9);
            Assert.NotEqual(before.Performance.Amount, over.Performance.Amount);
        }

        [Fact]
        public void VenueTip_NamesTheSeatsFilled_AndWhenItAddsNothing()
        {
            ShowSetup setup = new() { Seats = 10000, CapacityFilled = 0.92 };
            Assert.Equal("9,200 of 10,000 seats filled (92%).", PanelView.VenueTip(setup, gain: true));
            Assert.Equal("9,200 of 10,000 seats filled (92%).\nAdds nothing to a song that loses hype.", PanelView.VenueTip(setup, gain: false));
            setup.Seats = 0;
            Assert.Equal("The venue is 92% full.", PanelView.VenueTip(setup, gain: true));

            ShowSession session = ThreeSongs(out _);
            Assert.Equal(1500, session.State.Setup.Seats);  // the fixture's concert hall
            Assert.StartsWith("1,500 of 1,500 seats filled (100%).", PanelView.Build(session).Venue.Tip);
        }

        [Fact]
        public void PerformanceTip_GivesEachRowsSuccessChance_TheEmptyPlaces_AndWhatCountsMost()
        {
            ShowSession session = ThreeSongs(out _);
            ShowState state = session.State;
            SongPreview preview = session.Preview();
            string Chance(int j) => Math.Round(PanelView.SuccessChance(state.Config, preview.Slots[j].Expected)).ToString(CultureInfo.InvariantCulture);
            double second = (PanelView.SuccessChance(state.Config, preview.Slots[1].Expected) + PanelView.SuccessChance(state.Config, preview.Slots[2].Expected)) / 2;
            Assert.Equal($"Center: {Chance(0)}% success\nRow 2: {Math.Round(second)}% success\nFront rows and idols with more fans count most.",
                PanelView.Build(session).Performance.Tip);

            int[] gap = { session.Lineup[0], -1, -1 };
            SongPreview gapped = Engine.Preview(state, gap);
            Assert.Equal($"Center: {Chance(0)}% success\n2 empty places\nFront rows and idols with more fans count most.", PanelView.PerformanceTip(state, gap, gapped));
            gap[2] = session.Lineup[2];
            Assert.Contains("\n1 empty place\n", PanelView.PerformanceTip(state, gap, Engine.Preview(state, gap)));
        }

        [Fact]
        public void ProductionTip_AddsUpToItsRow_AndNamesTheFlowFromTheSecondSongOn()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            PanelView first = PanelView.Build(session);
            string[] lines = first.Production.Tip.Split('\n');
            Assert.Equal(4, lines.Length);   // no flow for the first song
            Assert.StartsWith("Production quality 50%: ", lines[0]);
            Assert.StartsWith("Song's appeal to these idols' fans: ", lines[1]);
            Assert.StartsWith($"Changed lineup {Math.Round(100 * session.Preview().Novelty)}%: ", lines[2]);
            Assert.Equal("Hype 50%: a changed lineup surprises the crowd.", lines[3]);
            Assert.Equal(Number(first.Production.Value), lines.Take(3).Sum(line => Number(line.Substring(line.LastIndexOf(' ') + 1))));

            new FakeStage(concert).PlaySong(session);
            PanelView second = PanelView.Build(session);
            lines = second.Production.Tip.Split('\n');
            SongPreview preview = session.Preview();
            Assert.Equal(SongMatch.Genre | SongMatch.Lyrics | SongMatch.Choreography, preview.Steps.Matches);
            string flow = lines.Single(line => line.StartsWith("Same "));
            Assert.Equal("Same genre, lyrics and choreography as the last song: " + PanelView.Signed(Number(flow.Substring(flow.LastIndexOf(' ') + 1))) + "%", flow);
            Assert.Equal(Number(second.Production.Value),
                lines.Where(line => line.EndsWith("%") && !line.StartsWith("Hype ")).Sum(line => Number(line.Substring(line.LastIndexOf(' ') + 1))));
            Assert.Equal(preview.Steps.Gain, !second.Production.Tip.Contains("Quality and appeal only add to songs that raise the hype."));
        }

        [Fact]
        public void OnALoss_TheVenueAndProductionTipsSaySo()
        {
            ShowSession session = ThreeSongs(out _);
            session.State.Config.Par = 99;   // nobody reaches it
            SongPreview preview = session.Preview();
            Assert.False(preview.Steps.Gain);
            PanelView view = PanelView.Build(session);
            Assert.EndsWith("\nAdds nothing to a song that loses hype.", view.Venue.Tip);
            Assert.EndsWith("\nQuality and appeal only add to songs that raise the hype.", view.Production.Tip);
            Assert.Equal(0, view.Venue.Amount);
            Assert.True(view.Performance.Amount < 0);
            Assert.Equal(Tone.Bad, view.Total.Tone);
        }

        [Theory]
        [InlineData(SongMatch.Genre, "genre")]
        [InlineData(SongMatch.Lyrics | SongMatch.Choreography, "lyrics and choreography")]
        [InlineData(SongMatch.Genre | SongMatch.Lyrics | SongMatch.Choreography, "genre, lyrics and choreography")]
        [InlineData(SongMatch.None, "")]
        public void MatchList_ReadsAsAList(SongMatch matches, string list) => Assert.Equal(list, PanelView.MatchList(matches));

        [Fact]
        public void OnTheTalkTab_OnlyTheTalkBreaksTotalShows_WhileSomeoneHosts()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            new FakeStage(concert).PlaySong(session);
            PanelView talk = PanelView.Build(session, tab: FormationTab.Talk);
            Assert.All(talk.Rows, row => Assert.False(row.Shown));

            session.Drop(2, DropPoint.Bench, DropPoint.AtHost(0));
            talk = PanelView.Build(session, tab: FormationTab.Talk);
            Assert.All(new[] { talk.Venue, talk.Performance, talk.Production }, row => Assert.False(row.Shown));
            double after = Engine.McHype(session.State, 2), change = after - session.State.Hype;
            Assert.Equal("After Talk Break:", talk.Total.Title);
            Assert.Equal(PanelView.Signed(change) + "%", talk.Total.Value);
            Assert.Equal(change, talk.Total.Amount, 9);
            Assert.False(talk.Total.HasRange);
            string talkShare = Math.Round(100 * Engine.Talk(session.State, new[] { 2 })).ToString(CultureInfo.InvariantCulture);
            Assert.Equal($"Hosts' talk {talkShare}%: about {Math.Round(after)}% hype after.\nLast song: {PanelView.Signed(session.LastResult.Change)}%.", talk.Total.Tip);

            // The song's tab still shows the song's rows
            Assert.All(PanelView.Build(session).Rows, row => Assert.True(row.Shown));
        }

        [Fact]
        public void TheHypeBar_PreviewsTheTabsNextStep_AndMarksWhereTheHypeWasBeforeTheLastOne()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            FakeStage stage = new(concert);
            HypeBarView bar = PanelView.Build(session).Bar;
            double hype = session.State.Hype;
            Assert.Equal(hype, bar.Hype);
            Assert.Equal(hype + session.Preview().Change, bar.Target, 9);
            Assert.False(bar.HasBefore);
            Assert.Equal($"Hype {Math.Round(hype)}%. After this song: about {Math.Round(bar.Target)}%.", bar.Tip);

            stage.PlaySong(session);
            hype = session.State.Hype;
            bar = PanelView.Build(session).Bar;
            Assert.Equal(session.LastResult.HypeBefore, bar.Before);
            Assert.EndsWith($"\nLast song: {PanelView.Signed(session.LastResult.Change)}%.", bar.Tip);

            bar = PanelView.Build(session, tab: FormationTab.Talk).Bar;
            Assert.False(bar.HasTarget);
            Assert.StartsWith($"Hype {Math.Round(hype)}%.\nLast song: ", bar.Tip);

            session.Drop(2, DropPoint.Bench, DropPoint.AtHost(0));
            bar = PanelView.Build(session, tab: FormationTab.Talk).Bar;
            Assert.Equal(Engine.McHype(session.State, 2), bar.Target, 9);
            Assert.StartsWith($"Hype {Math.Round(hype)}%. After the talk break: about {Math.Round(bar.Target)}%.", bar.Tip);

            stage.PlaySong(session);  // the talk break
            bar = PanelView.Build(session).Bar;
            Assert.Equal(session.LastMc.HypeBefore, bar.Before);
            Assert.EndsWith($"\nLast talk break: {PanelView.Signed(session.LastMc.HypeAfter - session.LastMc.HypeBefore)}%.", bar.Tip);
        }

        [Fact]
        public void AfterTheLastSong_NothingIsPreviewed()
        {
            ShowSession session = ThreeSongs(out _concert concert);
            FakeStage stage = new(concert);
            while (!session.State.Finished)
                stage.PlaySong(session);
            PanelView view = PanelView.Build(session);
            Assert.All(view.Rows, row => Assert.False(row.Shown));
            Assert.False(view.Bar.HasTarget);
        }
    }
}
