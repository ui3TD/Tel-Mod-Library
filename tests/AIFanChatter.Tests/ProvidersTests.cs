using SimpleJSON;
using System.Linq;
using Xunit;

namespace AIFanChatter.Tests
{
    public class ProvidersTests
    {
        public ProvidersTests() => TestEnv.Reset();

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
            Assert.Equal("claude-haiku-4-5", body["model"].Value);
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
        [InlineData(Provider.OpenAI, "k", "", "model")]
        [InlineData(Provider.OpenAI, null, "gpt-x", "API key")]
        [InlineData(Provider.Compatible, null, "", "Base URL")]
        public void IncompleteSettings_SayWhatsMissing(Provider provider, string key, string model, string missing)
        {
            Assert.Null(Providers.BuildRequest(new Connection { Provider = provider, ApiKey = key, Model = model }, "s", "u", 1, out string error));
            Assert.Contains(missing, error);
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

    public class PostParserTests
    {
        private static readonly System.Random Random = new(7);

        [Fact]
        public void ParsesPostsAndReplies()
        {
            var posts = PostParser.Parse(Replies.Posts, Random);
            Assert.Equal(3, posts.Count);
            Assert.Equal("F_HC_T", posts[0].User);
            Assert.Equal("<noparse>Best single yet!</noparse>", posts[0].Message);
            Assert.Equal("<noparse>Agreed</noparse>", posts[0].Replies.Single().Message);
            Assert.Empty(posts[1].Replies);
        }

        [Fact]
        public void FindsTheJsonInsideProseAndCodeFences()
        {
            Assert.Equal(3, PostParser.Parse("Here you go:\n```json\n" + Replies.Posts + "\n```\nEnjoy!", Random).Count);
        }

        [Fact]
        public void UnknownFanCodes_BecomeValidOnes()
        {
            var posts = PostParser.Parse("{\"posts\":[{\"user\":\"superfan99\",\"text\":\"hi\"},{\"user\":\"f_hc_ya\",\"text\":\"yo\"}]}", Random);
            Assert.Contains(posts[0].User, Prompt.FanCodes);
            Assert.Equal("F_HC_YA", posts[1].User);
        }

        /// <summary>
        /// The feed's text renders tags; inside noparse they show as typed, and a closing noparse can't escape it.
        /// </summary>
        [Fact]
        public void AngleBrackets_ShowAsTyped()
        {
            string text = PostParser.Parse("{\"posts\":[{\"user\":\"M_C_T\",\"text\":\"<color=red>wow</color> <3 </noparse><b>\"}]}", Random)[0].Message;
            Assert.Equal("<noparse><color=red>wow</color> <3 <b></noparse>", text);
        }

        [Fact]
        public void CountsAndLengths_AreCapped()
        {
            string longText = new('a', 500);
            string replies = string.Join(",", Enumerable.Repeat("{\"user\":\"M_C_T\",\"text\":\"r\"}", 10));
            string post = "{\"user\":\"F_C_T\",\"text\":\"" + longText + "\",\"replies\":[" + replies + "]}";
            var posts = PostParser.Parse("{\"posts\":[" + string.Join(",", Enumerable.Repeat(post, 10)) + "]}", Random);
            Assert.Equal(Prompt.MaxPosts, posts.Count);
            Assert.Equal(Prompt.MaxReplies, posts[0].Replies.Count);
            Assert.Equal(Prompt.MaxLength, PostParser.Clean(longText).Length);
            Assert.EndsWith("...", PostParser.Clean(longText));
            Assert.Equal(PostParser.ForFeed(PostParser.Clean(longText)), posts[0].Message);
        }

        /// <summary>
        /// The feed's font has no emoji and no heart (they'd show as boxes), but has ♪ and ★.
        /// </summary>
        [Fact]
        public void Emoji_AreRemoved()
        {
            Assert.Equal("so good! <3 ナナ♪★", PostParser.Clean("so good! 🎤❤️ ナナ♪★ 👩‍🎤"));
        }

        [Fact]
        public void LineBreaks_BecomeSpaces()
        {
            Assert.Equal("one two three", PostParser.Clean("one\ntwo\r\n\tthree"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Sorry, I can't help with that.")]
        [InlineData("{\"posts\":[]}")]
        [InlineData("{\"posts\":[{\"user\":\"M_C_T\",\"text\":\"\"}]}")]
        [InlineData("{\"posts\":\"none\"}")]
        [InlineData("{\"posts\":[{\"text\":\"broken \\u12")]
        [InlineData("}{")]
        public void UnusableReplies_GiveNoPosts(string text)
        {
            Assert.Empty(PostParser.Parse(text, Random));
        }
    }
}
