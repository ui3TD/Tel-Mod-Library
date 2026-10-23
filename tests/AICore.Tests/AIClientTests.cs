using SimpleJSON;
using System;
using System.Collections.Generic;
using Xunit;

namespace AICore.Tests
{
    /// <summary>
    /// The client on its own, as any AI mod uses it: limits, failures, the schema and effort fallback,
    /// retries and connection tests.
    /// </summary>
    public class AIClientTests
    {
        private const string Schema = "{\"type\":\"object\"}";

        private readonly FakeHost host = new();
        private readonly FakeTransport transport = new();
        private readonly AIClient client;
        private readonly List<string> texts = new();

        public AIClientTests()
        {
            TestEnv.Reset();
            TestEnv.Configure();
            client = new AIClient(host, transport);
        }

        private bool Send(string schema = Schema) => client.Send("sys", "user", schema, reply => texts.Add(reply.Text), "the test moment");

        private void Answer(string text = "hello") => transport.Reply(200, Replies.Anthropic(text));

        [Fact]
        public void Reply_ReachesTheMod()
        {
            Assert.True(Send());
            Assert.True(client.InFlight);
            Answer("the text");
            Assert.False(client.InFlight);
            Assert.Equal(new[] { "the text" }, texts);
        }

        [Fact]
        public void Off_OrNoRequestsPerWeek_SendsNothing()
        {
            host.Enabled = false;
            Assert.Equal("the mod is off", client.Blocked());
            Assert.False(Send());
            host.Enabled = true;
            host.PerWeek = 0;
            Assert.False(Send());
            Assert.Empty(transport.Sent);
        }

        [Fact]
        public void Limits_OneAtATime_30SecondsApart_20AnHour()
        {
            Assert.True(Send());
            Assert.False(Send());
            Answer();
            Assert.False(Send());
            for (int i = 1; i < AIClient.MaxPerHour; i++)
            {
                host.RealTime += AIClient.MinGapSeconds;
                host.GameTime = host.GameTime.AddDays(7);
                Assert.True(Send());
                Answer();
            }
            host.RealTime += AIClient.MinGapSeconds;
            host.GameTime = host.GameTime.AddDays(7);
            Assert.Equal("20 requests were sent in the last hour", client.Blocked());
            host.RealTime += 3600;
            Assert.Null(client.Blocked());
        }

        [Fact]
        public void WeeklyLimit_StartsAgainOnMonday()
        {
            host.PerWeek = 1;
            // Wednesday
            Assert.True(Send());
            Answer();
            host.RealTime += 60;
            Assert.Equal("this week's requests are used up", client.Blocked());
            host.GameTime = host.GameTime.AddDays(5);
            Assert.Null(client.Blocked());
        }

        [Fact]
        public void MissingSettings_StopRequestsUntilTheyChange()
        {
            KeyStore.Delete(Provider.Anthropic);
            Assert.False(Send());
            Assert.True(client.Suspended);
            Assert.Contains("API key", host.Notices[0].Text);
            // The host adds the mod's name where it shows the notice
            Assert.DoesNotContain("Test AI Mod", host.Notices[0].Text);
            Assert.Equal(NoticeKind.Stopped, host.Notices[0].Kind);
            Assert.False(host.Notices[0].FromApply);
            client.OnSettingsChanged("s", "u");
            Assert.False(client.Suspended);
        }

        [Fact]
        public void RejectedKey_StopsRequests_BusyServicePausesThem()
        {
            Send();
            transport.Reply(401, "{\"error\":{\"message\":\"invalid x-api-key\"}}");
            Assert.True(client.Suspended);
            Assert.Empty(texts);

            client.OnSettingsChanged("s", "u");
            host.RealTime += 60;
            Send();
            transport.NoConnection();
            Assert.Equal("waiting after the service was unavailable", client.Blocked());
            host.RealTime += AIClient.BackoffSeconds;
            Assert.Null(client.Blocked());
        }

        [Fact]
        public void RejectedKey_SaysWhatToDo()
        {
            Send();
            transport.Reply(401, "{\"error\":{\"message\":\"invalid x-api-key\"}}");
            Assert.Equal("Anthropic rejected the API key (HTTP 401: invalid x-api-key). Requests stopped: "
                + "fix the key, then press Apply in Mod Settings.", host.Notices[0].Text);
            Assert.Equal(NoticeKind.Stopped, host.Notices[0].Kind);
        }

