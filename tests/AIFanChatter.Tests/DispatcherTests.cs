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

        /// <summary>
        /// The posts are timed from the moment, when the request went out, not from when the reply came back.
        /// </summary>
        [Fact]
        public void Posts_AreTimedFromTheMoment_NotTheReply()
        {
            DateTime moment = host.GameTime;
            Assert.True(dispatcher.Request(Moment()));
            host.GameTime = moment.AddHours(9);
            transport.Reply(200, Replies.Anthropic(Replies.Posts));
            Assert.Equal(moment, host.QueuedFor.Single());
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
            Assert.Contains("opinion of the group: 23%", body);
        }

        [Fact]
        public void Prompt_CarriesTheRecentPosts()
        {
            host.Posts.Add(TestEnv.Post("F_C_T", "<noparse>earlier post</noparse>"));
            dispatcher.Request(Moment());
            string user = SimpleJSON.JSON.Parse(transport.Sent.Single().Body)["messages"][0]["content"].Value;
            Assert.EndsWith("History: earlier posts on the feed, oldest first, for context only. Don't reply to them or repeat them:\n- F_C_T: earlier post", user);
        }

        [Fact]
        public void EmptyFeed_GivesNoFeedSection()
        {
            dispatcher.Request(Moment());
            Assert.DoesNotContain("earlier posts on the feed", transport.Sent.Single().Body);
        }

        [Fact]
        public void UnreadableFeed_StillSendsTheRequest()
        {
            host.ReadPosts = () => throw new InvalidOperationException("no feed");
            Assert.True(SendAndReply());
            Assert.Single(host.Queued);
            Assert.Contains(TestEnv.Logs, l => l.Contains("Couldn't read the feed"));
        }

        [Fact]
        public void Requests_AskForTheReplySchema_AndLowEffort()
        {
            dispatcher.Request(Moment());
            Assert.Contains("\"output_config\":{\"format\":{\"type\":\"json_schema\"", transport.Sent.Single().Body);
            Assert.Contains("\"effort\":\"low\"", transport.Sent.Single().Body);
            Assert.Equal(Providers.MaxOutputTokens, SimpleJSON.JSON.Parse(transport.Sent.Single().Body)["max_tokens"].AsInt);
        }

        /// <summary>
        /// A model that can't take low effort gets the same request again without it, keeping the schema.
        /// That counts as one request, and the effort isn't asked for again until the settings change.
        /// </summary>
        [Fact]
        public void RejectedEffort_SendsTheRequestAgainWithoutIt_KeepingTheSchema()
        {
            TestEnv.Configure(Provider.Compatible, model: "local-model", baseUrl: "http://localhost:1");
            TestEnv.Vars[Settings.PerWeekVar] = "2";
            Assert.True(dispatcher.Request(Moment()));
            Assert.Contains("\"reasoning_effort\":\"low\"", transport.Sent[0].Body);
            transport.Reply(400, "{\"error\":{\"message\":\"unknown field reasoning_effort\"}}");
            Assert.Equal(2, transport.Sent.Count);
            Assert.DoesNotContain("reasoning", transport.Sent[1].Body);
            Assert.Contains("response_format", transport.Sent[1].Body);
            transport.Reply(200, Replies.OpenAI(Replies.Posts));
            Assert.Single(host.Queued);
            Assert.Empty(host.Notices);
            Assert.Contains(TestEnv.Logs, l => l.Contains("doesn't accept low reasoning effort"));

            // The second try wasn't another request; the next asks for the schema but not the effort
            host.RealTime += 31;
            Assert.True(dispatcher.Request(Moment()));
            Assert.DoesNotContain("reasoning", transport.Sent[2].Body);
            Assert.Contains("response_format", transport.Sent[2].Body);
            transport.Reply(200, Replies.OpenAI(Replies.Posts));

            dispatcher.OnSettingsChanged();
            host.GameTime = host.GameTime.AddDays(7);
            host.RealTime += 31;
            Assert.True(dispatcher.Request(Moment()));
            Assert.Contains("\"reasoning_effort\":\"low\"", transport.Sent[3].Body);
        }

        /// <summary>
        /// A service that rejects the request without the effort too gets it a third time without the schema.
        /// All three count as one request, and neither is asked for again until the settings change.
        /// </summary>
        [Theory]
        [InlineData(400)]
        [InlineData(422)]
        public void RejectedSchema_SendsTheRequestAgainWithoutEffortThenWithoutSchema(long status)
        {
            TestEnv.Vars[Settings.PerWeekVar] = "2";
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(status, "{\"error\":{\"message\":\"output_config is not supported\"}}");
            Assert.Equal(2, transport.Sent.Count);
            Assert.Contains("\"effort\":\"low\"", transport.Sent[0].Body);
            Assert.Contains("\"format\"", transport.Sent[1].Body);
            Assert.DoesNotContain("effort", transport.Sent[1].Body);
            Assert.False(dispatcher.Request(Moment()));
            transport.Reply(status, "{\"error\":{\"message\":\"output_config is not supported\"}}");
            Assert.Equal(3, transport.Sent.Count);
            Assert.DoesNotContain("output_config", transport.Sent[2].Body);
            transport.Reply(200, Replies.Anthropic(Replies.Posts));
            Assert.Single(host.Queued);
            Assert.False(dispatcher.Suspended);
            Assert.Empty(host.Notices);
            Assert.Contains(TestEnv.Logs, l => l.Contains("doesn't accept a reply schema"));

            // The retries weren't other requests: the week still has room for one more, without either
            host.RealTime += 31;
            Assert.True(dispatcher.Request(Moment()));
            Assert.DoesNotContain("output_config", transport.Sent[3].Body);
            transport.Reply(200, Replies.Anthropic(Replies.Posts));

            dispatcher.OnSettingsChanged();
            host.GameTime = host.GameTime.AddDays(7);
            host.RealTime += 31;
            Assert.True(dispatcher.Request(Moment()));
            Assert.Contains("\"effort\":\"low\"", transport.Sent[4].Body);
            Assert.Contains("\"format\"", transport.Sent[4].Body);
        }

        [Fact]
        public void RejectedWithoutEitherToo_StopsRequests()
        {
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(400, "{\"error\":{\"message\":\"bad request\"}}");
            transport.Reply(400, "{\"error\":{\"message\":\"still bad\"}}");
            transport.Reply(400, "{\"error\":{\"message\":\"bad every time\"}}");
            Assert.Equal(3, transport.Sent.Count);
            Assert.True(dispatcher.Suspended);
            Assert.Contains("bad every time", host.Notices.Single().Text);
        }

        [Theory]
        [InlineData(401)]
        [InlineData(429)]
        [InlineData(500)]
        public void OtherErrors_AreNotRetriedWithoutTheSchema(long status)
        {
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(status, "{\"error\":{\"message\":\"nope\"}}");
            Assert.Single(transport.Sent);
        }

        private const string OnePost = "{\"posts\":[{\"user\":\"F_C_T\",\"text\":\"So random.Pass.Pass.Pass\",\"replies\":[]}]}";

        /// <summary>
        /// A single post when the prompt asked for several is taken as a failed reply: the same request goes
        /// again, once, and counts as one request.
        /// </summary>
        [Fact]
        public void OnePost_SendsTheRequestOnceMore()
        {
            TestEnv.Vars[Settings.PerWeekVar] = "2";
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(200, Replies.Anthropic(OnePost));
            Assert.Equal(2, transport.Sent.Count);
            Assert.Equal(transport.Sent[0].Body, transport.Sent[1].Body);
            Assert.Empty(host.Queued);
            Assert.True(dispatcher.InFlight);
            transport.Reply(200, Replies.Anthropic(Replies.Posts));
            Assert.Equal(3, host.Queued.Single().Count);
            Assert.Empty(host.Notices);
            Assert.Contains(TestEnv.Logs, l => l.Contains("only one post"));

            // The second try wasn't another request: the week still has room for one more
            host.RealTime += 31;
            Assert.True(dispatcher.Request(Moment()));
        }

        /// <summary>
        /// A second single post in a row is shown, and single posts aren't retried again until the settings
        /// change, so a model that keeps to one post costs one extra request at most.
        /// </summary>
        [Fact]
        public void OnePostTwice_IsShown_AndNotRetriedUntilTheSettingsChange()
        {
            TestEnv.Vars[Settings.PerWeekVar] = "7";
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(200, Replies.Anthropic(OnePost));
            transport.Reply(200, Replies.Anthropic(OnePost));
            Assert.Equal(2, transport.Sent.Count);
            Assert.Single(host.Queued.Single());
            Assert.False(dispatcher.InFlight);

            host.RealTime += 31;
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(200, Replies.Anthropic(OnePost));
            Assert.Equal(3, transport.Sent.Count);
            Assert.Equal(2, host.Queued.Count);

            dispatcher.OnSettingsChanged();
            host.RealTime += 31;
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(200, Replies.Anthropic(OnePost));
            Assert.Equal(5, transport.Sent.Count(r => r.Body.Contains("Title: Song")));
        }

        [Fact]
        public void SeveralPosts_AreNotRetried()
        {
            Assert.True(SendAndReply());
            Assert.Single(transport.Sent);
        }

        /// <summary>
        /// A prompt without {min} says how many posts in its own words, and may ask for one.
        /// </summary>
        [Fact]
        public void OnePost_NotRetried_WhenThePromptDoesntUseMin()
        {
            Language.Data[Prompt.SystemKey] = "Write 1 post in {language}.";
            Assert.True(SendAndReply(Replies.Anthropic(OnePost)));
            Assert.Single(transport.Sent);
            Assert.Single(host.Queued.Single());
        }

        [Fact]
        public void FailedRetry_ReportsTheError()
        {
            Assert.True(dispatcher.Request(Moment()));
            transport.Reply(200, Replies.Anthropic(OnePost));
            transport.Reply(500, "{\"error\":{\"message\":\"overloaded\"}}");
            Assert.Empty(host.Queued);
            Assert.False(dispatcher.InFlight);
            Assert.Contains("overloaded", host.Notices.Single().Text);
        }

        [Fact]
        public void WeekOf_IsTheMonday()
        {
            Assert.Equal(new DateTime(2020, 6, 1), Dispatcher.WeekOf(new DateTime(2020, 6, 7, 23, 0, 0)));
            Assert.Equal(new DateTime(2020, 6, 8), Dispatcher.WeekOf(new DateTime(2020, 6, 8, 0, 1, 0)));
        }
    }
}
