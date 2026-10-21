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
        // Typed without http:// or https://: http for this PC and the local network, https for the rest
        [InlineData(Provider.Compatible, "openrouter.ai/api/v1", "https://openrouter.ai/api/v1/chat/completions")]
        [InlineData(Provider.Compatible, "localhost:11434", "http://localhost:11434/v1/chat/completions")]
        [InlineData(Provider.Compatible, "127.0.0.1:1234/v1", "http://127.0.0.1:1234/v1/chat/completions")]
        [InlineData(Provider.Compatible, "192.168.1.20:11434", "http://192.168.1.20:11434/v1/chat/completions")]
        [InlineData(Provider.Compatible, "172.20.0.5:8000", "http://172.20.0.5:8000/v1/chat/completions")]
        [InlineData(Provider.Compatible, "172.40.0.5", "https://172.40.0.5/v1/chat/completions")]
        public void ResolveUrl(Provider provider, string baseUrl, string expected)
        {
            Assert.Equal(expected, Providers.ResolveUrl(new Connection { Provider = provider, BaseUrl = baseUrl }, out string error));
            Assert.Null(error);
        }

        [Theory]
        [InlineData(Provider.Compatible, "")]
        [InlineData(Provider.OpenAI, "ftp://example.com")]
        [InlineData(Provider.Compatible, "not an address")]
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
        [InlineData("refusal", Problem.Refused, Failure.Declined)]
        [InlineData("max_tokens", Problem.OutOfTokens, Failure.BadReply)]
        [InlineData("model_context_window_exceeded", Problem.OutOfTokens, Failure.BadReply)]
        public void Anthropic_StopReasonsThatAreFailures(string stop, Problem problem, Failure failure)
        {
            ProviderReply reply = Parse(Provider.Anthropic, 200, Replies.Anthropic("partial", stop));
            Assert.Equal(problem, reply.Problem);
            Assert.Equal(failure, reply.Failure);
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
            Assert.Equal(Problem.OutOfTokens, Parse(Provider.OpenAI, 200, Replies.OpenAI("x", "length")).Problem);
            Assert.Equal(Failure.BadReply, Parse(Provider.OpenAI, 200, Replies.OpenAI("x", "length")).Failure);
            Assert.Equal(Problem.Filtered, Parse(Provider.Compatible, 200, Replies.OpenAI("x", "content_filter")).Problem);
            Assert.Equal(Failure.Declined, Parse(Provider.Compatible, 200, Replies.OpenAI("x", "content_filter")).Failure);
        }

        private static string WithNative(string finish, string native) =>
            "{\"choices\":[{\"message\":{\"content\":\"x\"},\"finish_reason\":" + finish + ",\"native_finish_reason\":\"" + native + "\"}]}";

        /// <summary>
        /// Services that don't turn the model's own stop reason into OpenAI's pass it on as it is: in
        /// finish_reason, or, on OpenRouter, in native_finish_reason. Case doesn't matter.
        /// </summary>
        [Theory]
        [InlineData("null", "MAX_TOKENS", Problem.OutOfTokens)]
        [InlineData("\"MAX_TOKENS\"", "MAX_TOKENS", Problem.OutOfTokens)]
        [InlineData("\"stop\"", "max_tokens", Problem.OutOfTokens)]
        [InlineData("null", "SAFETY", Problem.Filtered)]
        [InlineData("\"stop\"", "RECITATION", Problem.Filtered)]
        [InlineData("\"stop\"", "refusal", Problem.Refused)]
        [InlineData("\"error\"", "error", Problem.ProviderFailed)]
        [InlineData("\"stop\"", "STOP", Problem.None)]
        [InlineData("\"stop\"", "end_turn", Problem.None)]
        [InlineData("null", "completed", Problem.None)]
        [InlineData("\"something_new\"", "", Problem.None)]
        public void ProviderStopReasons_AreRead(string finish, string native, Problem expected)
        {
            Assert.Equal(expected, Parse(Provider.Compatible, 200, WithNative(finish, native)).Problem);
        }

        /// <summary>
        /// Some providers put the whole answer in the reasoning field and leave the content empty
        /// (OpenRouter's reasoning, DeepSeek's and vLLM's reasoning_content). Reasoning can hold drafts,
        /// so the last answer in it is used.
        /// </summary>
        [Theory]
        [InlineData("reasoning")]
        [InlineData("reasoning_content")]
        public void AnswerInTheReasoningField_IsUsed(string field)
        {
            string reasoning = Json.Quote("First try: {\"answer\": \"draft\"} no. Final: { \"answer\" : \"final\"}");
            ProviderReply reply = Parse(Provider.Compatible, 200,
                "{\"choices\":[{\"message\":{\"content\":null,\"refusal\":null,\"" + field + "\":" + reasoning + "},\"finish_reason\":\"stop\"}]}");
            Assert.True(reply.Ok);
            Assert.True(reply.FromReasoning);
            Assert.Equal("{ \"answer\" : \"final\"}", reply.Text);
        }

        [Fact]
        public void ReasoningWithoutAnAnswer_IsAnEmptyReply()
        {
            ProviderReply reply = Parse(Provider.Compatible, 200,
                "{\"choices\":[{\"message\":{\"content\":\"\",\"reasoning\":\"Thinking about fans...\"},\"finish_reason\":\"stop\"}]}");
            Assert.True(reply.Ok);
            Assert.False(reply.FromReasoning);
            Assert.Equal("", reply.Text);
        }

        [Fact]
        public void ContentAsAListOfParts_IsJoined()
        {
            ProviderReply reply = Parse(Provider.Compatible, 200,
                "{\"choices\":[{\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Hello \"},{\"type\":\"image_url\"},{\"type\":\"text\",\"text\":\"fans\"}]},\"finish_reason\":\"stop\"}]}");
            Assert.Equal("Hello fans", reply.Text);
        }

        /// <summary>
        /// A provider behind the service can fail after the request started and still send a 2xx: the
        /// service is unavailable, not the model broken. An error field that's only null is no error.
        /// </summary>
        [Fact]
        public void ProviderErrorsInsideA2xx_AreTransient()
        {
            ProviderReply top = Parse(Provider.Compatible, 200, "{\"error\":{\"message\":\"Upstream failed\",\"code\":502}}");
            Assert.Equal(Problem.ProviderFailed, top.Problem);
            Assert.Equal(Failure.Transient, top.Failure);
            Assert.Equal("Upstream failed", top.Detail);

            ProviderReply inChoice = Parse(Provider.Compatible, 200,
                "{\"choices\":[{\"error\":{\"message\":\"Provider crashed\"},\"message\":{\"content\":\"\"},\"finish_reason\":\"error\"}]}");
            Assert.Equal(Problem.ProviderFailed, inChoice.Problem);
            Assert.Equal("Provider crashed", inChoice.Detail);

            ProviderReply nullError = Parse(Provider.Compatible, 200,
                "{\"error\":null,\"choices\":[{\"message\":{\"content\":\"hi\"},\"finish_reason\":\"stop\"}]}");
            Assert.True(nullError.Ok);
            Assert.Equal("hi", nullError.Text);
        }

        [Fact]
        public void OpenAI_RefusalIsAFailure()
        {
            ProviderReply reply = Parse(Provider.OpenAI, 200, "{\"choices\":[{\"message\":{\"content\":null,\"refusal\":\"I can't help with that.\"},\"finish_reason\":\"stop\"}]}");
            Assert.Equal(Failure.Declined, reply.Failure);
            Assert.Equal(Problem.Refused, reply.Problem);
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
        [InlineData(401, Failure.Settings, Problem.KeyRejected)]
        [InlineData(403, Failure.Settings, Problem.Forbidden)]
        [InlineData(402, Failure.Settings, Problem.NoCredit)]
        [InlineData(404, Failure.Settings, Problem.NotFound)]
        [InlineData(400, Failure.Settings, Problem.Rejected)]
        [InlineData(408, Failure.Transient, Problem.Unavailable)]
        [InlineData(429, Failure.Transient, Problem.RateLimited)]
        [InlineData(500, Failure.Transient, Problem.Unavailable)]
        [InlineData(529, Failure.Transient, Problem.Unavailable)]
        public void HttpErrors_AreSettingsOrTransient(long status, Failure expected, Problem problem)
        {
            ProviderReply reply = Parse(Provider.Anthropic, status, "");
            Assert.Equal(expected, reply.Failure);
            Assert.Equal(problem, reply.Problem);
            Assert.Equal(status, reply.Status);
        }

        /// <summary>
        /// OpenRouter answers 404 "Provider returned error" when the model exists but none of its providers
        /// could take the request (a free model at capacity); that passes, so it's a pause, not a stop.
        /// </summary>
        [Fact]
        public void OpenRoutersProvider404_IsTransient()
        {
            ProviderReply reply = Parse(Provider.Compatible, 404, "{\"error\":{\"message\":\"Provider returned error\",\"code\":404}}");
            Assert.Equal(Problem.Unavailable, reply.Problem);
            Assert.Equal(Failure.Transient, reply.Failure);
            Assert.Equal(Problem.NotFound, Parse(Provider.Compatible, 404, "{\"error\":{\"message\":\"x is not a valid model ID\"}}").Problem);
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

        /// <summary>
        /// A web page instead of a reply means the Base URL points somewhere else: the settings.
        /// </summary>
        [Fact]
        public void HtmlPageOn200_IsTheSettings()
        {
            ProviderReply reply = Parse(Provider.Compatible, 200, "<html>login</html>");
            Assert.Equal(Failure.Settings, reply.Failure);
            Assert.Equal(Problem.NotJson, reply.Problem);
        }

        [Fact]
        public void NoConnection_IsTransient()
        {
            ProviderReply reply = Providers.ParseResponse(Provider.Anthropic, new HttpResult { Status = 0, NetworkError = "Cannot connect" });
            Assert.Equal(Failure.Transient, reply.Failure);
            Assert.Contains("Cannot connect", reply.Error);
        }

        /// <summary>
        /// An empty 2xx is the service's or a proxy's doing, not the model's.
        /// </summary>
        [Fact]
        public void EmptyBodyOn200_IsTransient()
        {
            ProviderReply reply = Parse(Provider.Anthropic, 200, "");
            Assert.Equal(Failure.Transient, reply.Failure);
            Assert.Equal(Problem.EmptyAnswer, reply.Problem);
        }
    }
}