        [Fact]
        public void EveryError_IsReported()
        {
            // Refusals only cost their own request, so requests go on and each one fails again
            for (int i = 0; i < 3; i++)
            {
                host.RealTime += 60;
                Send();
                transport.Reply(200, Replies.Anthropic("", "refusal"));
            }
            Assert.Equal(3, host.Notices.Count);
            Assert.All(host.Notices, n => Assert.Equal(NoticeKind.Missed, n.Kind));
            Assert.Equal(3, TestEnv.Logs.Count);
        }

        /// <summary>
        /// A model that runs out of room will keep doing so: requests stop, and the notice says so and what to do.
        /// </summary>
        [Fact]
        public void OutOfRoom_StopsRequests_UntilApply()
        {
            Send();
            transport.Reply(200, Replies.Anthropic("partial", "max_tokens"));
            Assert.True(client.Suspended);
            Assert.Equal("No test answer for the test moment: claude-haiku-5-5 on Anthropic hit its 4096-token limit. "
                + "Requests stopped: choose another model, then press Apply in Mod Settings.", host.Notices[0].Text);
            Assert.Equal(NoticeKind.Stopped, host.Notices[0].Kind);
            host.RealTime += 3600;
            Assert.Equal("requests are stopped until the settings change", client.Blocked());

            client.OnApplied("s", "u");
            Assert.False(client.Suspended);
            Assert.Null(client.Blocked());
            Assert.Single(host.Frames);
        }

        [Fact]
        public void Refusal_OnlyCostsThatRequest()
        {
            Send();
            transport.Reply(200, Replies.Anthropic("", "refusal"));
            Assert.False(client.Suspended);
            Assert.Equal("No test answer for the test moment: claude-haiku-5-5 on Anthropic declined. "
                + "Other tests will still run.", host.Notices[0].Text);
            Assert.Equal(NoticeKind.Missed, host.Notices[0].Kind);
            host.RealTime += 60;
            Assert.Null(client.Blocked());
        }

        [Fact]
        public void StopNotice_ShowsEvenRightAfterAnotherNotice()
        {
            Send();
            transport.Reply(200, Replies.Anthropic("", "refusal"));
            host.RealTime += 60;
            Send();
            transport.Reply(200, Replies.Anthropic("", "max_tokens"));
            Assert.Equal(2, host.Notices.Count);
            Assert.Contains(Notices.Stopped, host.Notices[1].Text);
        }

        [Fact]
        public void ProviderFailureInsideA200_PausesRequests()
        {
            TestEnv.Configure(Provider.Compatible, model: "some/model", baseUrl: "https://openrouter.ai/api/v1");
            Send();
            transport.Reply(200, "{\"error\":{\"message\":\"Upstream provider timed out\",\"code\":502}}");
            Assert.False(client.Suspended);
            Assert.Equal("waiting after the service was unavailable", client.Blocked());
            Assert.Equal("No test answer for the test moment: openrouter.ai's provider failed (Upstream provider timed out). "
                + Notices.TryNow, host.Notices[0].Text);
            Assert.Equal(NoticeKind.Paused, host.Notices[0].Kind);
            Assert.True(client.Paused);
        }

        /// <summary>
        /// Pressing Apply with nothing changed lifts a stop or a pause and tests the connection; with
        /// nothing stopped it does nothing, so it costs nothing.
        /// </summary>
        [Fact]
        public void Apply_LiftsAPause_AndDoesNothingOtherwise()
        {
            client.OnApplied("s", "u");
            Assert.Empty(host.Frames);

            Send();
            transport.NoConnection();
            Assert.NotNull(client.Blocked());
            client.OnApplied("s", "u");
            host.RealTime += 60;
            Assert.Null(client.Blocked());
            host.RunFrames();
            Assert.Single(transport.Sent, r => r.Body.Contains("\"max_tokens\":" + AIClient.TestMaxTokens));
        }

