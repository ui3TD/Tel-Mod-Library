using System;
using System.Linq;
using Xunit;

namespace AICore.Tests
{
    /// <summary>
    /// Every notice tells the player what happened, with which service and model, for which moment, what to
    /// do, and whether requests go on. None is a bare "something went wrong".
    /// </summary>
    public class NoticesTests
    {
        private static readonly Connection OpenRouter = new()
        {
            Provider = Provider.Compatible,
            BaseUrl = "https://openrouter.ai/api/v1",
            Model = "deepseek/deepseek-v4-pro-0813"
        };

        private static readonly Problem[] All = Enum.GetValues(typeof(Problem)).Cast<Problem>().Where(p => p != Problem.None).ToArray();

        /// <summary>The problems that are about the settings, not a moment.</summary>
        private static readonly Problem[] SettingsProblems = { Problem.KeyRejected, Problem.Forbidden, Problem.NoCredit, Problem.NotFound, Problem.Rejected, Problem.NotJson };

        [Fact]
        public void EveryNotice_NamesTheServiceAndSaysWhatHappensNext()
        {
            foreach (Problem problem in All)
            {
                string text = Notices.Text(problem, "the service's words", 500, OpenRouter, "Kaho's graduation");
                Assert.Contains("openrouter.ai", text);
                bool next = text.Contains(Notices.StartAgain) || text.Contains(Notices.TryingAgain) || text.Contains(Notices.TryNow)
                    || text.Contains(AIMod.OthersGoOn) || text.Contains("The next moment will try again.");
                Assert.True(next, problem + ": " + text);
            }
        }

        [Fact]
        public void MomentNotices_NameTheMoment_SettingsNoticesDont()
        {
            foreach (Problem problem in All)
            {
                string text = Notices.Text(problem, "", 500, OpenRouter, "Kaho's graduation");
                if (SettingsProblems.Contains(problem))
                {
                    Assert.DoesNotContain("Kaho's graduation", text);
                }
                else
                {
                    Assert.StartsWith(AIMod.Missed + " for Kaho's graduation: ", text);
                }
            }
        }

        [Fact]
        public void ModelNotices_NameTheModel()
        {
            foreach (Problem problem in new[] { Problem.OutOfTokens, Problem.EmptyReply, Problem.Unreadable, Problem.Refused, Problem.NotFound })
            {
                Assert.Contains("deepseek/deepseek-v4-pro-0813", Notices.Text(problem, "", 404, OpenRouter, "x"));
            }
        }

        /// <summary>
        /// Only an OpenAI-compatible service uses the Base URL, so only its notices and log text point to it.
        /// </summary>
        [Theory]
        [InlineData(Provider.Anthropic, false)]
        [InlineData(Provider.OpenAI, false)]
        [InlineData(Provider.Compatible, true)]
        public void BaseUrlIsMentioned_OnlyForOpenAICompatible(Provider provider, bool mentioned)
        {
            Connection connection = new() { Provider = provider, BaseUrl = "https://openrouter.ai/api/v1", Model = "some-model" };
            foreach (Problem problem in new[] { Problem.NotFound, Problem.NotJson })
            {
                string text = Notices.Text(problem, "", 404, connection, null);
                Assert.True(mentioned == text.Contains("Base URL"), problem + ": " + text);
            }
            string notFound = Notices.Text(Problem.NotFound, "", 404, connection, null);
            Assert.True(mentioned == notFound.Contains("address"), notFound);
            string error = Providers.ParseResponse(provider, new HttpResult { Status = 200, Body = "<html>login</html>" }).Error;
            Assert.True(mentioned == error.Contains("Base URL"), error);
        }

        /// <summary>
        /// Whatever stops requests says so and says how to start them again.
        /// </summary>
        [Fact]
        public void StoppingNotices_SayHowToStartAgain()
        {
            foreach (Problem problem in All.Where(p => Notices.FailureOf(p) == Failure.Settings || Notices.FailureOf(p) == Failure.BadReply))
            {
                string text = Notices.Text(problem, "", 401, OpenRouter, "x");
                Assert.Contains(Notices.Stopped, text);
                Assert.EndsWith(Notices.StartAgain, text);
            }
        }

        [Fact]
        public void TheServicesOwnWords_AreQuoted_WithTheStatus()
        {
            Assert.Equal("openrouter.ai: out of credit (HTTP 402: Insufficient credits). Requests stopped: add credit, then press Apply in Mod Settings.",
                Notices.Text(Problem.NoCredit, "Insufficient credits.", 402, OpenRouter, "x"));
            Assert.Contains("(HTTP 503: Overloaded)", Notices.Text(Problem.Unavailable, "Overloaded", 503, OpenRouter, "x"));
            Assert.Contains("(HTTP 503)", Notices.Text(Problem.Unavailable, "", 503, OpenRouter, "x"));
        }

        /// <summary>
        /// A 429 would only fail again if the player pressed Apply at once, so it doesn't offer that.
        /// </summary>
        [Fact]
        public void RateLimit_DoesntOfferToTryNow()
        {
            string text = Notices.Text(Problem.RateLimited, "", 429, OpenRouter, "x");
            Assert.EndsWith(Notices.TryingAgain, text);
            Assert.DoesNotContain("retry now", text);
        }

        /// <summary>
        /// The game's notification column is narrow: a notice quotes at most 100 characters of the
        /// service's message, and even the longest stays short.
        /// </summary>
        [Fact]
        public void Notices_StayShort()
        {
            string longMessage = string.Concat(Enumerable.Repeat("The upstream provider returned an error. ", 10));
            Assert.Contains(longMessage.Substring(0, Notices.DetailLength) + "…", Notices.Text(Problem.Unavailable, longMessage, 503, OpenRouter, "x"));
            int longest = All.Max(p => Notices.Text(p, longMessage, 503, OpenRouter, "the event Producer caught dating an idol").Length);
            Assert.True(longest <= 280, "The longest notice has " + longest + " characters");
        }

        [Fact]
        public void ConnectionTestNotices_StartWithACapital_AndKeepNamesAsTheyAre()
        {
            Assert.StartsWith("Can't reach openrouter.ai (Cannot resolve host).", Notices.Text(Problem.NoConnection, "Cannot resolve host", 0, OpenRouter, null));
            Assert.StartsWith("openrouter.ai is unavailable", Notices.Text(Problem.Unavailable, "", 502, OpenRouter, null));
        }
    }
}
