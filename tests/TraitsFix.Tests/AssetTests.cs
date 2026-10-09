using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static System.FormattableString;
using static TraitFix.TraitsFix;

namespace TraitsFixTests
{
    /// <summary>
    /// The in-game, notification and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private static int Percent(float coeff) => (int)Math.Round(coeff * 100);

        /// <summary>
        /// The game moves a positive relationship by Add(0.1) a week, and Add halves positive values.
        /// </summary>
        private const float VanillaPositiveDrift = 0.1f / 2;

        private static readonly string[] Traits =
        {
            "Clumsy", "Annoying", "Misandry", "Indiscreet", "Maternal", "Precocious", "Underdog", "Defeatist", "Worrier",
            "Arrogant", "Complacent", "Lone_Wolf", "Anxiety", "Live_fast", "Forgiving", "Trendy", "Perfectionist",
            "Photogenic", "Meme_queen",
        };

        /// <summary>
        /// The numbers each in-game description must state.
        /// </summary>
        public static IEnumerable<object[]> InGameDescriptions() => new[]
        {
            new object[] { "Clumsy", $"{CLUMSY_DANCE_MODIFIER} to dance, +{CLUMSY_FUNNY_MODIFIER} to comedy" },
            new object[] { "Annoying", Invariant($"{1 + ANNOYING_MODIFIER}x more stamina") },
            new object[] { "Misandry", $"{MISANDRY_CHANCE}% chance for a negative opinion" },
            new object[] { "Underdog", $"+{UNDERDOG_MODIFIER} to all stats if latest single didn't top the chart" },
            new object[] { "Defeatist", $"{DEFEATIST_MODIFIER} to all stats if latest single didn't top the chart" },
            new object[] { "Worrier", $"{WORRIER_MODIFIER} to all stats when group has scandal points" },
            new object[] { "Complacent", $"{COMPLACENT_MODIFIER} to performance stats after centering a single" },
            new object[] { "Lone_Wolf", $"+{LONEWOLF_MODIFIER} bonus to all stats when hosting a show alone" },
            new object[] { "Anxiety", $"{ANXIETY_MODIFIER} reduction in all stats" },
            new object[] { "Trendy", Invariant($"{TRENDY_TEEN_MODIFIER}x appeal for teens and young adults, {TRENDY_ADULT_MODIFIER}x appeal for adults") },
            new object[] { "Perfectionist", $"{PERFECTIONIST_MENTAL} to mental stamina when an event goes poorly (tour <{PERFECTIONIST_TOUR_ATT}% attendance or concert <{PERFECTIONIST_HYPE}% hype)" },
            new object[] { "Photogenic", $"+{Percent(PHOTOGENIC_MODIFIER)}% bonus to photoshoot payments" },
            new object[] { "Meme_queen", $"+{MEME_INT_SHOW} to all stats for online content" },
        };

        private static JSONNode TraitEntry(string trait)
        {
            JSONNode list = TestGame.LoadJson("JSON/Idols/traits.json");
            for (int i = 0; i < list.Count; i++)
            {
                if ((string)list[i]["type"] == trait)
                    return list[i];
            }
            Assert.Fail($"{trait} missing from traits.json");
            return null;
        }

        [Fact]
        public void TraitsJson_ListsEveryFixedTraitOnce()
        {
            JSONNode list = TestGame.LoadJson("JSON/Idols/traits.json");
            List<string> types = Enumerable.Range(0, list.Count).Select(i => (string)list[i]["type"]).ToList();
            Assert.Equal(Traits.OrderBy(n => n), types.OrderBy(n => n));
            foreach (string type in types)
                Assert.True(Enum.IsDefined(typeof(traits._trait._type), type), $"{type} isn't a game trait");
        }

        [Theory]
        [MemberData(nameof(InGameDescriptions))]
        public void InGameDescription_MatchesCode(string trait, string expected)
        {
            Assert.Contains(expected, (string)TraitEntry(trait)["description"]);
        }

