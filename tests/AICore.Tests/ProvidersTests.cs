using SimpleJSON;
using Xunit;

namespace AICore.Tests
{
    public class ProvidersTests
    {
        public ProvidersTests() => TestEnv.Reset();

        /// <summary>A reply schema with one closed object, as a mod would pass.</summary>
        private const string TestSchema = "{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\"}},\"required\":[\"text\"],\"additionalProperties\":false}";

        [Theory]
        [InlineData(Provider.Anthropic, "", "https://api.anthropic.com/v1/messages")]
        [InlineData(Provider.OpenAI, "", "https://api.openai.com/v1/chat/completions")]
        [InlineData(Provider.Compatible, "http://localhost:11434", "http://localhost:11434/v1/chat/completions")]
        [InlineData(Provider.Compatible, "http://localhost:1234/v1", "http://localhost:1234/v1/chat/completions")]
        [InlineData(Provider.Compatible, "http://localhost:1234/v1/", "http://localhost:1234/v1/chat/completions")]
        [InlineData(Provider.Compatible, "https://openrouter.ai/api/v1", "https://openrouter.ai/api/v1/chat/completions")]
        [InlineData(Provider.Compatible, "https://example.com/custom/chat/completions", "https://example.com/custom/chat/completions")]
        [InlineData(Provider.Anthropic, "https://proxy.example.com", "https://proxy.example.com/v1/messages")]
        [InlineData(Provider.Anthropic, "https://proxy.example.com/v1/messages", "https://proxy.example.com/v1/messages")]
        public void ResolveUrl(Provider provider, string baseUrl, string expected)
        {
            Assert.Equal(expected, Providers.ResolveUrl(new Connection { Provider = provider, BaseUrl = baseUrl }, out string error));
            Assert.Null(error);
        }

        [Theory]
        [InlineData(Provider.Compatible, "")]
        [InlineData(Provider.Compatible, "localhost:11434")]
        [InlineData(Provider.OpenAI, "ftp://example.com")]
        public void ResolveUrl_Unusable(Provider provider, string baseUrl)
        {
            Assert.Null(Providers.ResolveUrl(new Connection { Provider = provider, BaseUrl = baseUrl }, out string error));
            Assert.NotNull(error);
        }

        [Fact]
        public void Anthropic_HeadersAndBody()
        {
            HttpRequestSpec request = Providers.BuildRequest(new Connection { Provider = Provider.Anthropic, ApiKey = "k" }, "sys", "user", 4096, out _);
            Assert.Equal("k", request.Header("x-api-key"));
            Assert.Equal("2023-06-01", request.Header("anthropic-version"));
            Assert.Equal("application/json", request.Header("content-type"));
            Assert.Null(request.Header("Authorization"));

            JSONNode body = JSON.Parse(request.Body);
            Assert.Equal("claude-haiku-5-5", body["model"].Value);
            Assert.Equal("sys", body["system"].Value);
            Assert.Equal("user", body["messages"][0]["role"].Value);
            Assert.Equal("user", body["messages"][0]["content"].Value);
            // A number, not "4096": the API rejects a quoted max_tokens
            Assert.Contains("\"max_tokens\":4096", request.Body);
            Assert.DoesNotContain("temperature", request.Body);
        }

        [Fact]
        public void OpenAI_UsesBearerAndMaxCompletionTokens()
        {
            HttpRequestSpec request = Providers.BuildRequest(new Connection { Provider = Provider.OpenAI, ApiKey = "k", Model = "gpt-x" }, "sys", "user", 100, out _);
            Assert.Equal("Bearer k", request.Header("Authorization"));
            Assert.Contains("\"max_completion_tokens\":100", request.Body);
            Assert.DoesNotContain("\"max_tokens\"", request.Body);
            JSONNode body = JSON.Parse(request.Body);
            Assert.Equal("system", body["messages"][0]["role"].Value);
            Assert.Equal("sys", body["messages"][0]["content"].Value);
            Assert.Equal("user", body["messages"][1]["content"].Value);
        }

        [Fact]
        public void Compatible_WorksWithoutAKeyAndUsesMaxTokens()
        {
            HttpRequestSpec request = Providers.BuildRequest(
                new Connection { Provider = Provider.Compatible, BaseUrl = "http://localhost:11434", Model = "llama3" }, "s", "u", 100, out string error);
            Assert.Null(error);
            Assert.Null(request.Header("Authorization"));
            Assert.Contains("\"max_tokens\":100", request.Body);
        }

        [Theory]
        [InlineData(Provider.Anthropic, null, "", "API key")]
        [InlineData(Provider.OpenAI, null, "gpt-x", "API key")]
        [InlineData(Provider.Compatible, null, "", "Base URL")]
        public void IncompleteSettings_SayWhatsMissing(Provider provider, string key, string model, string missing)
        {
            Assert.Null(Providers.BuildRequest(new Connection { Provider = provider, ApiKey = key, Model = model }, "s", "u", 1, out string error));
            Assert.Contains(missing, error);
        }

