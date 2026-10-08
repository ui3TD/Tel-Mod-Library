using Xunit;
using _media_type = Shows._param._media_type;
using _status = Shows._show._status;

namespace PromotionTierTweaks.Tests
{
    /// <summary>
    /// The tier requirements count a single show's episodes.
    /// </summary>
    public class EpisodeCountTests
    {
        public EpisodeCountTests() => TestGame.Reset();

        [Fact]
        public void NoShows_CountsZero()
        {
            Assert.Equal(0, Utility.CountEpisodesOfShowsWithType(_media_type.internet));
        }

        /// <summary>
        /// "6 episodes of an internet show": the longest-running show counts, episodes of different shows don't add up.
        /// </summary>
        [Fact]
        public void CountsTheLongestRunningShow()
        {
            TestGame.Show(_media_type.internet, 4);
            TestGame.Show(_media_type.internet, 9);
            TestGame.Show(_media_type.internet, 5);

            Assert.Equal(9, Utility.CountEpisodesOfShowsWithType(_media_type.internet));
        }

        [Fact]
        public void IgnoresOtherMedia()
        {
            TestGame.Show(_media_type.radio, 12);
            TestGame.Show(_media_type.tv, 20);
            TestGame.Show(_media_type.internet, 3);

            Assert.Equal(3, Utility.CountEpisodesOfShowsWithType(_media_type.internet));
            Assert.Equal(12, Utility.CountEpisodesOfShowsWithType(_media_type.radio));
            Assert.Equal(20, Utility.CountEpisodesOfShowsWithType(_media_type.tv));
        }

        /// <summary>
        /// A show still in production doesn't count; one that aired its episodes does, even if later cancelled or relaunched.
        /// </summary>
        [Theory]
        [InlineData(_status.working, 0)]
        [InlineData(_status.released, 7)]
        [InlineData(_status.relaunching, 7)]
        [InlineData(_status.relaunching_working, 7)]
        [InlineData(_status.canceled, 7)]
        public void CountsShowsThatHaveAired(_status status, int expected)
        {
            TestGame.Show(_media_type.tv, 7, status: status);

            Assert.Equal(expected, Utility.CountEpisodesOfShowsWithType(_media_type.tv));
        }

        /// <summary>
        /// Shows the game hasn't given a medium (e.g. a draft) are skipped.
        /// </summary>
        [Fact]
        public void SkipsShowsWithoutMedium()
        {
            TestGame.Show(_media_type.internet, 7).medium.media_type = null;

            Assert.Equal(0, Utility.CountEpisodesOfShowsWithType(_media_type.internet));
        }
    }

    /// <summary>
    /// The highest promotion level the game allows, through the patched game method.
    /// </summary>
    public class MaxLevelTests
    {
        public MaxLevelTests() => TestGame.Reset(patched: true);

        [Fact]
        public void AllRequirementsMet_ReachesLevel10()
        {
            TestGame.MeetVanillaRequirements();
            TestGame.MeetModRequirements();

            Assert.Equal(10, TestGame.MaxLevel(0));
        }

        /// <summary>
        /// The game's requirements alone stop at level 2: lvl 3 wants 6 internet episodes, not 1.
        /// </summary>
        [Fact]
        public void VanillaRequirementsOnly_StopsAtLevel2()
        {
            TestGame.MeetVanillaRequirements();

            Assert.Equal(2, TestGame.MaxLevel(0));
        }

        /// <summary>
        /// Each of the mod's requirements, missing on its own, stops promotion just below its tier.
        /// </summary>
        [Theory]
        [InlineData("internet episodes", 2)]
        [InlineData("internet audience", 3)]
        [InlineData("radio episodes", 3)]
        [InlineData("tv episodes", 5)]
        [InlineData("famous idols", 8)]
        public void EachModRequirement_BlocksItsTier(string missing, int expected)
        {
            TestGame.MeetVanillaRequirements();
            TestGame.Show(_media_type.internet, missing == "internet episodes" ? Utility.lvl3Eps - 1 : Utility.lvl3Eps);
            TestGame.Show(_media_type.internet, 1, missing == "internet audience" ? Utility.lvl4audience - 1 : Utility.lvl4audience);
            TestGame.Show(_media_type.radio, missing == "radio episodes" ? Utility.lvl4Eps - 1 : Utility.lvl4Eps);
            TestGame.Show(_media_type.tv, missing == "tv episodes" ? Utility.lvl6Eps - 1 : Utility.lvl6Eps);
            for (int i = 0; i < Utility.lvl9IdolCount; i++)
                TestGame.Idol(missing == "famous idols" && i == 0 ? Utility.lvl9Fame - 1 : Utility.lvl9Fame);

            Assert.Equal(expected, TestGame.MaxLevel(0));
        }