        [Fact]
        public void LeakNotifications_MatchCode()
        {
            JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            Dictionary<string, string> text = Enumerable.Range(0, constants.Count).ToDictionary(i => (string)constants[i]["id"], i => (string)constants[i]["text"]);

            // One name is filled into "@", two into "@1" and "@2"
            float lost = -INDISCREET_MENTAL;
            Assert.Equal($"An indiscreet person has leaked that @ is dating. She lost {lost} mental stamina points.", text[INDISCREET_LABEL_OUTSIDE]);
            Assert.Equal($"An indiscreet person has leaked that @ is dating. She lost {lost} mental stamina points and gained 1 scandal point.", text[INDISCREET_LABEL_OUTSIDE_SCANDAL]);
            Assert.Equal($"An indiscreet person has leaked that @1 and @2 are dating each other. They both lost {lost} mental stamina points.", text[INDISCREET_LABEL_INSIDE]);
            Assert.Equal($"An indiscreet person has leaked that @1 and @2 are dating each other. They both lost {lost} mental stamina points and gained 1 scandal point.", text[INDISCREET_LABEL_INSIDE_SCANDAL]);
        }

        /// <summary>
        /// The Steam description's trait lines match the code.
        /// </summary>
        [Fact]
        public void SteamDescription_MatchesCode()
        {
            string text = File.ReadAllText(TestGame.ModAsset("steam description.txt"));
            float ageTraitSpeed = (VanillaPositiveDrift + MATERNAL_WEEKLY_BONUS / 2) / VanillaPositiveDrift;
            Assert.Equal(MATERNAL_BONUS, PRECOCIOUS_BONUS);

            string[] expected =
            {
                $"- Live Fast: double the rate of stat decreases after their peak age",
                Invariant($"- Trendy: {TRENDY_TEEN_MODIFIER}x the appeal to non-adults and {TRENDY_ADULT_MODIFIER}x the appeal to adults."),
                $"- Anxiety: {ANXIETY_MODIFIER} to all stats when a special event is waiting.",
                $"- Clumsy: +{CLUMSY_FUNNY_MODIFIER} to funny and {CLUMSY_DANCE_MODIFIER} to dance.",
                $"- Complacent: {COMPLACENT_MODIFIER} to vocal and dance if they were center in the latest single.",
                $"- Worrier: {WORRIER_MODIFIER} to all stats when there are scandal points.",
                $"- Defeatist: {DEFEATIST_MODIFIER} to all stats if the latest single does not top the charts.",
                $"- Underdog: +{UNDERDOG_MODIFIER} to all stats if the latest single does not top the charts.",
                $"- Lone Wolf: +{LONEWOLF_MODIFIER} to all stats when they are the only one assigned to a show.",
                $"- Photogenic: +{Percent(PHOTOGENIC_MODIFIER)}% to photoshoots",
                Invariant($"- Maternal: positive relationships with those younger than them by default and these relationships develop naturally {ageTraitSpeed}x faster."),
                Invariant($"- Precocious: positive relationships with those older than them by default and these relationships develop naturally {ageTraitSpeed}x faster."),
                "- Forgiving: will never dislike or hate any other girls.",
                $"- Meme Queen: +{MEME_INT_SHOW} to all stats for internet shows and get +{MEME_VIRAL_SUCCESS}% success rate and +{MEME_VIRAL_SUCCESS_CRIT}% crit success rate",
                Invariant($"- Annoying: causes other members to spend {1 + ANNOYING_MODIFIER}x physical stamina in shows."),
                $"- Misandry: {MISANDRY_CHANCE}% chance of receiving bad opinions from Male fans",
                $"- Perfectionist: {PERFECTIONIST_MENTAL} to mental stamina when world tours end with less than {PERFECTIONIST_TOUR_ATT}% attendance, or when they participate in concerts with less than {PERFECTIONIST_HYPE}% hype.",
                $"- Indiscreet: girls in dating relationships unknown to the player have a {INDISCREET_CHANCE}% chance of having the relationship revealed each week. The girls revealed to be dating lose {-INDISCREET_MENTAL} mental stamina.",
            };
            foreach (string line in expected)
                Assert.Contains(line, text);
        }

        /// <summary>
        /// The Steam changelog ends with an entry for the version in the project file.
        /// </summary>
        [Fact]
        public void SteamChangelog_ListsCurrentVersion()
        {
            string version = Regex.Match(File.ReadAllText(TestGame.ModFile("Traits Fix.csproj")), "<Version>(.+)</Version>").Groups[1].Value;
            string[] lines = File.ReadAllLines(TestGame.ModAsset("steam description.txt"));
            Assert.StartsWith($"- {version}: ", lines.Last());
        }
    }
}
