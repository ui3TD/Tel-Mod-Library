using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// Election results, posted once the winner is revealed: the places with votes and how each idol moved,
    /// the biggest moves below the listed places, and the election before.
    /// </summary>
    public class ElectionTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string H = Moment.HistorySection;

        private static readonly Dictionary<string, string> Texts = new()
        {
            ["SEVENT__ELECTION"] = "Election",
            ["SSK__NATIONAL_TV"] = "National TV",
            ["CONCERT__CLUB"] = "Club",
        };

        public ElectionTests()
        {
            TestEnv.Reset();
            foreach (KeyValuePair<string, string> text in Texts)
            {
                Language.Data[text.Key] = text.Value;
            }
        }

        public void Dispose()
        {
            foreach (string key in Texts.Keys)
            {
                Language.Data.Remove(key);
            }
        }

        /// <summary>A finished election, numbered as the game numbers it when it finishes.</summary>
        private static SEvent_SSK._SSK Numbered(int count, DateTime finished, params data_girls.girls[] ranked)
        {
            SEvent_SSK._SSK election = Election(finished, ranked.Length, ranked);
            election.Count = count;
            return election;
        }

        [Fact]
        public void FinishedElection_PostsOnce()
        {
            SEvent_SSK._SSK election = Numbered(1, Today, Idol("Rin"));
            Assert.True(SEvent_SSK_SSK_Finish.ShouldPost(election, true));
            SEvent_SSK_SSK_Finish.Postfix(election, true);
            Assert.False(SEvent_SSK_SSK_Finish.ShouldPost(election, true));
        }

        /// <summary>
        /// A skipped Finish, or an election the game didn't finish: no posts.
        /// </summary>
        [Fact]
        public void UnfinishedElections_DoNotPost()
        {
            SEvent_SSK._SSK election = Numbered(1, Today, Idol("Rin"));
            Assert.False(SEvent_SSK_SSK_Finish.ShouldPost(election, false));
            Assert.False(SEvent_SSK_SSK_Finish.ShouldPost(null, true));
            election.Status = SEvent_Tour.tour._status.normal;
            Assert.False(SEvent_SSK_SSK_Finish.ShouldPost(election, true));
        }

        /// <summary>
        /// Every place with its votes and how the idol moved since the last election. The biggest drop below
        /// the places is an idol the game no longer ranks; a graduate isn't one, as she didn't take part.
        /// </summary>
        [Fact]
        public void Election_GivesPlaces_Votes_AndMovement()
        {
            data_girls.girls rin = Idol("Rin"), aoi = Idol("Aoi"), yui = Idol("Yui"), mei = Idol("Mei"), kaho = Idol("Kaho"), fresh = Idol("Nana");
            Numbered(1, Today.AddMonths(-6), rin, aoi, yui, mei, kaho);
            yui.Graduated(Today.AddMonths(-2));
            SEvent_SSK._SSK election = Numbered(2, Today, aoi, rin, fresh, kaho);
            election.Broadcast = SEvent_SSK._broadcast.nationalTV;
            election.Single = Single("Vote For Me", Today.AddDays(-14), 50000);
            election.Concert = new SEvent_Concerts._concert { Venue = SEvent_Concerts._venue.club };

            Moment moment = MomentContext.Election(election);
            Assert.Equal("the results of the agency's idol election were announced", moment.Kind);
            Assert.Equal("Election #2", moment.Label);
            Assert.Equal("Election #2", moment.Fact(F, "Election"));
            Assert.Equal("National TV", moment.Fact(F, "Broadcast on"));
            Assert.Equal("\"Vote For Me\"", moment.Fact(F, "Voting single"));
            Assert.Equal("Club", moment.Fact(F, "Held after the concert at"));
            Assert.Equal("4", moment.Fact(F, "Idols ranked"));
            Assert.Equal("\n- 1st: Aoi, 4000 votes (up from 2nd)"
                + "\n- 2nd: Rin, 3000 votes (down from 1st)"
                + "\n- 3rd: Nana, 2000 votes (not ranked last time)"
                + "\n- 4th: Kaho, 1000 votes (up from 5th)", moment.Fact(F, "Results"));
            Assert.Equal("Mei, no longer ranked (was 4th)", moment.Fact(F, "Biggest drop outside the top 10"));
            // The game keeps only 10 places, so no idol below them can be seen climbing
            Assert.Null(moment.Fact(F, "Biggest rise outside the top 10"));
            Assert.Equal("Election #1, won by Rin, 6 months ago", moment.Fact(H, "Previous election"));
            Assert.Equal("2", moment.Fact(H, "Elections so far, this one included"));
        }

        [Fact]
        public void FirstElection_HasNoMovement()
        {
            Moment moment = MomentContext.Election(Numbered(1, Today, Idol("Rin"), Idol("Aoi")));
            Assert.Equal("\n- 1st: Rin, 2000 votes\n- 2nd: Aoi, 1000 votes", moment.Fact(F, "Results"));
            Assert.Null(moment.Fact(F, "Biggest drop outside the top 10"));
            Assert.Null(moment.Fact(F, "Biggest rise outside the top 10"));
            Assert.Null(moment.Fact(H, "Previous election"));
            Assert.Equal("1", moment.Fact(H, "Elections so far, this one included"));
        }

        /// <summary>
        /// With a mod that keeps more places: the list stops at 10 and says how many were ranked; the biggest
        /// drop is the largest fall below the 10, the biggest rise the largest climb there. A climb inside the
        /// 10 or a fall of one place doesn't win.
        /// </summary>
        [Fact]
        public void ManyPlaces_ListTen_AndTheBiggestMovesBelow()
        {
            List<data_girls.girls> idols = Enumerable.Range(1, 20).Select(i => Idol("I" + i)).ToList();
            data_girls.girls I(int n) => idols[n - 1];
            Numbered(1, Today.AddMonths(-6), idols.ToArray());
            SEvent_SSK._SSK election = Numbered(2, Today,
                I(1), I(3), I(4), I(5), I(6), I(7), I(8), I(9), I(10), I(11),
                I(20), I(12), I(13), I(14), I(15), I(16), I(17), I(2), I(18), I(19));

            Moment moment = MomentContext.Election(election);
            string results = moment.Fact(F, "Results");
            Assert.Equal(10, results.Split('\n').Count(l => l.StartsWith("- ")));
            Assert.StartsWith("\n- 1st: I1, 20000 votes (same place as last time)", results);
            Assert.Equal("20", moment.Fact(F, "Idols ranked"));
            Assert.Equal("18th: I2, 3000 votes (down from 2nd)", moment.Fact(F, "Biggest drop outside the top 10"));
            Assert.Equal("11th: I20, 10000 votes (up from 20th)", moment.Fact(F, "Biggest rise outside the top 10"));
            // The top 3, then the biggest moves, without her election place: these results are it
            Assert.Equal(new[] { "I1", "I3", "I4", "I2", "I20" }, moment.Facts(Moment.MembersSection).Select(f => f.Key));
            Assert.DoesNotContain(moment.Facts(Moment.MembersSection), f => f.Value.Contains("election"));
        }

        /// <summary>
        /// Without moves below the top, the lines are places 1 to 5.
        /// </summary>
        [Fact]
        public void Members_AreTheTopFive_WithoutMoves()
        {
            List<data_girls.girls> idols = Enumerable.Range(1, 7).Select(i => Idol("I" + i)).ToList();
            Moment moment = MomentContext.Election(Numbered(1, Today, idols.ToArray()));
            Assert.Equal(new[] { "I1", "I2", "I3", "I4", "I5" }, moment.Facts(Moment.MembersSection).Select(f => f.Key));
        }

        /// <summary>
        /// An idol who announced her graduation still takes part, and her lines say so, in the results and
        /// in the biggest moves.
        /// </summary>
        [Fact]
        public void AnnouncedGraduation_IsNamed()
        {
            data_girls.girls rin = Idol("Rin"), aoi = Idol("Aoi"), mei = Idol("Mei");
            Numbered(1, Today.AddMonths(-6), mei, rin, aoi);
            aoi.status = data_girls._status.announced_graduation;
            mei.status = data_girls._status.announced_graduation;
            Moment moment = MomentContext.Election(Numbered(2, Today, aoi, rin));
            Assert.Equal("\n- 1st: Aoi, 2000 votes (up from 3rd), announced her graduation\n- 2nd: Rin, 1000 votes (same place as last time)", moment.Fact(F, "Results"));
            Assert.Equal("Mei, no longer ranked (was 1st), announced her graduation", moment.Fact(F, "Biggest drop outside the top 10"));
        }

        /// <summary>
        /// Every group's idols take part; a sister group's are named with their group.
        /// </summary>
        [Fact]
        public void SisterGroupIdols_AreNamedWithTheirGroup()
        {
            data_girls.girls rin = Idol("Rin"), aoi = Idol("Aoi");
            Group("Stars", Today.AddYears(-2), rin);
            Group("Moons", Today.AddYears(-1), aoi);
            Moment moment = MomentContext.Election(Numbered(1, Today, aoi, rin));
            Assert.Equal("\n- 1st: Aoi of Moons, 2000 votes\n- 2nd: Rin, 1000 votes", moment.Fact(F, "Results"));
        }
    }
}
