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
            Assert.StartsWith("The new event, which every post reacts to:\nEvent: the group released a new single", text);
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

        /// <summary>
        /// A nicknamed idol goes as her nickname with her full name after it, so fans can use either.
        /// </summary>
        [Theory]
        [InlineData("Momo", "Yasuda Momone", "Momo (Yasuda Momone)")]
        [InlineData("Momo", "<color=#4a7ccf>Yasuda Momone</color>", "Momo (Yasuda Momone)")]
        [InlineData("Momo", "", "Momo")]
        [InlineData("Momo", null, "Momo")]
        [InlineData("Yasuda Momone", "Yasuda Momone", "Yasuda Momone")]
        public void Nickname_KeepsTheFullName(string nickname, string fullName, string expected)
        {
            Assert.Equal(expected, MomentContext.WithNickname(nickname, fullName));
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
            string prompt = Prompt.System("English", new Dictionary<string, string>(), null);
            foreach (string code in Prompt.FanCodes)
            {
                Assert.Contains("- " + code + ":", prompt);
            }
            Assert.Equal(12, Prompt.FanCodes.Distinct().Count());
        }

        /// <summary>
        /// The prompt's wording comes from constants.json, with every placeholder filled in and its line breaks kept.
        /// </summary>
        [Fact]
        public void SystemPrompt_UsesTheConstantsText()
        {
            string prompt = Prompt.System("Japanese", new Dictionary<string, string>(), null);
            Assert.StartsWith("You write fan posts for the social media feed of an idol management game.", prompt);
            Assert.Contains("\n- Write 3 to 6 posts reacting to the event", prompt);
            Assert.Contains("0 to 3 replies", prompt);
            Assert.Contains("under 100 characters", prompt);
            Assert.Contains("- Write in Japanese.", prompt);
            Assert.Contains("- Match each fan's tone to their opinion of the group", prompt);
            Assert.DoesNotContain("real people", prompt);
            foreach (string placeholder in new[] { "{min}", "{max}", "{replies}", "{length}", "{language}" })
            {
                Assert.DoesNotContain(placeholder, prompt);
            }
            Assert.EndsWith("{\"posts\":[{\"user\":\"F_HC_T\",\"text\":\"...\",\"replies\":[{\"user\":\"M_C_A\",\"text\":\"...\"}]}]}", prompt);
            Assert.Empty(TestEnv.Logs);
        }

        [Fact]
        public void EveryPromptText_IsInConstants()
        {
            foreach (string key in new[] { Prompt.SystemKey, Prompt.UserKey, "AIFANCHATTER__PROMPT__MEMBERS", "AIFANCHATTER__PROMPT__HISTORY", "AIFANCHATTER__PROMPT__FEED" })
            {
                Assert.True(Language.Data.ContainsKey(key), key);
            }
        }

        /// <summary>
        /// Changed wording, as from an edited constants.json or another mod's, is what's sent. The reply
        /// format is kept even when the new wording leaves it out.
        /// </summary>
        [Fact]
        public void ChangedWording_IsUsed()
        {
            Language.Data[Prompt.SystemKey] = "Only {max} posts, in {language}. Fans argue about everything.";
            string prompt = Prompt.System("Russian", null, null);
            Assert.StartsWith("Only 6 posts, in Russian. Fans argue about everything.", prompt);
            Assert.EndsWith(Prompt.FormatLead + "\n" + Prompt.FormatExample, prompt);
            Assert.Single(TestEnv.Logs);
        }

        /// <summary>
        /// A missing template falls back to the parts the reply depends on: the fan codes and the reply format.
        /// </summary>
        [Fact]
        public void MissingText_FallsBackToTheEssentials()
        {
            Language.Data.Remove(Prompt.SystemKey);
            string prompt = Prompt.System("English", null, null);
            Assert.StartsWith("Fan accounts.", prompt);
            Assert.Contains("- F_HC_A:", prompt);
            Assert.EndsWith(Prompt.FormatExample, prompt);
            Assert.Single(TestEnv.Logs);
        }

        /// <summary>
        /// Each fan account carries its fan type's opinion of the group, when the game has one.
        /// </summary>
        [Fact]
        public void FanAccounts_CarryTheirOpinion()
        {
            Dictionary<string, int> opinions = new() { ["M_C_T"] = 12, ["F_HC_A"] = 88 };
            string prompt = Prompt.System("English", null, opinions);
            Assert.Contains("- M_C_T: casual teen boy, opinion of the group: 12/100", prompt);
            Assert.Contains("- F_HC_A: devoted adult woman, opinion of the group: 88/100", prompt);
            Assert.Contains("- M_C_YA: casual young adult man\n", prompt);
        }

        [Theory]
        [InlineData(0f, 0)]
        [InlineData(0.5f, 50)]
        [InlineData(0.736f, 74)]
        [InlineData(1f, 100)]
        [InlineData(1.5f, 100)]
        [InlineData(-0.2f, 0)]
        [InlineData(float.NaN, 50)]
        public void Opinion_IsOutOf100(float opinion, int expected)
        {
            Assert.Equal(expected, Prompt.OpinionOutOf100(opinion));
        }

        /// <summary>
        /// The game's 12 fan types map onto the 12 codes the feed draws, one each.
        /// </summary>
        [Fact]
        public void EveryGameFanType_HasItsOwnFeedCode()
        {
            resources.fanType[] genders = { resources.fanType.male, resources.fanType.female };
            resources.fanType[] kinds = { resources.fanType.casual, resources.fanType.hardcore };
            resources.fanType[] ages = { resources.fanType.teen, resources.fanType.youngAdult, resources.fanType.adult };
            List<string> codes = genders.SelectMany(g => kinds.SelectMany(k => ages.Select(a => Prompt.CodeFor(g, k, a)))).ToList();
            Assert.Equal(12, codes.Distinct().Count());
            Assert.All(codes, c => Assert.Contains(c, Prompt.FanCodes));
            Assert.Equal("M_HC_YA", Prompt.CodeFor(resources.fanType.male, resources.fanType.hardcore, resources.fanType.youngAdult));
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
