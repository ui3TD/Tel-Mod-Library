using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ModMenus.Tests
{
    // Installing the Mod Settings button and building the popup's frame (GenerateMenuPopup) only move
    // Unity UI objects around, so they aren't tested. MenuBuildTests and ApplyCancelTests cover the
    // menu's rows, Apply and Cancel, through copies that run on a fake scene (see TestGame.cs).
    public class SliderRangeTests
    {
        private static (float min, float max, float def) Read(string json)
        {
            ModMenusUtils.GetSliderRange(JSON.Parse(json), out float min, out float max, out float def);
            return (min, max, def);
        }

        [Fact]
        public void NoFieldsGivesZeroToHundredAtMidpoint()
        {
            Assert.Equal((0f, 100f, 50f), Read("{\"type\": \"slider\"}"));
        }

        [Fact]
        public void MissingDefaultIsMidpointOfRange()
        {
            // The old formula, max + min / 2, gave 20.5: above the range.
            Assert.Equal((1f, 20f, 10.5f), Read("{\"minValue\": 1, \"maxValue\": 20}"));
        }

        [Fact]
        public void MissingDefaultIsMidpointOfNegativeRange()
        {
            Assert.Equal((-10f, 10f, 0f), Read("{\"minValue\": -10, \"maxValue\": 10}"));
        }

        [Fact]
        public void DefaultValueIsUsedWhenSet()
        {
            Assert.Equal((1f, 20f, 5f), Read("{\"minValue\": 1, \"maxValue\": 20, \"defaultValue\": 5}"));
        }

        [Theory]
        [InlineData("{\"minValue\": 10}")]
        [InlineData("{\"maxValue\": 10}")]
        public void RangeNeedsBothMinAndMax(string json)
        {
            Assert.Equal((0f, 100f, 50f), Read(json));
        }
    }

    public class SiblingIndexTests
    {
        // Settings list: 8 buttons, vanilla Settings at index 2.
        [Theory]
        [InlineData(7, 2, 8, 3)] // newly cloned button at the end moves under Settings
        [InlineData(3, 2, 8, 3)] // already under Settings stays put
        [InlineData(0, 2, 8, 2)] // above Settings: Settings shifts up when it's removed
        [InlineData(5, 7, 8, 7)] // Settings last: goes to the end
        [InlineData(3, 9, 8, 7)] // clamped to the list
        public void PlacesItemDirectlyAfterAnchor(int itemIndex, int anchorIndex, int childCount, int expected)
        {
            Assert.Equal(expected, ModMenusUtils.SiblingIndexAfter(itemIndex, anchorIndex, childCount));
        }

        [Theory]
        [InlineData(7, 2)]
        [InlineData(0, 2)]
        [InlineData(1, 5)]
        [InlineData(6, 0)]
        public void ResultIsDirectlyAfterAnchorOnceMoved(int itemIndex, int anchorIndex)
        {
            // Simulate Transform.SetSiblingIndex: remove the item, insert it at the new index.
            List<string> list = Enumerable.Range(0, 8).Select(i => "b" + i).ToList();
            string item = list[itemIndex];
            string anchor = list[anchorIndex];

            int target = ModMenusUtils.SiblingIndexAfter(itemIndex, anchorIndex, list.Count);
            list.RemoveAt(itemIndex);
            list.Insert(target, item);

            Assert.Equal(list.IndexOf(anchor) + 1, list.IndexOf(item));
        }
    }

    // Checks the menus our mods ship, so a bad modmenu.json fails here instead of in game.
    public class ShippedMenuTests
    {
        private static string RepoRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static IEnumerable<object[]> MenuFiles()
        {
            string mods = Path.Combine(RepoRoot(), "mods");
            return Directory.GetFiles(mods, ModMenusUtils.JSON_FILE, SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == ModMenusUtils.JSON_DIR)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                         && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .Select(f => new object[] { f.Substring(mods.Length + 1) });
        }

        // Only the items ModMenus shows: ignored ones are skipped in AddMenuItems.
        private static List<JSONNode> Items(string relativePath)
        {
            string text = File.ReadAllText(Path.Combine(RepoRoot(), "mods", relativePath));
            JSONArray array = JSON.Parse(text).AsArray;
            Assert.NotNull(array);
            return Enumerable.Range(0, array.Count)
                .Select(i => array[i])
                .Where(item => !item[ModMenusUtils.JSON_FIELD_IGNORE].AsBool)
                .ToList();
        }

        [Fact]
        public void MenusAreFound()
        {
            Assert.True(MenuFiles().Count() >= 3);
        }

        [Theory]
        [MemberData(nameof(MenuFiles))]
        public void SliderDefaultsAreInRange(string menu)
        {
            // SimpleJSON's == compares references, so compare the type's text
            foreach (JSONNode item in Items(menu).Where(i => i[ModMenusUtils.JSON_FIELD_TYPE].Value == ModMenusUtils.JSON_TYPE_SLIDER))
            {
                ModMenusUtils.GetSliderRange(item, out float min, out float max, out float def);
                Assert.True(min < max, $"{item[ModMenusUtils.JSON_FIELD_VARID]}: range {min}..{max}");
                Assert.InRange(def, min, max);
            }
        }

        [Theory]
        [MemberData(nameof(MenuFiles))]
        public void DropdownDefaultsAreInList(string menu)
        {
            foreach (JSONNode item in Items(menu).Where(i => i[ModMenusUtils.JSON_FIELD_TYPE].Value == ModMenusUtils.JSON_TYPE_DROPDOWN))
            {
                int count = item[ModMenusUtils.JSON_FIELD_LIST].AsArray.Count;
                Assert.InRange(item[ModMenusUtils.JSON_FIELD_DEF].AsInt, 0, count - 1);
            }
        }

        [Fact]
        public void SettingIdsAreUniqueAcrossMods()
        {
            List<string> ids = MenuFiles()
                .SelectMany(f => Items((string)f[0]))
                .Select(i => (string)i[ModMenusUtils.JSON_FIELD_VARID])
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }
}
