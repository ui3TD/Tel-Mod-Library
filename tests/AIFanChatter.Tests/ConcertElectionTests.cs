using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// Election singles (the voting single, and the single made from the results), and a concert that waits
    /// for its election so both go out as one moment.
    /// </summary>
    public class ConcertElectionTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string H = Moment.HistorySection;

        private static readonly Dictionary<string, string> Texts = new()
        {
            ["SEVENT__ELECTION"] = "Election",
            ["CONCERT__OPEN_AIR"] = "Open Air Stage",
            ["CONCERT__CLUB"] = "Club",
        };

        public ConcertElectionTests()
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

        /// <summary>An election set up and waiting: its voting single and its concert linked, as the game links them.</summary>
        private static SEvent_SSK._SSK Upcoming(singles._single single, SEvent_Concerts._concert concert)
        {
            SEvent_SSK._SSK election = new() { Status = SEvent_Tour.tour._status.normal };
            single.SSK = election;
            election.Single = single;
            concert.SSK = election;
            election.Concert = concert;
            SEvent_SSK.Elections.Add(election);
            return election;
        }

        [Fact]
        public void VotingSingle_SaysSo_BeforeItsElection()
        {
            Election(Today.AddMonths(-6), 2, Idol("Mirai"), Idol("Rin")).Count = 1;
            singles._single single = Single("Ballot Song", Today, 90000);
            SEvent_Concerts._concert concert = Concert("Live", Today.AddDays(14), 0, done: false);
            concert.Venue = SEvent_Concerts._venue.openAirStage;
            SEvent_SSK._SSK election = Upcoming(single, concert);

            Assert.Equal("Voting single for Election #2: each CD comes with a ballot, and the results are announced after the concert at Open Air Stage",
                MomentContext.Single(single).Fact(F, "Election"));
            // A single whose election is over isn't announced as a voting single any more
            election.Status = SEvent_Tour.tour._status.finished;
            Assert.Null(MomentContext.Single(single).Fact(F, "Election"));
            Assert.Null(MomentContext.Single(Single("Plain", Today, 1)).Fact(F, "Election"));
        }

        /// <summary>
        /// The single made from an election's results says whose lineup it is, and names ranked idols who
        /// graduated since, whose slots the game leaves empty.
        /// </summary>
        [Fact]
        public void ElectionSingle_NamesItsElection_AndGraduatesMissing()
        {
            data_girls.girls rin = Idol("Rin"), aoi = Idol("Aoi"), mei = Idol("Mei");
            SEvent_SSK._SSK election = Election(Today.AddMonths(-1), 3, rin, aoi, mei);
            election.Count = 1;
            aoi.Graduated(Today.AddDays(-10));
            singles._single single = Single("Results Song", Today, 80000, rin, mei);
            single.IsElectionSingle = true;
            election.ReleaseSingle = single;

            Moment moment = MomentContext.Single(single);
            Assert.Equal("Election single: its lineup is the top places of Election #1, won by Rin", moment.Fact(F, "Election"));
            Assert.Equal("\n- Aoi, 2nd, graduated since", moment.Fact(F, "Ranked but not in it"));
        }

        /// <summary>
        /// A concert with its election still to come waits; the election's end takes it. A concert without an
        /// election, or another election, doesn't.
        /// </summary>
        [Fact]
        public void Concert_WaitsForItsElection()
        {
            SEvent_Concerts._concert concert = Concert("Live", Today, 9000);
            SEvent_SSK._SSK election = Upcoming(Single("Ballot Song", Today.AddDays(-14), 1), concert);
            SEvent_Concerts_concert_Finish.Postfix(concert, true);
            Assert.Same(concert, ConcertElection.Pending);

            Assert.Null(ConcertElection.TakeFor(new SEvent_SSK._SSK()));
            Assert.Same(concert, ConcertElection.TakeFor(election));
            Assert.Null(ConcertElection.Pending);

            SEvent_Concerts._concert plain = Concert("Plain", Today, 9000);
            SEvent_Concerts_concert_Finish.Postfix(plain, true);
            Assert.Null(ConcertElection.Pending);
        }

        /// <summary>
        /// A concert whose election never finished goes out alone a full game day later, not sooner: the clock
        /// runs for a moment between the concert's screen and the results.
        /// </summary>
        [Fact]
        public void WaitingConcert_IsOverdue_AfterADay()
        {
            SEvent_Concerts._concert concert = Concert("Live", Today, 9000);
            ConcertElection.Wait(concert, Today);
            Assert.Null(ConcertElection.TakeOverdue(Today.AddHours(20)));
            Assert.Same(concert, ConcertElection.TakeOverdue(Today.AddDays(1)));
            Assert.Null(ConcertElection.Pending);
        }

        /// <summary>
        /// The combined moment: the concert's facts, then the election's without naming the concert again; the
        /// election's idols; both histories.
        /// </summary>
        [Fact]
        public void ConcertElection_CombinesBoth()
        {
            data_girls.girls rin = Idol("Rin"), aoi = Idol("Aoi");
            Concert("Earlier", Today.AddMonths(-3), 5000).Venue = SEvent_Concerts._venue.club;
            SEvent_SSK._SSK earlier = Election(Today.AddMonths(-6), 2, aoi, rin);
            earlier.Count = 1;
            SEvent_Concerts._concert concert = Concert("Live", Today, 9000);
            concert.Venue = SEvent_Concerts._venue.openAirStage;
            SEvent_SSK._SSK election = Election(Today, 2, rin, aoi);
            election.Count = 2;
            election.Concert = concert;
            concert.SSK = election;

            Moment moment = MomentContext.ConcertElection(concert, election);
            Assert.Equal("the group held a concert, and the results of Election #2 were announced after it", moment.Kind);
            Assert.Equal("the concert at Open Air Stage and Election #2", moment.Label);
            Assert.Equal("Open Air Stage", moment.Fact(F, "Venue"));
            Assert.Equal("Election #2", moment.Fact(F, "Election"));
            Assert.Null(moment.Fact(F, "Held after the concert at"));
            Assert.StartsWith("\n- 1st: Rin", moment.Fact(F, "Results"));
            Assert.Equal(new[] { "Rin", "Aoi" }, moment.Facts(M).Select(f => f.Key));
            Assert.DoesNotContain(moment.Facts(M), f => f.Value.Contains("election"));
            Assert.NotNull(moment.Fact(H, "Previous concert"));
            Assert.NotNull(moment.Fact(H, "Previous election"));
            Assert.NotNull(moment.Fact(Moment.AgencySection, "Date"));
        }
    }
}
