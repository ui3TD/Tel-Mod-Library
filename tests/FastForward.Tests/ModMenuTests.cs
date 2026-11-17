using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FastForward.Tests
{
    /// <summary>
    /// With ModMenus installed, the speed starts at the menu's default; without it, at the code's.
    /// The two must be the same.
    /// </summary>
    public class ModMenuTests
    {
        /// <summary>
        /// A row of the mod's modmenu.json, which the test project copies next to the tests.
        /// </summary>
        private static SimpleJSON.JSONNode Setting(string varID)
        {
            string menu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modmenu.json");
            SimpleJSON.JSONArray items = SimpleJSON.JSON.Parse(File.ReadAllText(menu)).AsArray;
            return Enumerable.Range(0, items.Count).Select(i => items[i]).Single(i => i["varID"].Value == varID);
        }

        [Fact]
        public void CodeDefault_MatchesTheModMenu()
        {
            Assert.Equal(FastForward.DEFAULT_MULTIPLIER, Setting(FastForward.VARID)["defaultValue"].AsDouble);
        }

        /// <summary>
        /// Added in 1.4.0: the super-fast key is a key row, 4 unless the player changes it.
        /// </summary>
        [Fact]
        public void TheKeyIsAKeyRowWithTheCodesDefault()
        {
            SimpleJSON.JSONNode key = Setting(Controls_Update.KEY_VAR);

            Assert.Equal(("keybind", "Alpha4"), (key["type"].Value, key["defaultValue"].Value));
        }
    }
}
