using AICore;
using System.Linq;
using Xunit;

namespace AIFanChatter.Tests
{
    public class TabNoticesTests
    {
        public TabNoticesTests() => TabNotices.Reset();

        private static string[] Texts() => TabNotices.Shown.Select(n => n.Text).ToArray();

        [Fact]
        public void Newest_ComeFirst_AndTheOldestPastThreeDropOff()
        {
            for (int i = 1; i <= 4; i++)
            {
                Assert.True(TabNotices.Add("missed " + i, NoticeKind.Missed));
            }
            Assert.Equal(new[] { "missed 4", "missed 3", "missed 2" }, Texts());
        }

        [Fact]
        public void TheSameNoticeAgain_MovesToTheTop_WithoutACopy()
        {
            TabNotices.Add("a", NoticeKind.Missed);
            TabNotices.Add("b", NoticeKind.Missed);
            int version = TabNotices.Version;
            Assert.True(TabNotices.Add("a", NoticeKind.Missed));
            Assert.Equal(new[] { "a", "b" }, Texts());
            Assert.True(TabNotices.Version > version);

            // Already at the top: nothing changes
            version = TabNotices.Version;
            Assert.False(TabNotices.Add("a", NoticeKind.Missed));
            Assert.Equal(version, TabNotices.Version);
        }

        [Fact]
        public void AWorkingConnection_HasNoNotice()
        {
            Assert.False(TabNotices.Add("Anthropic with claude-haiku-5-5", NoticeKind.Connected));
            Assert.Empty(TabNotices.Shown);
        }

        /// <summary>
        /// The stopped or paused notice comes first, apart from the others' three: the latest replaces it,
        /// and it can't be closed.
        /// </summary>
        [Fact]
        public void TheStatus_ComesFirst_AndTheLatestReplacesIt()
        {
            TabNotices.Add("missed", NoticeKind.Missed);
            TabNotices.Add("paused", NoticeKind.Paused);
            TabNotices.Add("stopped", NoticeKind.Stopped);
            for (int i = 0; i < 3; i++)
            {
                TabNotices.Add("more " + i, NoticeKind.Missed);
            }
            Assert.Equal(new[] { "stopped", "more 2", "more 1", "more 0" }, Texts());
            TabNotice status = TabNotices.Shown[0];
            Assert.True(status.IsStatus);
            TabNotices.Close(status);
            Assert.Equal("stopped", Texts()[0]);
        }

        [Fact]
        public void TheStatus_GoesOnceRequestsRunAgain()
        {
            TabNotices.Add("stopped", NoticeKind.Stopped);
            TabNotices.Sync(stopped: true, paused: false);
            Assert.Single(TabNotices.Shown);
            TabNotices.Sync(stopped: false, paused: true);
            Assert.Empty(TabNotices.Shown);

            TabNotices.Add("paused", NoticeKind.Paused);
            TabNotices.Sync(stopped: false, paused: true);
            Assert.Single(TabNotices.Shown);
            TabNotices.Sync(stopped: false, paused: false);
            Assert.Empty(TabNotices.Shown);
        }

        [Fact]
        public void Close_RemovesOne()
        {
            TabNotices.Add("a", NoticeKind.Missed);
            TabNotices.Add("b", NoticeKind.Missed);
            TabNotices.Close(TabNotices.Shown.Single(n => n.Text == "a"));
            Assert.Equal(new[] { "b" }, Texts());
        }

        /// <summary>A load starts without the earlier notices, but requests stay stopped, and so does that notice.</summary>
        [Fact]
        public void Clear_KeepsOnlyTheStatus()
        {
            TabNotices.Add("stopped", NoticeKind.Stopped);
            TabNotices.Add("missed", NoticeKind.Missed);
            TabNotices.Clear();
            Assert.Equal(new[] { "stopped" }, Texts());
        }
    }
}
