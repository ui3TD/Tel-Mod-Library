using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestEnv;

namespace AIFanChatter.Tests
{
    public class FeedQueueTests
    {
        private static readonly DateTime Now = new(2020, 6, 3, 10, 0, 0);

        public FeedQueueTests() => Reset();

        private static List<SNS_Manager._message> Batch(string tag) => new()
        {
            Post("F_HC_T", tag + " one", Post("M_C_A", tag + " reply a"), Post("F_C_YA", tag + " reply b")),
            Post("M_C_YA", tag + " two"),
            Post("F_C_A", tag + " three", Post("M_HC_T", tag + " reply c"))
        };

        private static List<QueuedItem> DrainUntil(DateTime end)
        {
            List<QueuedItem> released = new();
            for (DateTime t = Now; t <= end; t = t.AddMinutes(10))
            {
                QueuedItem item;
                while ((item = FeedQueue.TakeDue(t)) != null)
                {
                    released.Add(item);
                }
            }
            return released;
        }

        [Fact]
        public void FirstPost_IsTheOnlyFirst()
        {
            FeedQueue.AddBatch(Batch("a"), Now, new Random(1));
            QueuedItem first = FeedQueue.TakeDue(Now.AddDays(3));
            Assert.Equal("a one", first.Text);
            Assert.True(first.First);
            Assert.Single(FeedQueue.Items.Concat(new[] { first }).Where(i => i.First));
        }

        /// <summary>
        /// The 48 hours after the moment are split into one stretch per post: each post comes, in order, at a
        /// random time in its own stretch, so the posts are spread over the two days.
        /// </summary>
        [Fact]
        public void Posts_AreSpreadOverTheTwoDaysAfterTheMoment()
        {
            HashSet<DateTime> seen = new();
            for (int seed = 0; seed < 50; seed++)
            {
                FeedQueue.Clear();
                FeedQueue.AddBatch(Batch("a"), Now, new Random(seed));
                List<DateTime> posts = FeedQueue.Items.Where(i => !i.IsReply).Select(i => i.Due).ToList();
                Assert.Equal(3, posts.Count);
                for (int i = 0; i < posts.Count; i++)
                {
                    Assert.InRange(posts[i], Now.AddHours(16 * i), Now.AddHours(16 * (i + 1)).AddTicks(-1));
                }
                seen.Add(posts[0]);
            }
            Assert.True(seen.Count > 10, "The posts' times aren't random");
        }