        /// <summary>
        /// A model that reasons always runs out of room on the tiny test, and a refusal still shows the
        /// settings work: both are "connected", and neither stops requests.
        /// </summary>
        [Theory]
        [InlineData("max_tokens")]
        [InlineData("refusal")]
        public void ConnectionTest_CutShortOrDeclined_IsConnected(string stop)
        {
            client.OnSettingsChanged("s", "u");
            host.RunFrames();
            transport.Reply(200, Replies.Anthropic("", stop));
            Assert.Equal("Anthropic with claude-haiku-5-5", host.Notices[0].Text);
            Assert.Equal(NoticeKind.Connected, host.Notices[0].Kind);
            Assert.True(host.Notices[0].FromApply);
            Assert.False(client.Suspended);
        }

        [Fact]
        public void AnswerInTheReasoningField_IsUsed_AndLogged()
        {
            TestEnv.Configure(Provider.Compatible, model: "some/model", baseUrl: "https://openrouter.ai/api/v1");
            Send();
            transport.Reply(200, "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":null,\"refusal\":null,"
                + "\"reasoning\":\"Let me write it. {\\\"answer\\\":\\\"42\\\"}\"}}]}");
            Assert.Equal(new[] { "{\"answer\":\"42\"}" }, texts);
            Assert.Contains(TestEnv.Logs, l => l.Contains("reasoning field"));
            Assert.Empty(host.Notices);
        }

        [Fact]
        public void RejectedEffort_ThenSchema_AreDroppedUntilTheSettingsChange()
        {
            Send();
            Assert.Contains("\"effort\"", transport.Sent[0].Body);
            transport.Reply(400, "{\"error\":{\"message\":\"effort not supported\"}}");
            Assert.DoesNotContain("\"effort\"", transport.Sent[1].Body);
            Assert.Contains("json_schema", transport.Sent[1].Body);
            transport.Reply(400, "{\"error\":{\"message\":\"schema not supported\"}}");
            Assert.DoesNotContain("json_schema", transport.Sent[2].Body);
            Answer();
            Assert.Single(texts);

            host.RealTime += 60;
            Send();
            Assert.DoesNotContain("json_schema", transport.Sent[3].Body);
            Answer();

            client.OnSettingsChanged("s", "u");
            host.RealTime += 60;
            Send();
            Assert.Contains("json_schema", transport.Sent[4].Body);
            Assert.Contains("\"effort\"", transport.Sent[4].Body);
        }

        /// <summary>
        /// A compatible service that rejects OpenRouter's reasoning setting (Google's) gets the request again
        /// with only the standard reasoning_effort and the schema, and keeps getting it that way until the
        /// settings change.
        /// </summary>
        [Fact]
        public void RejectedRouterEffort_KeepsTheStandardEffort()
        {
            TestEnv.Configure(Provider.Compatible, model: "gemini-4-argon", baseUrl: "https://generativelanguage.googleapis.com/v1beta/openai");
            Send();
            Assert.Contains("\"reasoning\":{", transport.Sent[0].Body);
            transport.Reply(400, "[{\"error\":{\"code\":400,\"message\":\"Unknown name \\\"reasoning\\\": Cannot find field.\"}}]");
            Assert.Equal(2, transport.Sent.Count);
            Assert.DoesNotContain("\"reasoning\":{", transport.Sent[1].Body);
            Assert.Contains("\"reasoning_effort\":\"low\"", transport.Sent[1].Body);
            Assert.Contains("json_schema", transport.Sent[1].Body);
            transport.Reply(200, Replies.OpenAI("hello"));
            Assert.Single(texts);
            Assert.Empty(host.Notices);

            // The next request goes straight through with the standard effort
            host.RealTime += 60;
            Send();
            Assert.Equal(3, transport.Sent.Count);
            Assert.DoesNotContain("\"reasoning\":{", transport.Sent[2].Body);
            Assert.Contains("\"reasoning_effort\":\"low\"", transport.Sent[2].Body);
            transport.Reply(200, Replies.OpenAI("again"));

            client.OnSettingsChanged("s", "u");
            host.RealTime += 60;
            Send();
            Assert.Contains("\"reasoning\":{", transport.Sent[transport.Sent.Count - 1].Body);
        }

