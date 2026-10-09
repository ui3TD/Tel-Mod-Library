using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static CustomAuditions.CustomAuditions;

namespace TargetedAuditions.Tests
{
    /// <summary>
    /// The shipped Mod Menu and text files agree with the code.
    /// </summary>
    public class AssetTests
    {
        private static SimpleJSON.JSONNode ModMenu() =>
            SimpleJSON.JSON.Parse(File.ReadAllText(Seams.ModAsset(Path.Combine("JSON", "Mod Menu", "modmenu.json"))));

        private static IEnumerable<SimpleJSON.JSONNode> Items(SimpleJSON.JSONNode menu)
        {
            for (int i = 0; i < menu.Count; i++)
                yield return menu[i];
        }

        private static SimpleJSON.JSONNode Item(string varID) =>
            Items(ModMenu()).Single(i => i["varID"].Value == varID && i["ignore"].Value != "true");

        private static HashSet<string> TextIDs()
        {
            string text = File.ReadAllText(Seams.ModAsset(Path.Combine("JSON", "Constants", "constants.json")));
            return new HashSet<string>(Regex.Matches(text, @"id:\s*""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value));
        }

        public static IEnumerable<object[]> Defaults => new[]
        {
            new object[] { VARID_MINAGE, DEF_MINAGE_STR },
            new object[] { VARID_MAXAGE, DEF_MAXAGE_STR },
            new object[] { VARID_LESCHANCE, DEF_CHANCE_LES_STR },
            new object[] { VARID_BICHANCE, DEF_CHANCE_BI_STR },
            new object[] { VARID_COUNT, DEF_COUNT },
        }.Concat(paramTypes.Select(p => new object[] { VARID_PRIO_PREFIX + p, DEF_PRIO }));

        /// <summary>
        /// Before the player opens the Mod Menu, the code's fallback must equal what the menu shows.
        /// </summary>
        [Theory]
        [MemberData(nameof(Defaults))]
        public void MenuDefault_MatchesCodeFallback(string varID, string fallback)
        {
            Assert.Equal(fallback, Item(varID)["defaultValue"].Value);
        }

        [Fact]
        public void EveryShownSetting_IsReadByTheCode()
        {
            HashSet<string> read = new(Defaults.Select(d => (string)d[0]));
            List<string> shown = Items(ModMenu()).Where(i => i["ignore"].Value != "true" && !string.IsNullOrEmpty(i["varID"].Value)).Select(i => i["varID"].Value).ToList();

            Assert.Equal(shown.Count, shown.Distinct().Count());
            Assert.Equal(read.OrderBy(v => v), shown.OrderBy(v => v));
        }

        [Fact]
        public void SliderDefaults_AreInRange()
        {
            foreach (SimpleJSON.JSONNode item in Items(ModMenu()).Where(i => i["type"].Value == "slider"))
                Assert.InRange(item["defaultValue"].AsInt, item["minValue"].AsInt, item["maxValue"].AsInt);
        }

        /// <summary>
        /// Priorities start at 1, so the priority roll always has a non-zero total.
        /// </summary>
        [Fact]
        public void PrioritySliders_StartAtOne()
        {
            foreach (data_girls._paramType p in paramTypes)
                Assert.Equal(1, Item(VARID_PRIO_PREFIX + p)["minValue"].AsInt);
        }

        /// <summary>
        /// The retired per-audition age popup leaves no menu entries or text behind.
        /// </summary>
        [Fact]
        public void AgePopup_IsGone()
        {
            Assert.DoesNotContain(Items(ModMenu()), i => i["varID"].Value == "AuditionAgeLimit_TogglePopup");
            Assert.DoesNotContain(TextIDs(), id => id.StartsWith("AGELIMIT__") || id == "AUDITIONAGELIMIT__MODMENU__TOGGLE");
            Assert.False(File.Exists(Path.Combine(Seams.RepoRoot(), "mods", "Targeted Auditions", "Popup UI.cs")));
        }

        [Fact]
        public void EveryLabel_HasText()
        {
            HashSet<string> ids = TextIDs();
            foreach (SimpleJSON.JSONNode item in Items(ModMenu()))
                Assert.Contains(item["labelID"].Value, ids);
        }
    }
}
