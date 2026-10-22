using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// An election cancelled after its voting single came out: fans bought its CDs for ballots that no
    /// longer count. Before the single is out, fans never heard of the election.
    /// </summary>
    public class ElectionCancelTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string H = Moment.HistorySection;

        private static readonly Dictionary<string, string> Texts = new()
        {
            ["SEVENT__ELECTION"] = "Election",
            ["CONCERT__OPEN_AIR"] = "Open Air Stage",
            ["SSK__NATIONAL_TV"] = "National TV",
        };

        public ElectionCancelTests()
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

        /// <summary>
        /// Election #2 set up after Election #1 (won by Mirai): its voting single "Ballot Song" with Rin at the
        /// center, released 5 days ago with 1000 CDs sold, and its results concert at the Open Air Stage.
        /// </summary>
        private static SEvent_SSK Upcoming(bool released = true)
        {
            Election(Today.AddMonths(-6), 2, Idol("Mirai"), Idol("Aoi")).Count = 1;
            singles._single single = Single("Ballot Song", Today.AddDays(-5), 1000, Idol("Rin"), Idol("Mio"));
            single.sales.Add(new singles._single._sales { sales = 1000 });
            if (!released)
            {
                single.status = singles._single._status.normal;
            }
            SEvent_Concerts._concert concert = Concert("Live", Today.AddDays(9), 0, done: false);
            concert.Venue = SEvent_Concerts._venue.openAirStage;
            SEvent_SSK._SSK election = new() { Status = SEvent_Tour.tour._status.normal, Single = single, Concert = concert, Broadcast = SEvent_SSK._broadcast.nationalTV };
            single.SSK = election;
            concert.SSK = election;
            SEvent_SSK.Elections.Add(election);
            // The component's own election, as the game holds the one being set up
            SEvent_SSK manager = (SEvent_SSK)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SEvent_SSK));
            manager.SSK = election;
            return manager;
        }

        /// <summary>
        /// The game's CancelSSK around the patch: it unlinks the single and the concert and drops the
        /// election. CancelConcert unlinks the concert first, then calls it.
        /// </summary>
        private static SEvent_SSK_CancelSSK.Before Cancel(SEvent_SSK manager, bool ran = true, bool withConcert = false)
        {
            if (withConcert)
            {
                manager.SSK.Concert.SSK = null;
            }
            SEvent_SSK_CancelSSK.Prefix(manager, out SEvent_SSK_CancelSSK.Before before);
            if (ran)
            {
                SEvent_SSK._SSK election = manager.SSK;
                election.Single?.SetSSK(null);
                if (election.Concert != null)
                {
                    election.Concert.SSK = null;
                }
                SEvent_SSK.Elections.Remove(election);
                manager.SSK = null;
            }
            return before;
        }

        [Fact]
        public void ReleasedVotingSingle_Posts()
        {
            SEvent_SSK_CancelSSK.Before before = Cancel(Upcoming());
            Assert.True(SEvent_SSK_CancelSSK.ShouldPost(before, true, SEvent_SSK.Elections));
        }

        /// <summary>
        /// A voting single not out yet (fans never heard of the election), a skipped original, or an election
        /// another mod kept: no post.
        /// </summary>
        [Fact]
        public void OtherCalls_DoNotPost()
        {
            Assert.False(SEvent_SSK_CancelSSK.ShouldPost(Cancel(Upcoming(released: false)), true, SEvent_SSK.Elections));
            TestEnv.Reset();
            Assert.False(SEvent_SSK_CancelSSK.ShouldPost(Cancel(Upcoming(), ran: false), false, SEvent_SSK.Elections));
            TestEnv.Reset();
            SEvent_SSK_CancelSSK.Before kept = Cancel(Upcoming(), ran: false);
            Assert.False(SEvent_SSK_CancelSSK.ShouldPost(kept, true, SEvent_SSK.Elections));
            Assert.False(SEvent_SSK_CancelSSK.ShouldPost(null, true, SEvent_SSK.Elections));
        }

        /// <summary>
        /// The election by the title the game gave it before the call, its broadcast, the single, its sales,
        /// what fans paid for it, and its concert; the single's lineup in the lines.
        /// </summary>
        [Fact]
        public void Cancellation_GivesItsFacts()
        {
            Moment moment = MomentContext.ElectionCancelled(Cancel(Upcoming()));
            Assert.Equal("an election was cancelled after its voting single came out: the ballot that came with each CD no longer counts", moment.Kind);
            Assert.Equal("Election #2", moment.Fact(F, "Election"));
            Assert.Equal("National TV", moment.Fact(F, "Was to be broadcast on"));
            Assert.Equal("\"Ballot Song\", released 5 days ago", moment.Fact(F, "Voting single"));
            Assert.Equal("1000 CDs", moment.Fact(F, "Voting single sales"));
            Assert.Equal("500000 yen", moment.Fact(F, "Money fans spent on it"));
            Assert.Equal("at Open Air Stage, still on without the results", moment.Fact(F, "Results concert"));
            Assert.Equal(new[] { "Rin", "Mio" }, moment.Facts(M).Select(f => f.Key));
        }

        /// <summary>A handshake event raises what each CD brings in, as the game counts it.</summary>
        [Fact]
        public void Money_IncludesTheHandshakeExtra()
        {
            SEvent_SSK manager = Upcoming();
            manager.SSK.Single.WithHandshake();
            long expected = 1000L * (singles.RevenuePerCD + singles.GetRevenueIncreaseForGroupHS());
            Assert.Equal(expected + " yen", MomentContext.ElectionCancelled(Cancel(manager)).Fact(F, "Money fans spent on it"));
        }

        [Fact]
        public void CancelledWithItsConcert_SaysSo()
        {
            Moment moment = MomentContext.ElectionCancelled(Cancel(Upcoming(), withConcert: true));
            Assert.Equal("at Open Air Stage, also cancelled", moment.Fact(F, "Results concert"));
        }

        /// <summary>
        /// The election before it and its winner; no count of elections, as this one never happened. The
        /// election moment still counts.
        /// </summary>
        [Fact]
        public void History_NamesThePreviousElection_WithoutACount()
        {
            Moment moment = MomentContext.ElectionCancelled(Cancel(Upcoming()));
            Assert.StartsWith("Election #1, won by Mirai", moment.Fact(H, "Previous election"));
            Assert.Null(moment.Fact(H, "Elections so far, this one included"));

            SEvent_SSK._SSK held = Election(Today, 2, Idol("Rin"), Idol("Aoi"));
            held.Count = 2;
            Assert.Equal("2", MomentContext.Election(held).Fact(H, "Elections so far, this one included"));
        }

        /// <summary>
        /// The game lowers fans' opinion inside the call; the prompt, built in the postfix, carries the
        /// lowered opinion.
        /// </summary>
        [Fact]
        public void Prompt_CarriesTheOpinionTheCancellationLowered()
        {
            TestEnv.Configure();
            FakeHost host = new();
            FakeTransport transport = new();
            Dispatcher.Instance = new Dispatcher(host, transport, new Random(1));
            host.FanOpinions["M_HC_T"] = 60;
            SEvent_SSK manager = Upcoming();

            SEvent_SSK_CancelSSK.Prefix(manager, out SEvent_SSK_CancelSSK.Before before);
            // What the game's CancelSSK does: hardcore fans' opinion falls, then the election goes
            host.FanOpinions["M_HC_T"] = 55;
            manager.SSK.Single.SetSSK(null);
            SEvent_SSK.Elections.Remove(manager.SSK);
            manager.SSK = null;
            SEvent_SSK_CancelSSK.Postfix(before, true);

            string body = transport.Sent.Single().Body;
            Assert.Contains("opinion of the group: 55%", body);
            Assert.Contains("Election #2", body);
        }
    }
}
