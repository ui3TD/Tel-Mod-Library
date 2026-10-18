using System;
using System.Linq;
using Xunit;

namespace AIFanChatter.Tests
{
    public class DispatcherTests
    {
        private readonly FakeHost host = new();
        private readonly FakeTransport transport = new();
        private readonly Dispatcher dispatcher;

        public DispatcherTests()
        {
            TestEnv.Reset();
            TestEnv.Configure();
            dispatcher = new Dispatcher(host, transport, new Random(1));
        }

        private static Moment Moment() => new Moment("a test").Add("Title", () => "Song");

        private bool SendAndReply(string body = null)
        {
            bool sent = dispatcher.Request(Moment());
            if (sent)
            {
                transport.Reply(200, body ?? Replies.Anthropic(Replies.Posts));
            }
            return sent;
        }

        [Fact]
        public void Reply_QueuesThePosts()
        {
            Assert.True(SendAndReply());
            Assert.Equal(3, host.Queued.Single().Count);
            Assert.Empty(host.Notices);
            Assert.Contains("Event: a test", transport.Sent.Single().Body);
            Assert.Contains("Title: Song", transport.Sent.Single().Body);
        }

        [Fact]
        public void Off_SendsNothing()
        {
            TestEnv.Vars[Settings.EnabledVar] = "0";
            Assert.False(dispatcher.Request(Moment()));
            Assert.Empty(transport.Sent);
        }

        [Fact]
        public void ZeroPerWeek_SendsNothing()
        {
            TestEnv.Vars[Settings.PerWeekVar] = "0";
            Assert.False(dispatcher.Request(Moment()));
        }

        [Fact]
        public void OneRequest_AtATime()
        {
            Assert.True(dispatcher.Request(Moment()));
            host.RealTime += 100;
            Assert.False(dispatcher.Request(Moment()));
            transport.Reply(200, Replies.Anthropic(Replies.Posts));
            Assert.True(dispatcher.Request(Moment()));
        }

        [Fact]
        public void ThirtySeconds_BetweenRequests()
        {
            Assert.True(SendAndReply());
            host.RealTime += 29;
            Assert.False(dispatcher.Request(Moment()));
            host.RealTime += 1;
            Assert.True(SendAndReply());
        }

        [Fact]
        public void WeeklyLimit_ResetsOnMonday()
        {
            TestEnv.Vars[Settings.PerWeekVar] = "2";
            host.GameTime = new DateTime(2020, 6, 3); // a Wednesday
            Assert.True(SendAndReply());
            host.RealTime += 60;
            Assert.True(SendAndReply());
            host.RealTime += 60;
            host.GameTime = new DateTime(2020, 6, 7); // Sunday, same week
            Assert.False(dispatcher.Request(Moment()));
            host.GameTime = new DateTime(2020, 6, 8); // Monday
            Assert.True(SendAndReply());
        }

        [Fact]
        public void TwentyAnHour_InRealTime()
        {
            TestEnv.Vars[Settings.PerWeekVar] = "7";
            for (int i = 0; i < 20; i++)
            {
                host.GameTime = host.GameTime.AddDays(7);
                host.RealTime += 31;
                Assert.True(SendAndReply());
            }
            host.GameTime = host.GameTime.AddDays(7);
            host.RealTime += 31;
            Assert.False(dispatcher.Request(Moment()));
            host.RealTime += 3600;
            Assert.True(SendAndReply());
        }

        [Theory]
        [InlineData(401)]
        [InlineData(404)]
        public void RejectedSettings_StopRequestsUntilTheyChange(long status)
        {
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(status, "{\"error\":{\"message\":\"nope\"}}");
            Assert.True(dispatcher.Suspended);
            Assert.True(host.Notices.Single().Error);
            host.RealTime += 7200;
            Assert.False(dispatcher.Request(Moment()));

            dispatcher.OnSettingsChanged();
            Assert.False(dispatcher.Suspended);
            Assert.True(SendAndReply());
        }

        [Fact]
        public void BusyService_PausesForTenMinutes()
        {
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(529, "{\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}");
            Assert.False(dispatcher.Suspended);
            host.RealTime += 599;
            Assert.False(dispatcher.Request(Moment()));
            host.RealTime += 1;
            Assert.True(dispatcher.Request(Moment()));
        }