        /// <summary>
        /// A compatible service that takes no effort setting and no schema: OpenRouter's setting goes first,
        /// then the standard effort, then the schema, all as the same request.
        /// </summary>
        [Fact]
        public void CompatibleFallback_DropsOneThingAtATime()
        {
            TestEnv.Configure(Provider.Compatible, model: "some/model", baseUrl: "https://example.com/v1");
            host.PerWeek = 1;
            Send();
            transport.Reply(400, "{\"error\":{\"message\":\"no reasoning\"}}");
            Assert.Contains("\"reasoning_effort\"", transport.Sent[1].Body);
            transport.Reply(400, "{\"error\":{\"message\":\"no reasoning_effort\"}}");
            Assert.DoesNotContain("reasoning_effort", transport.Sent[2].Body);
            Assert.Contains("json_schema", transport.Sent[2].Body);
            transport.Reply(422, "{\"error\":{\"message\":\"no schema\"}}");
            Assert.DoesNotContain("json_schema", transport.Sent[3].Body);
            transport.Reply(200, Replies.OpenAI("hello"));
            Assert.Single(texts);
            // The four attempts counted as one request
            host.RealTime += 60;
            Assert.Equal("this week's requests are used up", client.Blocked());
        }

        [Fact]
        public void NoSchema_IsNeverAskedFor()
        {
            client.Send("sys", "user", null, reply => texts.Add(reply.Text));
            Assert.DoesNotContain("json_schema", transport.Sent[0].Body);
        }

        [Fact]
        public void Retry_SendsTheSameRequest_AsTheSameRequest()
        {
            host.PerWeek = 1;
            int replies = 0;
            client.Send("sys", "user", Schema, reply =>
            {
                replies++;
                if (replies == 1)
                {
                    reply.Retry(again => texts.Add(again.Text));
                }
            });
            Answer("first");
            Assert.True(client.InFlight);
            Assert.Equal(transport.Sent[0].Body, transport.Sent[1].Body);
            Answer("second");
            Assert.Equal(new[] { "second" }, texts);
            Assert.False(client.InFlight);
            host.RealTime += 60;
            Assert.Equal("this week's requests are used up", client.Blocked());
        }

        [Fact]
        public void UnusableReply_StopsRequests_WithTheModsNotice()
        {
            client.Send("sys", "user", Schema, reply => reply.Unusable(Problem.Unreadable, "Nothing usable."), "the test moment");
            Answer();
            Assert.True(client.Suspended);
            Assert.Equal("No test answer for the test moment: claude-haiku-5-5 on Anthropic didn't use the reply format. "
                + "Requests stopped: choose another model, then press Apply in Mod Settings.", host.Notices[0].Text);
            Assert.Equal(NoticeKind.Stopped, host.Notices[0].Kind);
        }

        [Fact]
        public void ThrowingHandler_IsLoggedAndFreesTheClient()
        {
            client.Send("sys", "user", Schema, reply => throw new InvalidOperationException("boom"));
            Answer();
            Assert.False(client.InFlight);
            Assert.Contains("boom", TestEnv.Logs[0]);
        }

        [Fact]
        public void SettingsChange_RaisesTheEvent_AndTestsTheConnectionNextFrame()
        {
            int raised = 0;
            client.SettingsChanged += () => raised++;
            client.OnSettingsChanged("test system", "test user");
            client.OnSettingsChanged("test system", "test user");
            Assert.Equal(2, raised);
            Assert.Single(host.Frames);
            host.RunFrames();
            JSONNode body = JSON.Parse(transport.Sent[0].Body);
            Assert.Equal("test system", body["system"].Value);
            Assert.Equal(AIClient.TestMaxTokens, body["max_tokens"].AsInt);
            Answer("OK");
            Assert.Equal("Anthropic with claude-haiku-5-5", host.Notices[0].Text);
            Assert.Equal(NoticeKind.Connected, host.Notices[0].Kind);
            Assert.True(host.Notices[0].FromApply);
            // The test doesn't count towards the limits
            Assert.Null(client.Blocked());
        }

        [Fact]
        public void SettingsChange_WhileOff_TestsNothing()
        {
            host.Enabled = false;
            client.OnSettingsChanged("s", "u");
            host.RunFrames();
            Assert.Empty(transport.Sent);
        }

        [Fact]
        public void WeekOf_IsTheMonday()
        {
            Assert.Equal(new DateTime(2020, 6, 1), AIClient.WeekOf(new DateTime(2020, 6, 7, 23, 0, 0)));
            Assert.Equal(new DateTime(2020, 6, 8), AIClient.WeekOf(new DateTime(2020, 6, 8)));
        }
    }
}