        /// <summary>
        /// "6000 instead of 1000": peak internet audience exactly at the requirement is enough.
        /// </summary>
        [Theory]
        [InlineData(5999, 3)]
        [InlineData(6000, 10)]
        public void InternetAudience_Threshold(long peak, int expected)
        {
            TestGame.MeetVanillaRequirements();
            TestGame.MeetModRequirements();
            Shows.shows.RemoveAll(s => s.medium.media_type == _media_type.internet);
            TestGame.Show(_media_type.internet, Utility.lvl3Eps, peak);

            Assert.Equal(expected, TestGame.MaxLevel(0));
        }

        /// <summary>
        /// A cancelled show that ran its episodes still meets its tier, with no other show of that type on air.
        /// </summary>
        [Theory]
        [InlineData(_media_type.internet, 2)]
        [InlineData(_media_type.radio, 3)]
        [InlineData(_media_type.tv, 5)]
        public void CancelledShowThatRanItsEpisodes_MeetsItsTier(_media_type medium, int currentLevel)
        {
            TestGame.MeetVanillaRequirements();
            TestGame.MeetModRequirements();
            foreach (Shows._show show in Shows.shows.FindAll(s => s.medium.media_type == medium))
                show.status = _status.canceled;

            Assert.Equal(10, TestGame.MaxLevel(currentLevel));
        }

        /// <summary>
        /// A show cancelled before its sixth episode doesn't.
        /// </summary>
        [Fact]
        public void CancelledShowShortOfItsEpisodes_DoesNotMeetItsTier()
        {
            TestGame.MeetVanillaRequirements();
            TestGame.MeetModRequirements();
            Shows.shows.RemoveAll(s => s.medium.media_type == _media_type.tv);
            TestGame.Show(_media_type.tv, Utility.lvl6Eps - 1, status: _status.canceled);

            Assert.Equal(5, TestGame.MaxLevel(5));
        }

        /// <summary>
        /// Anywhere else, the game's count of launched shows still leaves out cancelled ones.
        /// </summary>
        [Fact]
        public void CancelledShows_CountOnlyForPromotion()
        {
            TestGame.MeetVanillaRequirements();
            TestGame.MeetModRequirements();
            Shows.shows.ForEach(s => s.status = _status.canceled);

            Assert.Equal(10, TestGame.MaxLevel(0));
            Assert.False(Activities_GetMaxLevel_Promotion.Running);
            Assert.Equal(0, Shows.CountShowsWithType(_media_type.internet));
        }

        /// <summary>
        /// A cancelled show that never aired doesn't count as launched.
        /// </summary>
        [Fact]
        public void CancelledShowWithNoEpisodes_DoesNotCount()
        {
            TestGame.Show(_media_type.radio, 0, status: _status.canceled);
            int result = 0;
            Activities_GetMaxLevel_Promotion.Running = true;
            try
            {
                Shows_CountShowsWithType.Postfix(ref result, _media_type.radio);
            }
            finally
            {
                Activities_GetMaxLevel_Promotion.Running = false;
            }

            Assert.Equal(0, result);
        }

        /// <summary>
        /// Graduated idols don't count towards the famous idols.
        /// </summary>
        [Fact]
        public void GraduatedIdols_DontCountAsFamous()
        {
            TestGame.MeetVanillaRequirements();
            TestGame.MeetModRequirements();
            data_girls.girl.Find(g => g.GetFameLevel() == Utility.lvl9Fame).status = data_girls._status.graduated;

            Assert.Equal(8, TestGame.MaxLevel(0));
        }

        /// <summary>
        /// The lowest unmet requirement decides, however many others are also missing.
        /// </summary>
        [Fact]
        public void SeveralMissing_StopsAtTheLowest()
        {
            TestGame.MeetVanillaRequirements();
            TestGame.Show(_media_type.internet, Utility.lvl3Eps, Utility.lvl4audience);

            Assert.Equal(3, TestGame.MaxLevel(0));
        }

        /// <summary>
        /// A tier already reached is never taken away, even if the shows behind it are gone.
        /// </summary>
        [Theory]
        [InlineData(3, 3)]
        [InlineData(4, 5)]
        [InlineData(6, 8)]
        [InlineData(9, 10)]
        public void ReachedTiers_AreKept(int currentLevel, int expected)
        {
            TestGame.MeetVanillaRequirements();

            Assert.Equal(expected, TestGame.MaxLevel(currentLevel));
        }

