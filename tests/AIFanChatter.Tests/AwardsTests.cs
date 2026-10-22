using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;
using T = Awards._type;
using Thanks = Awards._speech._thanks;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// The yearly awards show, posted as its dialogue ends: wins, losses with the winner the show announced,
    /// the acceptance speeches, and earlier years.
    /// </summary>
    public class AwardsTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string H = Moment.HistorySection;

        private static readonly Dictionary<string, string> Texts = new()
        {
            ["AWARDS__MOST_PERFORMANCES"] = "Most Performances",
            ["AWARDS__GRAVURE_QUEEN"] = "Gravure Queen",
            ["AWARDS__BEST_DEBUT_IDOL"] = "Best Debut Idol",
            ["AWARDS__BEST_SINGLE"] = "Best Single",
            ["AWARDS__BEST_EMPLOYER"] = "Best Employer",
            ["STAFF__CHOREO"] = "Choreographer",
        };

        public AwardsTests()
        {
            TestEnv.Reset();
            foreach (KeyValuePair<string, string> text in Texts)
            {
                Language.Data[text.Key] = text.Value;
            }
        }

        public void Dispose()
        {
            foreach (string key in Texts.Keys)
            {
                Language.Data.Remove(key);
            }
        }

        /// <summary>A nomination in this year's show, won or lost as the game's SetWins settles it.</summary>
        private static Awards._award Nominate(T type, bool won, data_girls.girls girl = null, singles._single single = null, int year = 2024)
        {
            Awards._award award = new() { Type = type, Girl = girl, Single = single, Year = year, IsNomination = true };
            Awards.TempNominations.Add(award);
            (won ? Awards._Awards : Awards._Nominations).Add(award);
            return award;
        }

        private static Awards._speech Speak(T type, data_girls.girls speaker, Thanks thanks, data_girls.girls idol = null, staff._staff staffer = null, bool won = true)
        {
            Awards._speech speech = new() { Type = type, Girl = speaker, Thanks = thanks, Target_Girl = idol, Target_Staff = staffer, Won = won };
            Awards.Speeches.Add(speech);
            return speech;
        }

        /// <summary>The game's Reset_After_Awards: it clears this year's nominations after the prefix reads them.</summary>
        private static (bool post, AwardsShow show) EndShow(bool ran = true)
        {
            Awards_Reset_After_Awards.Prefix(out AwardsShow show);
            if (ran)
            {
                Awards.TempNominations.Clear();
            }
            return (Awards_Reset_After_Awards.ShouldPost(show, ran, Today), show);
        }

        [Fact]
        public void ShowWithNominations_PostsOnce()
        {
            Nominate(T.most_performances, won: true);
            var (post, show) = EndShow();
            Assert.True(post);
            Assert.Single(show.Nominations);
            Assert.True(show.Nominations[0].Won);
            Awards_Reset_After_Awards.Postfix(show, true);
            Assert.False(Awards_Reset_After_Awards.ShouldPost(show, true, Today));
        }

        /// <summary>
        /// No nominations (another mod cleared them, or there were none), or a skipped reset: no posts.
        /// </summary>
        [Fact]
        public void ShowWithoutNominations_DoesNotPost()
        {
            Assert.False(EndShow().post);
            Nominate(T.most_performances, won: true);
            Assert.False(EndShow(ran: false).post);
        }

        /// <summary>
        /// Wins by the game's titles, with the idol or single they're for; losses with the winner the show
        /// announced, or "lost" alone when it named none; the awards' bonuses aren't sent.
        /// </summary>
        [Fact]
        public void Show_GivesWinsAndLosses()
        {
            data_girls.girls tsubasa = Idol("Tsubasa");
            data_girls.girls rena = Idol("Rena");
            singles._single song = Single("Night Train", Today.AddMonths(-3), 90000).Styled("Pop", "Love", "Cute");
            Nominate(T.best_debut_idol, won: true, girl: tsubasa);
            Nominate(T.most_performances, won: true);
            Nominate(T.gravure_queen, won: false, girl: rena);
            Nominate(T.best_single, won: false, single: song);
            Nominate(T.best_employer, won: false);
            variables.variable.Add(new variables._variable { name = "gravure_queen", value = "Saito Yuna" });
            variables.variable.Add(new variables._variable { name = "gravure_queen_group", value = "Starlight Ribbon" });
            variables.variable.Add(new variables._variable { name = "best_single", value = "Comet" });
            variables.variable.Add(new variables._variable { name = "best_single_group", value = "Hoshi Five" });

            Moment moment = MomentContext.Awards(EndShow().show);
            Assert.Equal("the yearly idol awards show was held", moment.Kind);
            Assert.Equal("the awards show", moment.Label);
            Assert.Equal("\n- Best Debut Idol: Tsubasa\n- Most Performances", moment.Fact(F, "Won"));
            Assert.Equal("\n- Gravure Queen: Rena, lost to Saito Yuna of Starlight Ribbon"
                + "\n- Best Single: \"Night Train\" (Pop, Love lyrics, Cute choreography), lost to \"Comet\" by Hoshi Five"
                + "\n- Best Employer, lost", moment.Fact(F, "Nominated but lost"));
            Assert.DoesNotContain("bonus", moment.Describe(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// One line per win's speech, with whom she thanked; a lost award's speech isn't given.
        /// </summary>
        [Fact]
        public void Speeches_SayWhomEachWinnerThanked()
        {
            staticVars.PlayerData.FirstName = "Haruto";
            staticVars.PlayerData.LastName = "Sato";
            data_girls.girls tsubasa = Idol("Tsubasa"), mirai = Idol("Mirai"), rin = Idol("Rin"), rena = Idol("Rena");
            staff._staff kenji = new() { nickname = "Kenji", type = staff._type.choreographer };
            Nominate(T.best_debut_idol, won: true, girl: tsubasa);
            Nominate(T.most_performances, won: true);
            Nominate(T.best_employer, won: true);
            Nominate(T.gravure_queen, won: false, girl: rena);
            Speak(T.best_debut_idol, tsubasa, Thanks.you);
            Speak(T.most_performances, mirai, Thanks.idol, idol: rin);
            Speak(T.best_employer, rin, Thanks.staff, staffer: kenji);
            Speak(T.gravure_queen, rena, Thanks.fujimoto, won: false);

            string speeches = MomentContext.Awards(EndShow().show).Fact(F, "Acceptance speeches");
            Assert.StartsWith("\n- Best Debut Idol: Tsubasa thanked the group's producer", speeches);
            Assert.Contains("\n- Most Performances: Mirai thanked Rin", speeches);
            Assert.Contains("\n- Best Employer: Rin thanked Kenji (Choreographer)", speeches);
            Assert.DoesNotContain("Rena", speeches);
        }

        /// <summary>The show's DoCustom step building a speech part into the line it's about to show.</summary>
        private static void Build(string formula, Awards._speech speech, string line, bool ran = true)
        {
            ActiveDialogueController controller = (ActiveDialogueController)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ActiveDialogueController));
            controller.activeNode = new data_dialogues._dialogue._node { val = line };
            Award_Speeches_Popup.SelectedSpeech = speech;
            vn_actions_DoCustom.Postfix(formula, controller, ran);
        }

        /// <summary>
        /// The speech in the words the show gave, its parts in order, with the dialogue's variables filled in
        /// (here by a stand-in for the game's parser) and the game's colour tags gone. Other dialogue steps
        /// aren't speech.
        /// </summary>
        [Fact]
        public void Speeches_AreQuoted_AsTheShowGaveThem()
        {
            Func<string, string> parse = AwardsShow.Parse;
            AwardsShow.Parse = text => text.Replace("[groupname]", "Stars");
            try
            {
                data_girls.girls mirai = Idol("Mirai"), rin = Idol("Rin");
                Nominate(T.most_performances, won: true);
                Awards._speech speech = Speak(T.most_performances, mirai, Thanks.idol, idol: rin);
                Build("awards_group_intro", speech, "Thank you all so much! It's an honor to be accepting this award on behalf of [groupname].");
                Build("awards_lost_narration", speech, "As the winner walks up to the stage...");
                Build("awards_group_thanks", speech, "I'd like to thank <color=#00f>Rin</color>, who supports us every step of the way.");
                Build("awards_group_thanks", speech, "Skipped by another mod", ran: false);

                Assert.Equal("\n- Most Performances, Mirai: \"Thank you all so much! It's an honor to be accepting this award on behalf of Stars."
                    + " I'd like to thank Rin, who supports us every step of the way.\"",
                    MomentContext.Awards(EndShow().show).Fact(F, "Acceptance speeches"));
            }
            finally
            {
                AwardsShow.Parse = parse;
                Award_Speeches_Popup.SelectedSpeech = null;
            }
        }

        [Theory]
        [InlineData(Thanks.fujimoto, "Gravure Queen: Rena thanked Fujimoto")]
        [InlineData(Thanks.her_choice, "Gravure Queen: Rena gave an acceptance speech")]
        public void Speeches_WithoutANamedIdol(Thanks thanks, string expected)
        {
            data_girls.girls rena = Idol("Rena");
            Nominate(T.gravure_queen, won: true, girl: rena);
            Speak(T.gravure_queen, rena, thanks);
            Assert.Equal("\n- " + expected, MomentContext.Awards(EndShow().show).Fact(F, "Acceptance speeches"));
        }

        /// <summary>
        /// Winners, then speakers for the agency's awards, then nominees who lost; at most 5.
        /// </summary>
        [Fact]
        public void Members_AreWinnersThenSpeakersThenNominees()
        {
            data_girls.girls winner = Idol("Winner"), speaker = Idol("Speaker"), loser = Idol("Loser");
            Nominate(T.gravure_queen, won: false, girl: loser);
            Nominate(T.best_debut_idol, won: true, girl: winner);
            Nominate(T.most_performances, won: true);
            Speak(T.most_performances, speaker, Thanks.you);
            Speak(T.best_debut_idol, winner, Thanks.you);
            Assert.Equal(new[] { "Winner", "Speaker", "Loser" }, MomentContext.Awards(EndShow().show).Facts(M).Select(f => f.Key));
        }

        /// <summary>
        /// Earlier wins, last year's by title, and how many shows the agency has been in.
        /// </summary>
        [Fact]
        public void History_GivesEarlierYears()
        {
            Awards._Awards.Add(new Awards._award { Type = T.best_employer, Year = 2022 });
            Awards._Awards.Add(new Awards._award { Type = T.most_performances, Year = 2023 });
            Awards._Awards.Add(new Awards._award { Type = T.best_employer, Year = 2023 });
            Awards._Nominations.Add(new Awards._award { Type = T.gravure_queen, Year = 2021 });
            Nominate(T.most_performances, won: true);

            Moment moment = MomentContext.Awards(EndShow().show);
            Assert.Equal("3", moment.Fact(H, "Awards won in earlier years"));
            Assert.Equal("Most Performances and Best Employer", moment.Fact(H, "Last year's wins"));
            Assert.Equal("4", moment.Fact(H, "Awards shows the agency was in, this one included"));
        }
    }
}
