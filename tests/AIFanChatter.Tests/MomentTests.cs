using System;
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

        /// <summary>
        /// The set list in order, as one unnumbered list: each song with its single's style and the idol at
        /// its center, each talk break with its idols. Hype is a percentage up to 200%, as in the game. The
        /// concert's name (the game's is its venue's), its performers (the set list names them) and whether it
        /// made a profit aren't sent.
        /// </summary>
        [Fact]
        public void Concert_GivesItsSetList_AndHypeAsAPercentage()
        {
            data_girls.girls arisa = TestGame.Idol("Arisa");
            data_girls.girls kokoro = TestGame.Idol("Kokoro");
            data_girls.girls mika = TestGame.Idol("Mika");
            singles._single metal = TestGame.Single("DJ, Never Let Me Go", TestGame.Today.AddDays(-5), 1).Styled("Metal", "Regret", "Cute");
            singles._single plain = TestGame.Single("Old Song", TestGame.Today.AddDays(-50), 1);
            SEvent_Concerts._concert concert = TestGame.Concert("Live", TestGame.Today, 15000);
            concert.ProjectedValues.Actual_Hype = 199;
            concert.ProjectedValues.Actual_Attendance = 0.92f;
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Single = metal, Center = arisa });
            concert.SetListItems.Add(new SEvent_Concerts._concert._mc { Girls = new List<data_girls.girls> { kokoro, null, mika } });
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Single = plain });
            concert.SetListItems.Add(new SEvent_Concerts._concert._mc());
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = arisa });

            Moment moment = MomentContext.Concert(concert);
            Assert.Equal("199%", moment.Fact(Moment.FactsSection, "Hype"));
            // The share of seats filled and the people who came, in one line
            Assert.Equal("92% (15000)", moment.Fact(Moment.FactsSection, "Attendance"));
            Assert.Null(moment.Fact(Moment.FactsSection, "Audience"));
            Assert.Null(moment.Fact(Moment.FactsSection, "Made a profit"));
            Assert.Null(moment.Fact(Moment.FactsSection, "Title"));
            Assert.Null(moment.Fact(Moment.FactsSection, "Performers"));
            string lines = moment.Lines(Moment.FactsSection);
            Assert.Contains("\nSet list, in order:\n"
                + "- \"DJ, Never Let Me Go\" (Metal, Regret lyrics, Cute choreography), center Arisa\n"
                + "- Talk break with Kokoro and Mika\n"
                + "- \"Old Song\"\n"
                + "- Talk break", lines);
            Assert.DoesNotContain("Set list 1", lines);
        }

        [Fact]
        public void Lists_LeaveOutItemsTheGameCantGive_AndAreLeftOutWhenEmpty()
        {
            Moment moment = new Moment("x")
                .AddList(Moment.FactsSection, "Things", new Func<object>[] { () => "one", () => throw new InvalidOperationException(), () => " ", () => "two" })
                .AddList(Moment.FactsSection, "Nothing", new Func<object>[] { () => null })
                .Add("After", () => "yes");
            Assert.Equal("Things:\n- one\n- two\nAfter: yes", moment.Lines(Moment.FactsSection));
        }

        [Fact]
        public void JoinAnd_ReadsAsAList()
        {
            Assert.Equal("", MomentContext.JoinAnd(new string[0]));
            Assert.Equal("A", MomentContext.JoinAnd(new[] { "A" }));
            Assert.Equal("A and B", MomentContext.JoinAnd(new[] { "A", "B" }));
            Assert.Equal("A, B and C", MomentContext.JoinAnd(new[] { "A", "B", "C" }));
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

        /// <summary>
        /// The full name after a nickname is the one the game's own GetName gives without the nickname, in the
        /// name order the language's "default" variable sets.
        /// </summary>
        [Fact]
        public void NicknamedIdol_KeepsTheNameTheGameShowsWithoutIt()
        {
            Language.Variables.Add(new Language._variable { Variable = "default", Male = "firstName lastName", Female = "lastName firstName" });
            data_girls.girls center = TestGame.Idol("");
            center.firstName = "Momone";
            center.lastName = "Yasuda";
            data_girls.girls other = TestGame.Idol("Aya");
            singles._single single = TestGame.Single("Song", TestGame.Today, 1, center, other);
            string fullName = center.GetName(true);
            Assert.Equal("Yasuda Momone", fullName);

            center.nickname = "TestNick";
            string text = MomentContext.Single(single).Describe();
            Assert.Contains("Center: TestNick (" + fullName + ")", text);
            string members = text.Split('\n').FirstOrDefault(l => l.StartsWith("Members: ")) ?? "";
            Assert.Contains("TestNick (" + fullName + ")", members);
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

        /// <summary>
        /// The game's event texts speak to the player; when one says "you", the model is told who that is.
        /// </summary>
        [Theory]
        [InlineData("Announce that you'll be taking a sabbatical", "", true)]
        [InlineData("Stay quiet", "Fans praise your restraint.", true)]
        [InlineData("Stay quiet", "Nothing changes.", false)]
        [InlineData("Young fans cheer", "", false)]
        public void Event_SayingYou_NamesTheProducer(string answer, string outcome, bool named)
        {
            Event_Manager._activeEvent active = new() { data = new Event_Manager._randomEvent { title = "" } };
            Event_Manager._randomEvent._reply reply = new() { text = answer, description = outcome };
            Moment moment = MomentContext.Event(active, reply);
            Assert.Equal(named ? "the group's producer" : null, moment.Fact(Moment.FactsSection, "You"));
        }

        /// <summary>
        /// The producer goes by the name the game shows for the player: a nickname when one is set.
        /// </summary>
        [Fact]
        public void Producer_IsNamedAsTheGameShowsThePlayer()
        {
            Language.Variables.Add(new Language._variable { Variable = "default", Male = "lastName firstName", Female = "lastName firstName" });
            staticVars._playerData before = staticVars.PlayerData;
            List<staff._staff> staffBefore = staff.Staff.ToList();
            try
            {
                staticVars.PlayerData = new staticVars._playerData { FirstName = "Haruto", LastName = "Sato" };
                staff.Staff.Clear();
                staff._staff player = new() { type = staff._type.player };
                staff.Staff.Add(player);
                Assert.Equal("the group's producer (Sato Haruto)", MomentContext.Producer());
                player.nickname = "Boss";
                Assert.Equal("the group's producer (Boss)", MomentContext.Producer());
            }
            finally
            {
                staticVars.PlayerData = before;
                staff.Staff.Clear();
                staff.Staff.AddRange(staffBefore);
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
            Assert.Contains("0 to 3 replies from other fans but not themselves.", prompt);
            Assert.Contains("under 100 characters", prompt);
            Assert.Contains("- Write in Japanese.", prompt);
            Assert.Contains("- Match each fan's tone to their opinion of the group", prompt);
            Assert.Contains("\n- Don't repeat game stats. Exact sales and attendance can be quoted where it fits.\n", prompt);
            Assert.True(Prompt.AsksForSeveralPosts());
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

        /// <summary>
        /// Each of the game's fan groups gets the opinion the game works out from its gender, devotion and age
        /// (resources.GetFanOpinion), out of 100. A fan type the game has no opinion list for is left out.
        /// </summary>
        [Fact]
        public void GameHost_GivesEachFanGroupTheGamesOpinion()
        {
            resources.fanType[] genders = { resources.fanType.male, resources.fanType.female };
            resources.fanType[] devotions = { resources.fanType.casual, resources.fanType.hardcore };
            resources.fanType[] ages = { resources.fanType.teen, resources.fanType.youngAdult, resources.fanType.adult };
            foreach (resources.fanType gender in genders)
                foreach (resources.fanType devotion in devotions)
                    foreach (resources.fanType age in ages)
                        resources.Fans.Add(new resources._fan { gender = gender, hardcoreness = devotion, age = age, people = 100 });
            foreach (resources.fanType type in genders.Concat(devotions).Concat(ages))
            {
                resources.fanOpinions.Add(new resources._fanOpinion { type = type });
            }
            resources.GetFanOpinion(resources.fanType.male).Vals.AddRange(new[] { 1, 1, 1 });
            resources.GetFanOpinion(resources.fanType.teen).Vals.Add(-1);

            IDictionary<string, int> opinions = new GameHost().FanOpinions;
            Assert.Equal(12, opinions.Count);
            Assert.Equal(75, opinions["M_C_T"]);
            Assert.Equal(100, opinions["M_HC_A"]);
            Assert.Equal(0, opinions["F_C_T"]);
            Assert.Equal(50, opinions["F_HC_YA"]);

            resources.fanOpinions.RemoveAll(o => o.type == resources.fanType.adult);
            opinions = new GameHost().FanOpinions;
            Assert.Equal(8, opinions.Count);
            Assert.False(opinions.ContainsKey("M_C_A"));
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