        /// <summary>
        /// Meeting the mod's requirements never skips the game's own.
        /// </summary>
        [Fact]
        public void ModRequirementsMet_GameRequirementsStillApply()
        {
            TestGame.MeetModRequirements();
            TestGame.Idol();
            TestGame.Idol();
            singles.Singles.Add(new singles._single { status = singles._single._status.released });

            // No magazine appearances yet: the game's lvl 3 requirement
            Assert.Equal(2, TestGame.MaxLevel(0));
        }

        [Fact]
        public void Level10_StaysLevel10()
        {
            Assert.Equal(10, TestGame.MaxLevel(10));
        }

        /// <summary>
        /// The postfix only ever lowers the game's result.
        /// </summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 2)]
        [InlineData(3, 3)]
        [InlineData(5, 5)]
        [InlineData(8, 8)]
        public void Postfix_NeverRaisesTheGamesResult(int currentLevel, int vanilla)
        {
            int result = vanilla;
            Activities_GetMaxLevel_Promotion.Postfix(ref result, new Activities._activity { lvl = currentLevel });

            Assert.Equal(vanilla, result);
        }
    }

    /// <summary>
    /// The promotion tooltip shows the mod's requirements with the player's progress.
    /// </summary>
    public class DescriptionTests
    {
        private static readonly string Green = mainScript.green;
        private static readonly string Blue = mainScript.blue;
        private static readonly string Red = mainScript.red;

        public DescriptionTests() => TestGame.Reset(patched: true);

        private static string Line(string text, string colour) => $"<color={colour}>{text}</color>";

        [Fact]
        public void Level3_NextTier_ShowsProgress()
        {
            Stats.data.business.photoshoot_counter = 1;
            TestGame.Show(_media_type.internet, 2);

            Assert.Equal(
                Line("Appear in magazines: 1 / 3", Blue) + "\n" + Line("Run 6 episodes of an internet show: 2 / 6", Blue),
                Activities.GetPromotionDescription(3, 2));
        }

        [Fact]
        public void Level3_RequirementsMet_AreGreenAndCapped()
        {
            Stats.data.business.photoshoot_counter = 10;
            TestGame.Show(_media_type.internet, 30);

            Assert.Equal(
                Line("Appear in magazines: 3 / 3", Green) + "\n" + Line("Run 6 episodes of an internet show: 6 / 6", Green),
                Activities.GetPromotionDescription(3, 2));
        }

        /// <summary>
        /// Tiers beyond the next one are red until met.
        /// </summary>
        [Fact]
        public void Level4_FutureTier_IsRed()
        {
            TestGame.Show(_media_type.internet, 1, 2500);
            TestGame.Show(_media_type.radio, 6);

            Assert.Equal(
                Line("Internet show peak audience: 2,500 / 6,000", Red) + "\n" + Line("Run 6 episodes of a radio show: 6 / 6", Green),
                Activities.GetPromotionDescription(4, 1));
        }

        [Fact]
        public void Level6_ShowsTvEpisodes()
        {
            Stats.data.business.photoshoot_counter = 24;
            TestGame.Show(_media_type.tv, 5);

            Assert.Equal(
                Line("Appear in magazines: 24 / 24", Green) + "\n" + Line("Run 6 episodes of a TV show: 5 / 6", Blue),
                Activities.GetPromotionDescription(6, 5));
        }

        [Fact]
        public void Level9_ShowsIdolsWithFame9()
        {
            TestGame.Idol(9);
            TestGame.Idol(8);
            TestGame.Idol(10);

            Assert.Equal(Line("Idols with level 9 fame: 2 / 3", Blue), Activities.GetPromotionDescription(9, 8));
        }

        /// <summary>
        /// A tier the player has already reached shows as complete, whatever the current numbers.
        /// </summary>
        [Theory]
        [InlineData(3, "Appear in magazines: 3 / 3", "Run 6 episodes of an internet show: 6 / 6")]
        [InlineData(4, "Internet show peak audience: 6,000 / 6,000", "Run 6 episodes of a radio show: 6 / 6")]
        [InlineData(6, "Appear in magazines: 24 / 24", "Run 6 episodes of a TV show: 6 / 6")]
        [InlineData(9, "Idols with level 9 fame: 3 / 3", null)]
        public void ReachedTier_ShowsComplete(int lvl, string line1, string line2)
        {
            string expected = Line(line1, Green) + (line2 == null ? "" : "\n" + Line(line2, Green));

            Assert.Equal(expected, Activities.GetPromotionDescription(lvl, lvl));
            Assert.Equal(expected, Activities.GetPromotionDescription(lvl, 10));
        }

        /// <summary>
        /// The other tiers keep the game's text.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(10)]
        public void OtherTiers_Unchanged(int lvl)
        {
            string vanilla = "game text";
            Activities_GetPromotionDescription.Postfix(ref vanilla, lvl, 0);

            Assert.Equal("game text", vanilla);
        }
    }
}