        /// <summary>
        /// OpenAI, the default service, needs no model typed in: a blank one is gpt-6-luna.
        /// </summary>
        [Fact]
        public void OpenAI_WithoutAModel_UsesItsDefault()
        {
            HttpRequestSpec request = Providers.BuildRequest(new Connection { Provider = Provider.OpenAI, ApiKey = "k" }, "s", "u", 1, out string error);
            Assert.Null(error);
            Assert.Contains("\"model\":\"gpt-6-luna\"", request.Body);
        }

        [Fact]
        public void Body_EscapesEverythingAndParsesBack()
        {
            string tricky = "Quote \" backslash \\ newline \n tab \t bell \u0001 ナナ 🎤 </color>";
            HttpRequestSpec request = Providers.BuildRequest(new Connection { Provider = Provider.Anthropic, ApiKey = "k" }, tricky, tricky, 1, out _);
            Assert.DoesNotContain("\n", request.Body);
            Assert.DoesNotContain("\u0001", request.Body);
            Assert.Contains("\\u0001", request.Body);
            // SimpleJSON drops raw line breaks but reads escaped ones; the round trip proves the escaping
            Assert.Equal(tricky, JSON.Parse(request.Body)["system"].Value);
        }

        [Fact]
        public void Schema_GoesWhereEachServiceReadsIt()
        {
            HttpRequestSpec anthropic = Providers.BuildRequest(new Connection { Provider = Provider.Anthropic, ApiKey = "k" }, "s", "u", 1, out _, TestSchema);
            JSONNode format = JSON.Parse(anthropic.Body)["output_config"]["format"];
            Assert.Equal("json_schema", format["type"].Value);
            Assert.Equal("object", format["schema"]["type"].Value);
            Assert.Equal("u", JSON.Parse(anthropic.Body)["messages"][0]["content"].Value);

            foreach (Provider provider in new[] { Provider.OpenAI, Provider.Compatible })
            {
                HttpRequestSpec request = Providers.BuildRequest(new Connection { Provider = provider, ApiKey = "k", Model = "m", BaseUrl = "http://localhost:1" }, "s", "u", 1, out _, TestSchema);
                JSONNode responseFormat = JSON.Parse(request.Body)["response_format"];
                Assert.Equal("json_schema", responseFormat["type"].Value);
                Assert.Equal(AIMod.SchemaName, responseFormat["json_schema"]["name"].Value);
                Assert.Contains("\"strict\":true", request.Body);
                Assert.Equal("object", responseFormat["json_schema"]["schema"]["type"].Value);
                Assert.Equal("u", JSON.Parse(request.Body)["messages"][1]["content"].Value);
            }
        }

        [Fact]
        public void NoSchema_LeavesTheBodyAsBefore()
        {
            foreach (Provider provider in new[] { Provider.Anthropic, Provider.OpenAI, Provider.Compatible })
            {
                string body = Providers.BuildRequest(new Connection { Provider = provider, ApiKey = "k", Model = "m", BaseUrl = "http://localhost:1" }, "s", "u", 1, out _).Body;
                Assert.DoesNotContain("output_config", body);
                Assert.DoesNotContain("response_format", body);
                Assert.DoesNotContain("effort", body);
            }
        }

        /// <summary>
        /// Low effort where each service reads it: Anthropic's output_config next to the schema, OpenAI's
        /// reasoning_effort, and for compatible services both the top-level reasoning_effort most of them read
        /// and OpenRouter's reasoning object.
        /// </summary>
        [Fact]
        public void LowEffort_GoesWhereEachServiceReadsIt()
        {
            Connection Of(Provider provider) => new() { Provider = provider, ApiKey = "k", Model = "m", BaseUrl = "http://localhost:1" };

            JSONNode both = JSON.Parse(Providers.BuildRequest(Of(Provider.Anthropic), "s", "u", 1, out _, TestSchema, lowEffort: true).Body)["output_config"];
            Assert.Equal("low", both["effort"].Value);
            Assert.Equal("json_schema", both["format"]["type"].Value);
            JSONNode effortOnly = JSON.Parse(Providers.BuildRequest(Of(Provider.Anthropic), "s", "u", 1, out _, lowEffort: true).Body)["output_config"];
            Assert.Equal("low", effortOnly["effort"].Value);
            Assert.DoesNotContain("format", effortOnly.ToString());

            JSONNode openAI = JSON.Parse(Providers.BuildRequest(Of(Provider.OpenAI), "s", "u", 1, out _, lowEffort: true).Body);
            Assert.Equal("low", openAI["reasoning_effort"].Value);
            Assert.DoesNotContain("\"reasoning\"", openAI.ToString());

            JSONNode compatible = JSON.Parse(Providers.BuildRequest(Of(Provider.Compatible), "s", "u", 1, out _, TestSchema, lowEffort: true).Body);
            Assert.Equal("low", compatible["reasoning"]["effort"].Value);
            Assert.Equal("low", compatible["reasoning_effort"].Value);
            Assert.Equal("json_schema", compatible["response_format"]["type"].Value);
        }
    }

    public class ResponseTests
    {
        private static ProviderReply Parse(Provider provider, long status, string body) =>
            Providers.ParseResponse(provider, new HttpResult { Status = status, Body = body });

