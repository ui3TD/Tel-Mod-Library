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

        public static IEnumerable<object[]> LeakEventIds() =>
            from kind in new[] { INDISCREET_KIND_BOYFRIEND, INDISCREET_KIND_GIRLFRIEND, INDISCREET_KIND_IDOLS }
            from n in Enumerable.Range(1, INDISCREET_EVENTS_PER_KIND)
            select new object[] { kind, n };

        private static string LeakEventId(string kind, int n) => $"{INDISCREET_EVENT_PREFIX}{kind}_{n}";

        private static Dictionary<string, Event_Manager._randomEvent> LeakEvents()
        {
            JSONNode events = TestGame.LoadJson("JSON/Events/random_events.json");
            Dictionary<string, Event_Manager._randomEvent> byId = new();
            for (int i = 0; i < events.Count; i++)
            {
                Event_Manager._randomEvent randomEvent = new();
                randomEvent.Set(events[i], "Traits Fix");
                byId.Add(randomEvent.id, randomEvent);
            }
            return byId;
        }

        [Fact]
        public void LeakEvents_AreExactlyTheOnesTheCodeStarts()
        {
            IEnumerable<string> expected = LeakEventIds().Select(row => LeakEventId((string)row[0], (int)row[1]));
            Assert.Equal(expected.OrderBy(id => id), LeakEvents().Keys.OrderBy(id => id));
        }

        /// <summary>
        /// Each leak event has the couple as its actors and names the culprit by its variable, only opens
        /// when the mod starts it, and has a 600x860 picture like the game's own events. Its one reply has no
        /// effects and "-" as its result, so the game shows nothing of its own when it's chosen: the mod
        /// applies the consequences and shows the results. There are two different replies, each with a
        /// result of its own in the mod's text.
        /// </summary>
        [Theory]
        [MemberData(nameof(LeakEventIds))]
        public void LeakEvent_MatchesCode(string kind, int n)
        {
            string id = LeakEventId(kind, n);
            Event_Manager._randomEvent leak = LeakEvents()[id];
            int couple = kind == INDISCREET_KIND_IDOLS ? 2 : 1;

            Assert.False(leak.random);
            Assert.Empty(leak.requirement);
            Assert.Empty(leak.effects);
            Assert.Equal(Enumerable.Range(0, couple).Select(i => $"@actor_{i}@"), leak.actor.Select(a => a.name));
            Assert.All(leak.actor, a => Assert.Equal(Event_Manager._randomEvent.actorType.idol, a.type));
            Assert.All(leak.actor, a => Assert.Empty(a.requirement));
            for (int i = 0; i < couple; i++)
                Assert.Contains($"[actor_{i}]", leak.description);
            Assert.DoesNotContain($"[actor_{couple}]", leak.description);
            Assert.Contains($"[{id}{INDISCREET_CULPRIT_SUFFIX}]", leak.description);

            Assert.Equal(2, leak.reply.Count);
            Assert.NotEqual(leak.reply[0].text, leak.reply[1].text);
            JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            HashSet<string> texts = new(Enumerable.Range(0, constants.Count).Select(i => (string)constants[i]["id"]));
            for (int i = 0; i < leak.reply.Count; i++)
            {
                Assert.False(string.IsNullOrWhiteSpace(leak.reply[i].text));
                Assert.Empty(leak.reply[i].Effects);
                Assert.Equal("-", leak.reply[i].description);
                Assert.Contains(id + INDISCREET_RESULT_SUFFIX + (i + 1), texts);
            }

            Assert.Equal($"indiscreet_{kind}_{n}.png", leak.image);
            string picture = TestGame.ModAsset(Path.Combine("Textures", "RandomEventIllustrations", leak.image));
            Assert.True(File.Exists(picture), $"{picture} missing");
            Assert.Equal((600, 860), PngSize(picture));
        }

        [Fact]
        public void LeakReactions_Exist()
        {
            JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            Dictionary<string, string> text = Enumerable.Range(0, constants.Count).ToDictionary(i => (string)constants[i]["id"], i => (string)constants[i]["text"]);
            Assert.False(string.IsNullOrWhiteSpace(text[INDISCREET_LABEL_REACTION_LOW]));
            Assert.False(string.IsNullOrWhiteSpace(text[INDISCREET_LABEL_REACTION_HIGH]));
        }

        private static (int width, int height) PngSize(string path)
        {
            byte[] header = new byte[24];
            using (FileStream file = File.OpenRead(path))
                Assert.Equal(header.Length, file.Read(header, 0, header.Length));
            Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, header.Take(4));
            int BigEndian(int offset) => (header[offset] << 24) | (header[offset + 1] << 16) | (header[offset + 2] << 8) | header[offset + 3];
            return (BigEndian(16), BigEndian(20));
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
                $"- Indiscreet: girls in dating relationships unknown to the player have a {INDISCREET_CHANCE}% chance of having the relationship revealed each week.",
                "Each leak shows as an illustrated event describing how the indiscreet idol let it slip.",
                $"The girls revealed to be dating lose {-INDISCREET_MENTAL} mental stamina (except Thick Skin and Shameless girls, as in the game's scandals) and get the consequences of the game's own dating scandal event",
                $"no new outside relationships for {SCANDAL_TEMPLATE_BLOCK_DAYS} days.",
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
