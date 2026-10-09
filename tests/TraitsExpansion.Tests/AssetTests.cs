using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static System.FormattableString;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansionTests
{
    /// <summary>
    /// The in-game and Steam trait text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private static readonly NewTraits[] Negative =
        {
            NewTraits.Wooden_Acting, NewTraits.Sadistic, NewTraits.Reckless, NewTraits.Job_Hopper,
            NewTraits.Aerophobia, NewTraits.Stage_Fright, NewTraits.Tone_Deaf, NewTraits.Homely,
        };

        private static int Percent(float coeff) => (int)Math.Round(coeff * 100);

        /// <summary>
        /// The numbers each in-game description must state.
        /// </summary>
        public static IEnumerable<object[]> InGameDescriptions() => new[]
        {
            new object[] { NewTraits.Perfect_Pitch, $"+{PERFPITCH_BONUS} bonus to Vocals" },
            new object[] { NewTraits.Polyglot, $"{Percent(POLYGLOT_BONUS)}% more fans" },
            new object[] { NewTraits.Beauty_Guru, $"+{BEAUTYGURU_BONUS} bonus to Pretty" },
            new object[] { NewTraits.Mensa_Member, $"+{MENSA_BONUS} bonus to Smart" },
            new object[] { NewTraits.Quick_Wit, $"+{Percent(QUICKWIT_BONUS)}% bonus to variety shows" },
            new object[] { NewTraits.Fashionista, $"+{Percent(FASHIONISTA_COEFF - 1)}% bonus appeal for Females" },
            new object[] { NewTraits.Flirty, $"+{Percent(FLIRTY_COEFF - 1)}% bonus appeal for Males" },
            new object[] { NewTraits.Well_Endowed, $"+{WELLENDOWED_BONUS} bonus to Sexy" },
            new object[] { NewTraits.Idol_Otaku, $"+{Percent(OTAKU_COEFF - 1)}% bonus appeal for Hardcore" },
            new object[] { NewTraits.Wooden_Acting, $"-{Percent(WOODACTING_PENALTY)}% penalty to drama" },
            new object[] { NewTraits.Sadistic, "2x the mental stamina" },
            new object[] { NewTraits.Reckless, "3x more prone to injury" },
            new object[] { NewTraits.Aerophobia, $"-{AEROPHOB_PENALTY} mental stamina" },
            new object[] { NewTraits.Stage_Fright, $"-{STAGEFRIGHT_PENALTY} mental stamina" },
            new object[] { NewTraits.Cult_Leader, $"+{Percent(CULT_COEFF - 1)}% votes" },
            new object[] { NewTraits.Tone_Deaf, $"-{TONEDEAF_PENALTY} penalty to vocals" },
            new object[] { NewTraits.Homely, $"-{HOMELY_PENALTY} penalty to all visual stats" },
            new object[] { NewTraits.Thespian, $"-{Percent(1 - THESPIAN_COEFF)}% stamina cost for Drama" },
        };

        private static JSONNode TraitEntry(NewTraits trait)
        {
            JSONNode list = TestGame.LoadJson("JSON/Idols/traits.json");
            for (int i = 0; i < list.Count; i++)
            {
                if ((string)list[i]["type"] == trait.ToString())
                    return list[i];
            }
            Assert.Fail($"{trait} missing from traits.json");
            return null;
        }

        [Fact]
        public void TraitsJson_ListsEveryTraitOnce()
        {
            JSONNode list = TestGame.LoadJson("JSON/Idols/traits.json");
            List<string> types = Enumerable.Range(0, list.Count).Select(i => (string)list[i]["type"]).ToList();
            List<string> expected = Enum.GetNames(typeof(NewTraits)).Where(n => n != nameof(NewTraits.none)).ToList();

            Assert.Equal(expected.OrderBy(n => n), types.OrderBy(n => n));
        }

        [Theory]
        [MemberData(nameof(InGameDescriptions))]
        public void InGameDescription_MatchesCode(NewTraits trait, string expected)
        {
            Assert.Contains(expected, (string)TraitEntry(trait)["description"]);
        }

        [Fact]
        public void NegativeTraits_MarkedNegative()
        {
            foreach (NewTraits trait in Enum.GetValues(typeof(NewTraits)))
            {
                if (trait == NewTraits.none)
                    continue;
                JSONNode positive = TraitEntry(trait)["positive"];
                bool isPositive = positive == null || positive.AsBool;
                Assert.True(isPositive != Negative.Contains(trait), $"{trait} positive: {isPositive}");
            }
        }

        /// <summary>
        /// Each trait's intro names an introduction event the mod ships.
        /// </summary>
        [Fact]
        public void EveryIntro_HasEvent()
        {
            JSONNode events = TestGame.LoadJson("JSON/Events/dialogues.json");
            Dictionary<string, string> types = new();
            for (int i = 0; i < events.Count; i++)
                types[events[i]["id"]] = events[i]["type"];

            foreach (NewTraits trait in Enum.GetValues(typeof(NewTraits)))
            {
                if (trait == NewTraits.none)
                    continue;
                string intro = TraitEntry(trait)["intro"];
                Assert.Equal($"intro_{trait.ToString().ToLowerInvariant()}", intro);
                Assert.True(types.TryGetValue(intro, out string type), $"{intro} missing from dialogues.json");
                Assert.Equal("introduction", type);
            }
        }

        /// <summary>
        /// The notification takes the idol's name and the penalty from the code, so the number has one source.
        /// </summary>
        [Fact]
        public void StageFrightNotification_TakesThePenaltyFromTheCode()
        {
            JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            JSONNode entry = Enumerable.Range(0, constants.Count).Select(i => constants[i]).Single(c => (string)c["id"] == "IDOL__STAGEFRIGHT");
            Assert.Equal("@1 lost @2 mental stamina due to stage fright.", (string)entry["text"]);

            Language.Data["IDOL__STAGEFRIGHT"] = entry["text"];
            Assert.Equal("Aya lost 10 mental stamina due to stage fright.",
                Language.Insert("IDOL__STAGEFRIGHT", "Aya", STAGEFRIGHT_PENALTY.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Sadistic doubles vanilla's weekly 10, and Reckless triples vanilla's injury odds
        /// (0.5% below 20 physical stamina, 1% at 5 or below).
        /// </summary>
        [Fact]
        public void MultiplierClaims_MatchVanilla()
        {
            Assert.Equal(10, SADISTIC_MODIFIER);
            Assert.Equal(2 * 0.5f, RECKLESS_CHANCE);
            Assert.Equal(2 * 1f, RECKLESS_CHANCE_SEVERE);
            Assert.Equal(5f, RECKLESS_THR_LOWER);
        }

        /// <summary>
        /// The Steam description's trait lines match the code.
        /// </summary>
        [Fact]
        public void SteamDescription_MatchesCode()
        {
            string text = File.ReadAllText(TestGame.ModAsset("steam description.txt"));
            string[] expected =
            {
                $"- Perfect Pitch: +{PERFPITCH_BONUS} to vocal",
                $"- Tone Deaf: -{TONEDEAF_PENALTY} to vocal",
                $"- Beauty Guru: +{BEAUTYGURU_BONUS} to pretty",
                $"- Mensa Member: +{MENSA_BONUS} to smart",
                $"- Well Endowed: +{WELLENDOWED_BONUS} to sexy",
                $"- Homely: -{HOMELY_PENALTY} to pretty, cute, sexy and cool",
                $"- Fashionista: +{Percent(FASHIONISTA_COEFF - 1)}% appeal to females",
                $"- Flirty: +{Percent(FLIRTY_COEFF - 1)}% appeal to males",
                $"- Idol Otaku: +{Percent(OTAKU_COEFF - 1)}% appeal to hardcore",
                $"- Thespian: -{Percent(1 - THESPIAN_COEFF)}% stamina for dramas",
                Invariant($"- Wooden Acting: -{WOODACTING_PENALTY} to her reward multiplier for dramas (-{Percent(WOODACTING_PENALTY)}% at 50 in the job's skill)"),
                Invariant($"- Quick Wit: +{QUICKWIT_BONUS} to her reward multiplier for variety shows (+{Percent(QUICKWIT_BONUS)}% at 50 in the job's skill)"),
                $"- Cult Leader: +{Percent(CULT_COEFF - 1)}% votes in elections",
                $"- Stage Fright: -{STAGEFRIGHT_PENALTY} mental stamina each time she's MCing or centering in a concert",
                $"- Aerophobia: -{AEROPHOB_PENALTY} mental stamina when going on world tour",
                $"- Polyglot: {Percent(POLYGLOT_BONUS)}% more fans in world tours",
                "- Sadistic: Doubles the amount of mental stamina damage from bullying",
                $"- Reckless: Is 3x more likely to get injured, and can get injured if stamina is less than {RECKLESS_THR_UPPER}.",
            };
            foreach (string line in expected)
                Assert.Contains(line, text);
        }

        /// <summary>
        /// The Steam description lists each trait's save-file number.
        /// </summary>
        [Fact]
        public void SteamDescription_TraitNumbersMatch()
        {
            string text = File.ReadAllText(TestGame.ModAsset("steam description.txt"));
            Dictionary<string, int> listed = Regex.Matches(text, @"^(\w+) = (\d+)\r?$", RegexOptions.Multiline)
                .Cast<Match>()
                .ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));

            Dictionary<string, int> expected = Enum.GetValues(typeof(NewTraits)).Cast<NewTraits>()
                .Where(t => t != NewTraits.none)
                .ToDictionary(t => t.ToString(), t => (int)t);

            Assert.Equal(expected.OrderBy(p => p.Key), listed.OrderBy(p => p.Key));
        }
    }
}