        [Fact]
        public void Anthropic_JoinsTextBlocksAndSkipsThinking()
        {
            string body = "{\"content\":[{\"type\":\"thinking\",\"thinking\":\"\",\"signature\":\"x\"},{\"type\":\"text\",\"text\":\"Hello \"},{\"type\":\"text\",\"text\":\"fans\"}],\"stop_reason\":\"end_turn\"}";
            ProviderReply reply = Parse(Provider.Anthropic, 200, body);
            Assert.True(reply.Ok);
            Assert.Equal("Hello fans", reply.Text);
        }

        [Theory]
        [InlineData("refusal", "declined")]
        [InlineData("max_tokens", "ran out")]
        public void Anthropic_StopReasonsThatAreFailures(string stop, string message)
        {
            ProviderReply reply = Parse(Provider.Anthropic, 200, Replies.Anthropic("partial", stop));
            Assert.Equal(Failure.BadReply, reply.Failure);
            Assert.Contains(message, reply.Error);
        }

        [Fact]
        public void Refusals_UseTheModsText()
        {
            Assert.Equal(AIMod.RefusalText, Parse(Provider.Anthropic, 200, Replies.Anthropic("", "refusal")).Error);
            Assert.Equal(AIMod.RefusalText, Parse(Provider.OpenAI, 200,
                "{\"choices\":[{\"message\":{\"content\":null,\"refusal\":\"No.\"},\"finish_reason\":\"stop\"}]}").Error);
        }

        [Fact]
        public void OpenAI_ReadsTheFirstChoice()
        {
            ProviderReply reply = Parse(Provider.OpenAI, 200, Replies.OpenAI("{\"posts\":[]}"));
            Assert.True(reply.Ok);
            Assert.Equal("{\"posts\":[]}", reply.Text);
        }

        [Fact]
        public void OpenAI_LengthAndFilterAreFailures()
        {
            Assert.Equal(Failure.BadReply, Parse(Provider.OpenAI, 200, Replies.OpenAI("x", "length")).Failure);
            Assert.Equal(Failure.BadReply, Parse(Provider.Compatible, 200, Replies.OpenAI("x", "content_filter")).Failure);
        }

        [Fact]
        public void OpenAI_RefusalIsAFailure()
        {
            ProviderReply reply = Parse(Provider.OpenAI, 200, "{\"choices\":[{\"message\":{\"content\":null,\"refusal\":\"I can't help with that.\"},\"finish_reason\":\"stop\"}]}");
            Assert.Equal(Failure.BadReply, reply.Failure);
            Assert.Contains("declined", reply.Error);

            ProviderReply normal = Parse(Provider.Compatible, 200, "{\"choices\":[{\"message\":{\"content\":\"{}\",\"refusal\":null},\"finish_reason\":\"stop\"}]}");
            Assert.True(normal.Ok);
        }

        [Fact]
        public void OpenAI_NullContentIsEmpty()
        {
            ProviderReply reply = Parse(Provider.OpenAI, 200, "{\"choices\":[{\"message\":{\"content\":null},\"finish_reason\":\"stop\"}]}");
            Assert.Equal("", reply.Text);
        }

        [Theory]
        [InlineData(401, Failure.Settings)]
        [InlineData(403, Failure.Settings)]
        [InlineData(404, Failure.Settings)]
        [InlineData(400, Failure.Settings)]
        [InlineData(408, Failure.Transient)]
        [InlineData(429, Failure.Transient)]
        [InlineData(500, Failure.Transient)]
        [InlineData(529, Failure.Transient)]
        public void HttpErrors_AreSettingsOrTransient(long status, Failure expected)
        {
            Assert.Equal(expected, Parse(Provider.Anthropic, status, "").Failure);
        }

        [Fact]
        public void ErrorMessages_FromBothShapes()
        {
            Assert.Contains("invalid x-api-key", Parse(Provider.Anthropic, 401,
                "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}").Error);
            Assert.Contains("model does not exist", Parse(Provider.OpenAI, 404,
                "{\"error\":{\"message\":\"The model does not exist\",\"type\":\"invalid_request_error\"}}").Error);
            Assert.Contains("HTTP 502", Parse(Provider.Compatible, 502, "<html>Bad gateway</html>").Error);
        }

        [Fact]
        public void HtmlPageOn200_IsABadReply()
        {
            Assert.Equal(Failure.BadReply, Parse(Provider.Compatible, 200, "<html>login</html>").Failure);
        }

        [Fact]
        public void NoConnection_IsTransient()
        {
            ProviderReply reply = Providers.ParseResponse(Provider.Anthropic, new HttpResult { Status = 0, NetworkError = "Cannot connect" });
            Assert.Equal(Failure.Transient, reply.Failure);
            Assert.Contains("Cannot connect", reply.Error);
        }

        [Fact]
        public void EmptyBodyOn200_IsABadReply()
        {
            Assert.Equal(Failure.BadReply, Parse(Provider.Anthropic, 200, "").Failure);
        }
    }
}