        [Fact]
        public void NoConnection_PausesForTenMinutes()
        {
            Assert.True(dispatcher.Request(Moment()));
            transport.NoConnection();
            host.RealTime += 300;
            Assert.False(dispatcher.Request(Moment()));
            Assert.Contains("Cannot resolve", host.Notices.Single().Text);
        }

        [Fact]
        public void Errors_ShowAtMostEveryTenMinutes()
        {
            TestEnv.Vars[Settings.PerWeekVar] = "7";
            for (int i = 0; i < 3; i++)
            {
                host.RealTime += 31;
                Assert.True(dispatcher.Request(Moment()));
                transport.Reply(200, Replies.Anthropic("no json here"));
            }
            Assert.Single(host.Notices);
            Assert.Equal(3, TestEnv.Logs.Count);
            host.RealTime += 600;
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(200, Replies.Anthropic("still no json"));
            Assert.Equal(2, host.Notices.Count);
        }

        [Fact]
        public void PostsFromAnotherGame_AreDropped()
        {
            Assert.True(dispatcher.Request(Moment()));
            host.Session = new object();
            transport.Reply(200, Replies.Anthropic(Replies.Posts));
            Assert.Empty(host.Queued);
        }

        [Fact]
        public void MissingKey_ReportsAndStops()
        {
            KeyStore.Delete(Provider.Anthropic);
            Assert.False(dispatcher.Request(Moment()));
            Assert.Empty(transport.Sent);
            Assert.Contains("API key", host.Notices.Single().Text);
            Assert.True(dispatcher.Suspended);
        }

        [Fact]
        public void ErrorNotices_NeverShowTheKey()
        {
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(401, "{\"error\":{\"message\":\"bad key sk-test-1234\"}}");
            Assert.DoesNotContain("sk-test-1234", host.Notices.Single().Text);
        }

        [Fact]
        public void SettingsChange_TestsTheConnectionOnceNextFrame()
        {
            dispatcher.OnSettingsChanged();
            dispatcher.OnSettingsChanged();
            Assert.Single(host.Frames);
            host.RunFrames();
            Assert.Single(transport.Sent);
            Assert.Contains("Connection test", transport.Sent[0].Body);
            transport.Reply(200, Replies.Anthropic("OK"));
            Assert.Contains("connected", host.Notices.Single().Text);
            Assert.False(host.Notices.Single().Error);
        }

        [Fact]
        public void ConnectionTest_DoesntUseUpTheLimits()
        {
            TestEnv.Vars[Settings.PerWeekVar] = "1";
            dispatcher.TestConnection();
            transport.Reply(200, Replies.Anthropic("OK"));
            Assert.True(SendAndReply());
        }

        [Fact]
        public void ConnectionTest_SkippedWhenOff()
        {
            TestEnv.Vars[Settings.EnabledVar] = "0";
            dispatcher.OnSettingsChanged();
            host.RunFrames();
            Assert.Empty(transport.Sent);
        }

        [Fact]
        public void ConnectionTest_ReportsABadKey()
        {
            dispatcher.TestConnection();
            transport.Reply(401, "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}");
            Assert.True(host.Notices.Single().Error);
            Assert.Contains("invalid x-api-key", host.Notices.Single().Text);
        }

        [Fact]
        public void Prompt_UsesTheLanguageHandlesAndOpinions()
        {
            host.LanguageName = "Japanese";
            host.FanHandles["F_HC_T"] = "IdolLover";
            host.FanOpinions["F_HC_T"] = 23;
            dispatcher.Request(Moment());
            string body = transport.Sent.Single().Body;
            Assert.Contains("Write in Japanese.", body);
            Assert.Contains("@IdolLover", body);
            Assert.Contains("opinion of the group: 23/100", body);
        }

        [Fact]
        public void WeekOf_IsTheMonday()
        {
            Assert.Equal(new DateTime(2020, 6, 1), Dispatcher.WeekOf(new DateTime(2020, 6, 7, 23, 0, 0)));
            Assert.Equal(new DateTime(2020, 6, 8), Dispatcher.WeekOf(new DateTime(2020, 6, 8, 0, 1, 0)));
        }
    }
}
