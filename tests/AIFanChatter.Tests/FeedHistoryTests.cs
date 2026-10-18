using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AIFanChatter.Tests
{
    public class FeedHistoryTests
    {
        public FeedHistoryTests() => TestEnv.Reset();

        [Fact]
        public void EmptyFeed_GivesNothing_SoNoHeaderAppears()
        {
            Assert.Equal("", FeedHistory.Describe(null));
            Assert.Equal("", FeedHistory.Describe(new List<SNS_Manager._message>()));
            Assert.Equal("", PromptTemplate.Wrap("feed", FeedHistory.Describe(new List<SNS_Manager._message>())));
            Assert.DoesNotContain("History", Prompt.User(new Moment("x"), new List<SNS_Manager._message>()));
        }

        [Fact]
        public void Posts_AreListedOldestFirst_WithTheirReplies()
        {
            List<SNS_Manager._message> feed = new()
            {
                TestEnv.Post("F_C_T", "first", TestEnv.Post("M_C_A", "reply one")),
                TestEnv.Post("M_HC_YA", "second")
            };
            Assert.Equal("- F_C_T: first\n  - reply from M_C_A: reply one\n- M_HC_YA: second", FeedHistory.Describe(feed));
        }

        [Fact]
        public void Limits_KeepTheNewestPosts_FirstReplies_AndShortTexts()
        {
            List<SNS_Manager._message> feed = TestEnv.Feed(40);
            feed[39].Replies.AddRange(Enumerable.Range(0, 6).Select(i => TestEnv.Post("M_C_T", "reply " + i)));
            feed[38].Message = new string('a', 400);
            string text = FeedHistory.Describe(feed);
            List<string> posts = text.Split('\n').Where(l => l.StartsWith("- ")).ToList();
            Assert.Equal(FeedHistory.MaxPosts, posts.Count);
            Assert.Equal("- F_C_T: post 25", posts[0]);
            Assert.Equal(FeedHistory.MaxReplies, text.Split('\n').Count(l => l.StartsWith("  - reply from")));
            Assert.Contains("reply 2", text);
            Assert.DoesNotContain("reply 3", text);
            Assert.Contains("- F_C_T: " + new string('a', FeedHistory.MaxText - 3) + "...\n", text);
        }

        /// <summary>
        /// The mod's posts are wrapped in noparse and can hold "&lt;3"; the game's posts colour the names.
        /// </summary>
        [Fact]
        public void Texts_LoseOnlyTheirFormatting()
        {
            Assert.Equal("so good <3 love it", FeedHistory.Text("<noparse>so good <3 love it</noparse>"));
            Assert.Equal("Aya is the best", FeedHistory.Text("<color=#777bba>Aya</color> is the best"));
            Assert.Equal("two lines", FeedHistory.Text("two\nlines"));
        }

        [Fact]
        public void OddEntries_AreSkippedOrNamed()
        {
            List<SNS_Manager._message> feed = new()
            {
                null,
                TestEnv.Post("F_C_T", ""),
                TestEnv.Post("", "no poster", null, TestEnv.Post("M_C_T", "")),
                TestEnv.Post("SOME_MOD_POSTER", "from a mod")
            };
            Assert.Equal("- fan: no poster\n- SOME_MOD_POSTER: from a mod", FeedHistory.Describe(feed));
        }

        [Fact]
        public void Pending_PutsQueuedRepliesUnderTheirQueuedPost()
        {
            List<SNS_Manager._message> batch = new() { TestEnv.Post("F_HC_T", "<noparse>queued</noparse>", TestEnv.Post("M_C_A", "<noparse>queued reply</noparse>")) };
            FeedQueue.AddBatch(batch, TestGame.Today, new Random(1));
            FeedQueue.Items.Add(new QueuedItem { Due = TestGame.Today, User = "M_C_T", Text = "late", IsReply = true, ParentUser = "F_C_T", ParentText = "already shown" });

            List<SNS_Manager._message> pending = FeedHistory.Pending(FeedQueue.Items);
            SNS_Manager._message post = Assert.Single(pending);
            Assert.Equal("<noparse>queued</noparse>", post.Message);
            Assert.Equal("<noparse>queued reply</noparse>", Assert.Single(post.Replies).Message);
        }

        /// <summary>
        /// The game's feed and the queue, read fresh and copied, so the prompt never holds on to the game's list.
        /// </summary>
        [Fact]
        public void GameHost_GivesTheFeedThenTheQueue()
        {
            List<SNS_Manager._message> saved = SNS_Manager.Messages;
            try
            {
                SNS_Manager.Messages = new List<SNS_Manager._message> { TestEnv.Post("F_C_T", "shown") };
                FeedQueue.AddBatch(new List<SNS_Manager._message> { TestEnv.Post("M_C_T", "queued") }, TestGame.Today, new Random(1));

                IList<SNS_Manager._message> posts = new GameHost().RecentPosts;
                Assert.Equal(new[] { "shown", "queued" }, posts.Select(p => p.Message));
                posts.Clear();
                Assert.Single(SNS_Manager.Messages);
            }
            finally
            {
                SNS_Manager.Messages = saved;
            }
        }
    }
}
