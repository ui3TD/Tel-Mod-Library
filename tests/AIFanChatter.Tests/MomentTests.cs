using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AIFanChatter.Tests
{
    public class MomentTests
    {
        public MomentTests() => TestEnv.Reset();

        [Fact]
        public void Single_GivesItsFacts()
        {
            singles._single single = new() { title = "Summer <color=#f00>Love</color>" };
            single.ReleaseData.Sales = 123456;
            single.ReleaseData.NewFans = 789;
            string text = MomentContext.Single(single).Describe();
            Assert.StartsWith("Event: the group released a new single", text);
            Assert.Contains("Title: Summer Love", text);
            Assert.Contains("Sales: 123456", text);
            Assert.Contains("New fans: 789", text);
        }

        [Fact]
        public void FactsTheGameCantGive_AreLeftOut()
        {
            Moment moment = new Moment("x")
                .Add("Works", () => "yes")
                .Add("Throws", () => throw new System.InvalidOperationException())
                .Add("Null", () => null)
                .Add("Blank", () => "  ");
            Assert.Equal(new[] { "Works" }, moment.Facts.Select(f => f.Key));
        }

        [Fact]
        public void GameText_LosesTagsAndLineBreaks()
        {
            Assert.Equal("Aya and Mei", Moment.Clean("<color=#123456>Aya</color> and\n<b>Mei</b>"));
        }

        [Fact]
        public void Event_TranslatesItsTitle()
        {
            Language.Data["EVENT__TEST_TITLE"] = "A scandal!";
            try
            {
                Event_Manager._activeEvent active = new() { data = new Event_Manager._randomEvent { title = "EVENT__TEST_TITLE" } };
                string text = MomentContext.Event(active, new Event_Manager._randomEvent._reply()).Describe();
                Assert.Contains("What happened: A scandal!", text);
            }
            finally
            {
                Language.Data.Remove("EVENT__TEST_TITLE");
            }
        }

        [Theory]
        [InlineData("en", "English")]
        [InlineData("jp", "Japanese")]
        [InlineData("cn", "Simplified Chinese")]
        [InlineData("ptbr", "Brazilian Portuguese")]
        [InlineData("ru", "Russian")]
        [InlineData("Deutsch", "Deutsch")]
        [InlineData(null, "English")]
        public void LanguageNames(string code, string name)
        {
            Assert.Equal(name, Prompt.LanguageName(code));
        }

        [Fact]
        public void SystemPrompt_ListsEveryFanCodeTheFeedDraws()
        {
            string prompt = Prompt.System("English", Style.Balanced, new Dictionary<string, string>());
            foreach (string code in Prompt.FanCodes)
            {
                Assert.Contains("- " + code + ":", prompt);
            }
            Assert.Equal(12, Prompt.FanCodes.Distinct().Count());
        }

        [Theory]
        [InlineData("M_C_T", "casual teen boy")]
        [InlineData("F_HC_YA", "devoted young adult woman")]
        [InlineData("M_HC_A", "devoted adult man")]
        public void FanCodes_AreDescribed(string code, string description)
        {
            Assert.Equal(description, Prompt.Describe(code));
        }

        /// <summary>
        /// The feed picks the avatar from the code: M or F, HC or C, and T, YA or A.
        /// </summary>
        [Fact]
        public void FanCodes_GiveTheGamesAvatars()
        {
            foreach (string code in Prompt.FanCodes)
            {
                SNS_Manager._message message = new() { User = code };
                Assert.Equal(code.StartsWith("M") ? resources.fanType.male : resources.fanType.female, message.GetGender());
                Assert.Equal(code.Contains("_HC_") ? resources.fanType.hardcore : resources.fanType.casual, message.GetHardcoreness());
            }
        }
    }
}
