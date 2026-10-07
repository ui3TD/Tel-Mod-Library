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
        /// The per-audition age popup's toggle stays hidden, and defaults to off in code.
        /// </summary>
        [Fact]
        public void AgePopupToggle_IsHiddenAndOffByDefault()
        {
            List<SimpleJSON.JSONNode> toggles = Items(ModMenu()).Where(i => i["varID"].Value == VARID_AGELIMIT_POPUP_TOGGLE).ToList();
            Assert.NotEmpty(toggles);
            Assert.All(toggles, t => Assert.Equal("true", t["ignore"].Value));
            Assert.Equal("0", DEF_AGELIMIT_POPUP_TOGGLE);
        }

        [Fact]
        public void EveryLabel_HasText()
        {
            HashSet<string> ids = TextIDs();
            foreach (SimpleJSON.JSONNode item in Items(ModMenu()))
                Assert.Contains(item["labelID"].Value, ids);
        }

        [Fact]
        public void PopupText_Exists()
        {
            HashSet<string> ids = TextIDs();
            string popupSource = File.ReadAllText(Path.Combine(Seams.RepoRoot(), "mods", "Targeted Auditions", "Popup UI.cs"));
            List<string> used = Regex.Matches(popupSource, @"""(AGELIMIT__\w+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

            Assert.NotEmpty(used);
            Assert.All(used, id => Assert.Contains(id, ids));
        }
    }
}
