using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static StarSigns.StarSigns;
using static System.FormattableString;

namespace StarSigns.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        public AssetTests() => TestGame.Reset();

        private static int Percent(float coeff) => (int)Math.Round(coeff * 100);

        /// <summary>
        /// The game moves a positive relationship by Add(0.1) a week, and Add halves positive values.
        /// </summary>
        private const float VanillaPositiveDrift = 0.1f / 2;

        /// <summary>
        /// A weekly bonus rolled at this chance adds, on average, this percentage of the game's weekly drift.
        /// </summary>
        private static int Average(int chance) => (int)Math.Round(chance * WeeklyBonusTests.Bonus / VanillaPositiveDrift);

        private static Dictionary<string, string> Constants()
        {
            JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            return Enumerable.Range(0, constants.Count).ToDictionary(i => (string)constants[i]["id"], i => (string)constants[i]["text"]);
        }

        /// <summary>
        /// Every sign has a name and a description, under the ids the code looks up, and nothing else.
        /// </summary>
        [Fact]
        public void Constants_NameAndDescribeEverySign()
        {
            Dictionary<string, string> text = Constants();

            IEnumerable<string> expectedIds = TestGame.Signs.SelectMany(s => new[]
            {
                CONSTANT_SIGN_PREFIX + s.ToString().ToUpper(),
                CONSTANT_DESC_PREFIX + s.ToString().ToUpper(),
            });
            Assert.Equal(expectedIds.OrderBy(id => id), text.Keys.OrderBy(id => id));

            foreach (Zodiac sign in TestGame.Signs)
            {
                Assert.Equal(sign.ToString(), text[CONSTANT_SIGN_PREFIX + sign.ToString().ToUpper()]);
                Assert.False(string.IsNullOrWhiteSpace(text[CONSTANT_DESC_PREFIX + sign.ToString().ToUpper()]));
            }
        }

        /// <summary>
        /// The Steam description's effect lines match the code. The weekly relationship bonuses (Aries,
        /// Cancer, Virgo, Libra, Sagittarius, Capricorn, Pisces) are described by their average effect.
        /// </summary>
        [Fact]
        public void SteamDescription_MatchesCode()
        {
            string text = File.ReadAllText(TestGame.ModAsset("steam description.txt"));

            string[] expected =
            {
                $"Aries: About {Average(ARIES_REL_CHANCE)}% bonus to relationships when she's pushed",
                $"Taurus: Relationships fluctuate {100 - Percent(TAURUS_REL_COEFF)}% less",
                $"Gemini: Relationships fluctuate {Percent(GEMINI_REL_COEFF) - 100}% more",
                $"Cancer: Relationships improve about {Average(CANCER_REL_CHANCE)}% more with her clique",
                Invariant($"Leo: +{LEO_LEADER_BONUS} bonus to smart and funny when choosing clique leader"),
                $"Virgo: Relationships improve about {Average(VIRGO_REL_CHANCE)}% more with girls with >{VIRGO_SKILL_THR} average skill",
                $"Libra: Relationships improve about {Average(LIBRA_REL_CHANCE)}% more with bullied girls",
                $"Scorpio: {SCORP_BULLY_BONUS}% less chance of being bullied",
                $"Sagittarius: Relationships improve about {Average(SAGG_REL_CHANCE)}% more with scandal girls",
                $"Capricorn: Relationships improve about {Average(CAPR_REL_CHANCE)}% more with girls who don't date",
                $"Aquarius: {100 - Percent(AQUA_REL_COEFF)}% less penalty to relationship if pushed girl has less skills than her",
                $"Pisces: All relationships improve about {Average(PISCES_REL_CHANCE)}% more",
            };
            foreach (string line in expected)
                Assert.Contains(line, text);
        }

        /// <summary>
        /// The Steam description links the guide (SteamCMD can't upload a description with double quotes,
        /// so the params.json example lives there), and the guide's params.json line sets the sign it names.
        /// </summary>
        [Fact]
        public void Guide_StarsignExampleWorks()
        {
            Assert.Contains("[url=https://github.com/ui3TD/Tel-Mod-Library/blob/main/docs/StarSigns.md]",
                File.ReadAllText(TestGame.ModAsset("steam description.txt")));
            string line = File.ReadAllLines(Path.Combine(TestGame.RepoRoot(), "docs", "StarSigns.md")).Single(l => l.Contains("\"starsign\""));

            data_girls_textures_LoadAssetsData.Infix(JSON.Parse("{" + line + "}"), new data_girls_textures._textureAsset());

            Assert.Equal(Zodiac.Capricorn, Assert.Single(UniqueIdolSigns).Value);
        }

        /// <summary>
        /// The Steam changelog ends with an entry for the version in the project file, short enough for
        /// the game's 100-character change notes box.
        /// </summary>
        [Fact]
        public void SteamChangelog_ListsCurrentVersion()
        {
            string version = Regex.Match(File.ReadAllText(TestGame.ModFile("Star Signs.csproj")), "<Version>(.+)</Version>").Groups[1].Value;
            string last = File.ReadAllLines(TestGame.ModAsset("steam description.txt")).Last();
            Assert.StartsWith($"- {version}: ", last);
            Assert.InRange(last.Length - "- ".Length, 1, 100);
        }
    }
}
