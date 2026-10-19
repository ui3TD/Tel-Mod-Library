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

        private bool Send(string schema = Schema) => client.Send("sys", "user", schema, reply => texts.Add(reply.Text));

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
            Assert.StartsWith("Test AI Mod: ", host.Notices[0].Text);
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
        public void Errors_ShowAtMostOnceEvery10Minutes()
        {
            for (int i = 0; i < 3; i++)
            {
                host.RealTime += 60;
                Send();
                transport.Reply(200, Replies.Anthropic("x", "max_tokens"));
            }
            Assert.Single(host.Notices);
            Assert.Equal(3, TestEnv.Logs.Count);
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
        public void ModError_IsReportedLikeTheClientsOwn()
        {
            client.Send("sys", "user", Schema, reply => reply.Error("Nothing usable."));
            Answer();
            Assert.Equal(("Test AI Mod: Nothing usable.", true), host.Notices[0]);
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
            Assert.Equal(("Test AI Mod connected: Anthropic with claude-haiku-5-5", false), host.Notices[0]);
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