        [Fact]
        public void Replies_ComeWithinSixHoursOfTheirPost_AndWithinTheTwoDays()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                FeedQueue.Clear();
                FeedQueue.AddBatch(Batch("a"), Now, new Random(seed));
                foreach (QueuedItem reply in FeedQueue.Items.Where(i => i.IsReply))
                {
                    QueuedItem post = FeedQueue.Items.Single(i => !i.IsReply && i.Text == reply.ParentText);
                    Assert.InRange(reply.Due, post.Due, post.Due.AddHours(FeedQueue.MaxReplyDelayHours));
                    Assert.True(reply.Due <= Now.AddHours(FeedQueue.WindowHours), "A reply came after the two days");
                }
            }
        }

        /// <summary>
        /// A reply that arrives a day after its moment has its first day's posts due at once.
        /// </summary>
        [Fact]
        public void PostsWhoseTimePassedWhileWaiting_AreDueAtOnce()
        {
            FeedQueue.AddBatch(Batch("a"), Now.AddDays(-1), new Random(1));
            List<QueuedItem> due = FeedQueue.TakeAllDue(Now);
            Assert.Contains(due, i => i.Text == "a one");
            Assert.All(FeedQueue.Items, i => Assert.True(i.Due > Now));
        }

        [Fact]
        public void Replies_ComeWithOrAfterTheirPostAndNameIt()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                FeedQueue.Clear();
                FeedQueue.AddBatch(Batch("a"), Now, new Random(seed));
                List<QueuedItem> released = DrainUntil(Now.AddDays(3));
                Assert.Equal(6, released.Count);
                foreach (QueuedItem reply in released.Where(i => i.IsReply))
                {
                    int parent = released.FindIndex(i => !i.IsReply && i.Text == reply.ParentText && i.User == reply.ParentUser);
                    Assert.True(parent >= 0 && parent < released.IndexOf(reply), "A reply came before its post");
                }
            }
        }

        /// <summary>
        /// A jump in time (fast-forward) releases everything due at once, in release order, posts before their replies.
        /// </summary>
        [Fact]
        public void AJumpInTime_ReleasesEverythingDueInOrder()
        {
            FeedQueue.AddBatch(Batch("a"), Now, new Random(1));
            List<QueuedItem> released = FeedQueue.TakeAllDue(Now.AddDays(5));
            Assert.Equal(6, released.Count);
            Assert.Empty(FeedQueue.Items);
            Assert.True(released.Zip(released.Skip(1), (a, b) => a.Due <= b.Due).All(inOrder => inOrder), "Out of release order");
            foreach (QueuedItem reply in released.Where(i => i.IsReply))
            {
                Assert.True(released.FindIndex(i => !i.IsReply && i.Text == reply.ParentText) < released.IndexOf(reply), "A reply came before its post");
            }
        }

        [Fact]
        public void OnlyWhatsDue_IsReleased()
        {
            FeedQueue.AddBatch(Batch("a"), Now, new Random(1));
            DateTime later = Now.AddHours(20);
            List<QueuedItem> released = FeedQueue.TakeAllDue(later);
            Assert.NotEmpty(released);
            Assert.All(released, i => Assert.True(i.Due <= later));
            Assert.NotEmpty(FeedQueue.Items);
            Assert.All(FeedQueue.Items, i => Assert.True(i.Due > later));
        }

        [Fact]
        public void NoBatch_IsDropped()
        {
            string[] tags = { "a", "b", "c", "d", "e" };
            for (int i = 0; i < tags.Length; i++)
            {
                FeedQueue.AddBatch(Batch(tags[i]), Now.AddHours(i), new Random(i));
            }
            List<QueuedItem> released = FeedQueue.TakeAllDue(Now.AddDays(10));
            Assert.Equal(tags.Length * 6, released.Count);
            Assert.All(tags, tag => Assert.Equal(6, released.Count(i => i.Text.StartsWith(tag + " "))));
            Assert.Equal(released.Select(i => i.Due).OrderBy(d => d), released.Select(i => i.Due));
        }

        [Fact]
        public void Queue_SurvivesASaveAndLoad()
        {
            FeedQueue.AddBatch(Batch("quote \" and \\ and 日本"), Now, new Random(3));
            List<QueuedItem> before = FeedQueue.Items.ToList();
            string saved = FeedQueue.Serialize();
            FeedQueue.Load(saved);
            Assert.Equal(before.Count, FeedQueue.Items.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].Due, FeedQueue.Items[i].Due);
                Assert.Equal(before[i].Text, FeedQueue.Items[i].Text);
                Assert.Equal(before[i].User, FeedQueue.Items[i].User);
                Assert.Equal(before[i].IsReply, FeedQueue.Items[i].IsReply);
                Assert.Equal(before[i].ParentText, FeedQueue.Items[i].ParentText);
                Assert.Equal(before[i].First, FeedQueue.Items[i].First);
            }
        }

        [Fact]
        public void OldSavedQueue_WithBatchNumbers_StillLoads()
        {
            FeedQueue.Load("[{\"batch\":\"4\",\"first\":\"1\",\"due\":\"2020-06-03T12:00:00.0000000\",\"user\":\"F_HC_T\",\"text\":\"old post\"},"
                + "{\"batch\":\"4\",\"first\":\"0\",\"due\":\"2020-06-03T13:00:00.0000000\",\"user\":\"M_C_A\",\"text\":\"old reply\",\"parentUser\":\"F_HC_T\",\"parentText\":\"old post\"}]");
            Assert.Equal(2, FeedQueue.Items.Count);
            Assert.True(FeedQueue.Items[0].First);
            Assert.Equal(new DateTime(2020, 6, 3, 12, 0, 0), FeedQueue.Items[0].Due);
            Assert.True(FeedQueue.Items[1].IsReply);
            Assert.Equal("old post", FeedQueue.Items[1].ParentText);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("[{\"batch\":\"x\"}]")]
        public void BrokenSavedQueue_LoadsEmpty(string saved)
        {
            FeedQueue.AddBatch(Batch("a"), Now, new Random(1));
            FeedQueue.Load(saved);
            Assert.Empty(FeedQueue.Items);
        }
    }

    public class FeedStoreTests
    {
        public FeedStoreTests() => Reset();

        [Fact]
        public void Cap_IsOneHundred()
        {
            Assert.Equal(100, FeedStore.CapValue());
            List<SNS_Manager._message> feed = Feed(130);
            FeedStore.TrimToCap(feed);
            Assert.Equal(100, feed.Count);
            Assert.Equal("post 30", feed[0].Message);
        }

        [Fact]
        public void Save_KeepsTheNewestTwentyInTheGamesField()
        {
            List<SNS_Manager._message> feed = Feed(50);
            List<SNS_Manager._message> kept = FeedStore.SplitForSave(feed, out List<SNS_Manager._message> older);
            Assert.Equal(20, kept.Count);
            Assert.Equal("post 30", kept[0].Message);
            Assert.Equal("post 49", kept[19].Message);
            Assert.Equal(30, older.Count);
            Assert.Equal(50, feed.Count);
        }

        [Fact]
        public void Save_OfTwentyOrFewer_IsNotSplit()
        {
            List<SNS_Manager._message> feed = Feed(20);
            Assert.Same(feed, FeedStore.SplitForSave(feed, out List<SNS_Manager._message> older));
            Assert.Null(older);
        }

        [Fact]
        public void Load_PutsTheOlderPostsBackInFront()
        {
            List<SNS_Manager._message> loaded = Feed(20, "new");
            Assert.Equal(30, FeedStore.MergeOnLoad(loaded, Feed(30, "old")));
            Assert.Equal(50, loaded.Count);
            Assert.Equal("old 0", loaded[0].Message);
            Assert.Equal("new 19", loaded[49].Message);
        }

        [Fact]
        public void Messages_SurviveTheirOwnJson()
        {
            List<SNS_Manager._message> posts = new()
            {
                Post("F_HC_T", "quote \" slash \\ newline \n emoji 🎤 日本語", Post("M_C_A", "reply")),
                new SNS_Manager._message { Chara = SNS_Manager._chara.Fujimoto, Message = "story post" }
            };
            List<SNS_Manager._message> back = FeedStore.Deserialize(FeedStore.Serialize(posts));
            Assert.Equal(2, back.Count);
            Assert.Equal(posts[0].Message, back[0].Message);
            Assert.Equal("reply", back[0].Replies.Single().Message);
            Assert.Equal("M_C_A", back[0].Replies.Single().User);
            Assert.Equal(SNS_Manager._chara.Fujimoto, back[1].Chara);
            Assert.Equal("", back[1].User);
        }

        [Fact]
        public void FindParent_MatchesFanAndTextNewestFirst()
        {
            List<SNS_Manager._message> feed = new() { Post("F_C_T", "same"), Post("M_C_T", "same"), Post("F_C_T", "same") };
            Assert.Same(feed[2], FeedStore.FindParent(feed, "F_C_T", "same"));
            Assert.Null(FeedStore.FindParent(feed, "F_HC_A", "same"));
            Assert.Null(FeedStore.FindParent(null, "F_C_T", "same"));
        }

        [Fact]
        public void SaveThenLoad_RestoresTheWholeFeedAndQueue()
        {
            List<SNS_Manager._message> feed = Feed(60);
            FeedQueue.AddBatch(new List<SNS_Manager._message> { Post("F_C_T", "queued") }, new DateTime(2020, 1, 1), new Random(1));
            List<variables._variable> vars = new();

            List<SNS_Manager._message> saved = SNS_Manager_SaveFunction.SaveFeed(feed, vars);
            Assert.Equal(20, saved.Count);
            Assert.NotNull(FeedStore.GetVariable(vars, FeedStore.OlderPostsVar));
            Assert.NotNull(FeedStore.GetVariable(vars, FeedQueue.QueueVar));

            FeedQueue.Clear();
            List<SNS_Manager._message> loaded = new(saved);
            Assert.True(SNS_Manager_LoadFunction.LoadFeed(loaded, vars));
            Assert.Equal(feed.Select(p => p.Message), loaded.Select(p => p.Message));
            Assert.Equal("queued", FeedQueue.Items.Single().Text);
        }

        [Fact]
        public void SavingASmallFeed_RemovesStaleVariables()
        {
            List<variables._variable> vars = new()
            {
                new variables._variable { name = FeedStore.OlderPostsVar, value = "[]" },
                new variables._variable { name = FeedQueue.QueueVar, value = "[]" }
            };
            SNS_Manager_SaveFunction.SaveFeed(Feed(5), vars);
            Assert.Empty(vars);
        }
    }
}
