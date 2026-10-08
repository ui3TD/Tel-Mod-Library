using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PromotionTierTweaks.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        public AssetTests() => TestGame.Reset();

        private static string SteamDescription() => File.ReadAllText(TestGame.ModAsset("steam description.txt"));

        private static Dictionary<string, string> Constants()
        {
            SimpleJSON.JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            return Enumerable.Range(0, constants.Count).ToDictionary(i => constants[i]["id"].Value, i => constants[i]["text"].Value);
        }

        /// <summary>
        /// The tooltip labels exist and give the episode counts the code requires.
        /// </summary>
        [Theory]
        [InlineData(Utility.lvl3Label, "Run {0} episodes of an internet show", Utility.lvl3Eps)]
        [InlineData(Utility.lvl4Label, "Run {0} episodes of a radio show", Utility.lvl4Eps)]
        [InlineData(Utility.lvl6Label, "Run {0} episodes of a TV show", Utility.lvl6Eps)]
        public void Constants_LabelsMatchTheRequirements(string label, string format, int episodes)
        {
            Assert.Equal(string.Format(format, episodes), Constants()[label]);
        }

        [Fact]
        public void Constants_HaveNoOtherEntries()
        {
            Assert.Equal(new[] { Utility.lvl3Label, Utility.lvl4Label, Utility.lvl6Label }, Constants().Keys.OrderBy(k => k));
        }

        /// <summary>
        /// The requirements the mod copies from the game still match it.
        /// </summary>
        [Fact]
        public void CopiedGameValues_MatchTheGame()
        {
            Assert.Equal(Utility.lvl3mags, TestGame.GamePromo("lvl3_mags"));
            Assert.Equal(Utility.lvl6mags, TestGame.GamePromo("lvl6_mags"));
            Assert.Equal(Utility.lvl9IdolCount, TestGame.GamePromo("lvl9_famous_idols_count"));
        }

        /// <summary>
        /// Each Steam description line gives the mod's requirement and the game's it replaces.
        /// </summary>
        [Fact]
        public void SteamDescription_MatchesTheRequirements()
        {
            string description = SteamDescription();

            Assert.Contains($"- Promotion lvl 3 requires {Utility.lvl3Eps} episodes of an internet show instead of just 1", description);
            Assert.Contains($"- Promotion lvl 4 requires {Utility.lvl4Eps} episodes of a radio show instead of just 1, " +
                $"and peak internet show audience of {Utility.lvl4audience} instead of {TestGame.GamePromo("lvl4_internet_audience")}", description);
            Assert.Contains($"- Promotion lvl 6 requires {Utility.lvl6Eps} episodes of a TV show instead of just 1", description);
            Assert.Contains($"- Promotion lvl 9 requires {Utility.lvl9IdolCount} idols with {Utility.lvl9Fame} fame " +
                $"instead of {TestGame.GamePromo("lvl9_famous_idols_lvl")} fame", description);
        }

        /// <summary>
        /// Every requirement the mod raises is above the game's own.
        /// </summary>
        [Fact]
        public void Requirements_AreHarderThanTheGame()
        {
            Assert.True(Utility.lvl4audience > TestGame.GamePromo("lvl4_internet_audience"));
            Assert.True(Utility.lvl9Fame > TestGame.GamePromo("lvl9_famous_idols_lvl"));
            Assert.True(Utility.lvl3Eps > 1);
            Assert.True(Utility.lvl4Eps > 1);
            Assert.True(Utility.lvl6Eps > 1);
        }
    }
}
