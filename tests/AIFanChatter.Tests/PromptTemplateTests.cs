using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace AIFanChatter.Tests
{
    public class PromptTemplateTests
    {
        public PromptTemplateTests() => TestEnv.Reset();

        private static readonly Regex Placeholder = new(@"\{[a-z_]+\}");

        private static Dictionary<string, Func<string>> Modules(params (string Name, string Text)[] modules) =>
            modules.ToDictionary(m => m.Name, m => (Func<string>)(() => m.Text));

        [Fact]
        public void Fill_ReplacesKnownPlaceholders_AndKeepsUnknownOnes()
        {
            Assert.Equal("Hi Aya, {unknown} stays", PromptTemplate.Fill("Hi {name}, {unknown} stays", Modules(("name", "Aya"))));
        }

        [Fact]
        public void Fill_AModuleThatFails_GivesNothing_AndTheOthersStay()
        {
            Dictionary<string, Func<string>> modules = new()
            {
                ["a"] = () => throw new InvalidOperationException("broken"),
                ["b"] = () => "B"
            };
            Assert.Equal("B", PromptTemplate.Fill("{a}\n{b}", modules));
            Assert.Contains("a part of the prompt", Assert.Single(TestEnv.Logs));
        }

        [Fact]
        public void Fill_DropsLinesOfEmptyModules_AndCollapsesBlankLines()
        {
            string text = PromptTemplate.Fill("Top\n{a}\n\n{b}\n\n{c}\nEnd", Modules(("a", ""), ("b", ""), ("c", "C")));
            Assert.Equal("Top\n\nC\nEnd", text);
        }

        [Fact]
        public void Fill_RunsEachModuleOnce()
        {
            int runs = 0;
            PromptTemplate.Fill("{a} and {a}", new Dictionary<string, Func<string>> { ["a"] = () => (++runs).ToString() });
            Assert.Equal(1, runs);
        }

        [Fact]
        public void Wrap_PutsLinesInTheirWrapper_AndNeverLeavesAHeaderAlone()
        {
            Language.Data[PromptTemplate.WrapperPrefix + "TEST"] = "Header:\n{lines}";
            Assert.Equal("Header:\nline", PromptTemplate.Wrap("test", "line"));
            Assert.Equal("", PromptTemplate.Wrap("test", ""));
            Assert.Equal("line", PromptTemplate.Wrap("nowrapper", "line"));
        }

        /// <summary>
        /// The new event comes first under its own label, and both kinds of history come after it, each
        /// labelled as history, so the posts stay about the new event.
        /// </summary>
        [Fact]
        public void UserMessage_PutsTheNewEventFirst_AndLabelsHistory()
        {
            Moment moment = new Moment("a test").Add("Title", () => "Song");
            moment.AddTo(Moment.HistorySection, "Previous single", () => "Old Song");
            string text = Prompt.User(moment, new List<SNS_Manager._message> { TestEnv.Post("F_C_T", "<noparse>earlier post</noparse>") });

            Assert.StartsWith("The new event, which every post reacts to:\nEvent: a test\nTitle: Song", text);
            int history = text.IndexOf("History, for context only (not what the posts are about):\nPrevious single: Old Song", StringComparison.Ordinal);
            int feed = text.IndexOf("History: earlier posts on the feed, oldest first, for context only. Don't reply to them or repeat them:\n- F_C_T: earlier post", StringComparison.Ordinal);
            Assert.True(history > text.IndexOf("Title: Song", StringComparison.Ordinal), text);
            Assert.True(feed > history, text);
        }

        [Fact]
        public void UserMessage_WithoutHistory_HasNoEmptyHeaders()
        {
            string text = Prompt.User(new Moment("a test").Add("Title", () => "Song"), null);
            Assert.Equal("The new event, which every post reacts to:\nEvent: a test\nTitle: Song", text);
            Assert.Empty(TestEnv.Logs);
        }

        [Fact]
        public void MissingUserTemplate_UsesThePlainLayout()
        {
            Language.Data.Remove(Prompt.UserKey);
            string text = Prompt.User(new Moment("a test").Add("Title", () => "Song"), null);
            Assert.Equal("Event: a test\nTitle: Song", text);
            Assert.Single(TestEnv.Logs);
        }

        [Fact]
        public void ShippedTemplates_HoldEveryModule()
        {
            string system = Language.Data[Prompt.SystemKey];
            foreach (string module in new[] { "{fan_types}", "{format}", "{min}", "{max}", "{replies}", "{length}", "{language}" })
            {
                Assert.Contains(module, system);
            }
            string user = Language.Data[Prompt.UserKey];
            foreach (string module in new[] { "{event}", "{facts}", "{agency}", "{members}", "{history}", "{feed}" })
            {
                Assert.Contains(module, user);
            }
            Assert.True(user.IndexOf("{event}", StringComparison.Ordinal) < user.IndexOf("{history}", StringComparison.Ordinal));
            Assert.True(user.IndexOf("{event}", StringComparison.Ordinal) < user.IndexOf("{feed}", StringComparison.Ordinal));
            foreach (string wrapper in new[] { "MEMBERS", "HISTORY", "FEED" })
            {
                Assert.Contains("{lines}", Language.Data[PromptTemplate.WrapperPrefix + wrapper]);
            }
            Assert.StartsWith("History", Language.Data[PromptTemplate.WrapperPrefix + "HISTORY"]);
            Assert.StartsWith("History", Language.Data[PromptTemplate.WrapperPrefix + "FEED"]);
        }

        [Fact]
        public void ShippedTemplates_LeaveNoPlaceholdersBehind()
        {
            Moment moment = new Moment("a test").Add("Title", () => "Song");
            moment.AddTo(Moment.MembersSection, "Aya", () => "Age 19.");
            moment.AddTo(Moment.HistorySection, "Previous single", () => "Old Song");
            moment.AddTo(Moment.AgencySection, "Group", () => "Stars");
            string user = Prompt.User(moment, new List<SNS_Manager._message> { TestEnv.Post("F_C_T", "hi") });
            string system = Prompt.System("English", Prompt.FanCodes, null, null);
            Assert.DoesNotMatch(Placeholder, user);
            Assert.DoesNotMatch(Placeholder, system);
            Assert.Empty(TestEnv.Logs);
        }

        [Fact]
        public void ShippedRules_KeepFansOnTheNewEvent()
        {
            string system = Prompt.System("English", Prompt.FanCodes, null, null);
            Assert.Contains("\n- Every post reacts to the new event. The background and the earlier posts are history", system);
            Assert.Contains("\n- Stamina and mental stamina aren't public: fans only notice signs", system);
        }
    }
}
