using AICore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestEnv;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// The Refresh button's parts outside Unity: the next queued posts, the request that isn't counted, its
    /// posts shown at once, and the everyday chatter it asks for. The button itself is tested in the game.
    /// </summary>
    public class RefreshTests
    {
        private readonly FakeHost host = new();
        private readonly FakeTransport transport = new();
        private readonly Dispatcher dispatcher;

        public RefreshTests()
        {
            Reset();
            Configure();
            dispatcher = new Dispatcher(host, transport, new Random(1));
        }

        private static Moment Test() => new Moment("a test").Add("Title", () => "Song");

        private bool Send(bool manual)
        {
            bool sent = dispatcher.Request(Test(), manual);
            if (sent)
            {
                transport.Reply(200, Replies.Anthropic(Replies.Posts));
            }
            return sent;
        }

        private static List<SNS_Manager._message> Batch(int posts) => Enumerable.Range(1, posts)
            .Select(i => Post("F_C_T", "post " + i, Post("M_HC_A", "reply " + i + "a"), Post("F_HC_YA", "reply " + i + "b"))).ToList();

        [Fact]
        public void TakeNext_TakesTheNextPostsWithTheirReplies_InOrder()
        {
            FeedQueue.AddBatch(Batch(7), new DateTime(2020, 6, 3), new Random(1));
            List<QueuedItem> next = FeedQueue.TakeNext(5);

            Assert.Equal(15, next.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal("post " + (i + 1), next[i * 3].Text);
                Assert.All(next.Skip(i * 3 + 1).Take(2), r => Assert.True(r.IsReply && r.ParentText == "post " + (i + 1)));
            }
            Assert.Equal(new[] { "post 6", "post 7" }, FeedQueue.Items.Where(i => !i.IsReply).Select(i => i.Text));
            Assert.Equal(4, FeedQueue.Items.Count(i => i.IsReply));
        }

        [Fact]
        public void TakeNext_WithFewerQueued_TakesThemAll()
        {
            FeedQueue.AddBatch(Batch(2), new DateTime(2020, 6, 3), new Random(1));
            Assert.Equal(6, FeedQueue.TakeNext(5).Count);
            Assert.Empty(FeedQueue.Items);
            Assert.False(FeedQueue.HasPosts);
        }

        [Fact]
        public void PostCount_CountsPostsNotReplies()
        {
            FeedQueue.AddBatch(Batch(3), new DateTime(2020, 6, 3), new Random(1));
            Assert.Equal(3, FeedQueue.PostCount);
            FeedQueue.TakeNext(1);
            Assert.Equal(2, FeedQueue.PostCount);
        }

        [Fact]
        public void Manual_ShowsThePostsAtOnce_InsteadOfQueuing()
        {
            Assert.True(Send(manual: true));
            Assert.Equal(3, host.Shown.Single().Count);
            Assert.Empty(host.Queued);
        }

        [Fact]
        public void Manual_WorksWithZeroPerWeek()
        {
            Vars[Settings.PerWeekVar] = "0";
            Assert.False(Send(manual: false));
            Assert.True(Send(manual: true));
        }

        /// <summary>
        /// Manual requests skip the weekly limit, the 30 seconds and the hourly limit, and don't use them up:
        /// the automatic requests still get all of theirs.
        /// </summary>
        [Fact]
        public void Manual_SkipsTheLimits_AndDoesntCountTowardThem()
        {
            Vars[Settings.PerWeekVar] = "2";
            for (int i = 0; i < 25; i++)
            {
                Assert.True(Send(manual: true));
            }
            Assert.True(Send(manual: false));
            // Right after an automatic request, and with the week's requests used up next
            Assert.True(Send(manual: true));
            host.RealTime += 30;
            Assert.True(Send(manual: false));
            host.RealTime += 30;
            Assert.False(Send(manual: false));
            Assert.True(Send(manual: true));
        }

        [Fact]
        public void Manual_StillWaitsForTheRequestBefore()
        {
            Assert.True(dispatcher.Request(Test()));
            Assert.Equal("a request is still running", dispatcher.Blocked(manual: true));
            Assert.False(dispatcher.Request(Test(), manual: true));
        }

        [Fact]
        public void Manual_StillStopsWhenOff_Stopped_OrPaused()
        {
            Vars[Settings.EnabledVar] = "0";
            Assert.False(Send(manual: true));
            Vars[Settings.EnabledVar] = "1";

            Assert.True(dispatcher.Request(Test(), manual: true));
            transport.Reply(401, "{\"error\":{\"message\":\"nope\"}}");
            Assert.False(Send(manual: true));
            dispatcher.OnSettingsChanged();

            Assert.True(dispatcher.Request(Test(), manual: true));
            transport.Reply(529, "{\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}");
            Assert.False(Send(manual: true));
            host.RealTime += AIClient.BackoffSeconds;
            Assert.True(Send(manual: true));
        }

        [Fact]
        public void Manual_StillNeedsFans()
        {
            foreach (string code in host.FanCounts.Keys.ToList())
            {
                host.FanCounts[code] = 1;
            }
            Assert.False(Send(manual: true));
            Assert.True(dispatcher.TooFewFans);
        }

        [Fact]
        public void TooFewFans_CountsEveryKindOfFan()
        {
            foreach (string code in host.FanCounts.Keys.ToList())
            {
                host.FanCounts[code] = 0;
            }
            host.FanCounts[Prompt.FanCodes[0]] = Dispatcher.MinFans - 1;
            Assert.True(dispatcher.TooFewFans);
            host.FanCounts[Prompt.FanCodes[1]] = 1;
            Assert.False(dispatcher.TooFewFans);
        }

        private static Groups._group ChatterAgency()
        {
            List<data_girls.girls> idols = Enumerable.Range(1, 9).Select(i => Idol("Idol" + i, fans: 10000 - i * 1000)).ToList();
            Groups._group group = Group("Stars", Today.AddYears(-2), idols.ToArray());
            for (int i = 0; i < 6; i++)
            {
                Single("Song " + i, Today.AddMonths(-2 * i), 1000 * (i + 1), idols[0]).In(group);
            }
            Concert("Hall", Today.AddDays(-20), 5000);
            Shows.shows.Add(new Shows._show { title = "Idol Kitchen", status = Shows._show._status.released });
            return group;
        }

        /// <summary>
        /// Five idols: one of the three with the most fans and four from anyone, and three of the single,
        /// an earlier single, the concert and the show.
        /// </summary>
        [Fact]
        public void Chatter_PicksFiveIdols_OneOfThemPopular_AndThreeMemories()
        {
            ChatterAgency();
            HashSet<string> seen = new();
            for (int seed = 0; seed < 30; seed++)
            {
                Moment moment = MomentContext.Chatter(new Random(seed));
                List<string> idols = moment.Facts(AIFanChatter.Moment.MembersSection).Select(f => f.Key + f.Value).ToList();
                Assert.Equal(MomentContext.ChatterIdols, idols.Count);
                Assert.Contains(idols, line => new[] { "Idol1", "Idol2", "Idol3" }.Any(line.Contains));
                idols.ForEach(line => seen.Add(line.Substring(0, 5)));

                List<string> memories = moment.Facts(AIFanChatter.Moment.HistorySection).Select(f => f.Key).ToList();
                Assert.Equal(MomentContext.ChatterMemories, memories.Count);
                Assert.Subset(new HashSet<string> { "Latest single", "An earlier single", "Latest concert", "A show on air" }, memories.ToHashSet());
            }
            // Over many presses, every idol comes up
            Assert.Equal(9, seen.Count);
        }

        [Fact]
        public void Chatter_SaysNothingAboutNoNews_AndGivesTwentyFeedPosts()
        {
            ChatterAgency();
            Moment moment = MomentContext.Chatter(new Random(1));
            List<SNS_Manager._message> feed = Enumerable.Range(1, 30).Select(i => Post("F_C_T", "feed " + i)).ToList();
            string prompt = Prompt.User(moment, feed);

            Assert.Contains("Event: everyday fan chatter about the group", prompt);
            Assert.DoesNotContain("news", prompt);
            Assert.Contains("- F_C_T: feed 11\n", prompt);
            Assert.DoesNotContain("feed 10\n", prompt);
            // Other moments keep 8
            Assert.DoesNotContain("feed 22\n", Prompt.User(Test(), feed));
        }
    }
}
