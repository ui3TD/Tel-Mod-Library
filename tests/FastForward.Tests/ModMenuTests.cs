using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace FastForward.Tests
{
    /// <summary>
    /// With ModMenus installed, the speed starts at the menu's default; without it, at the code's.
    /// The two must be the same.
    /// </summary>
    public class ModMenuTests
    {
        private static SimpleJSON.JSONNode Setting([CallerFilePath] string testFile = "")
        {
            string menu = Path.Combine(Path.GetDirectoryName(testFile), "..", "..", "mods", "FastForward", "assets", "JSON", "Mod Menu", "modmenu.json");
            SimpleJSON.JSONArray items = SimpleJSON.JSON.Parse(File.ReadAllText(menu)).AsArray;
            return Enumerable.Range(0, items.Count).Select(i => items[i]).Single(i => i["varID"].Value == FastForward.VARID);
        }

        [Fact]
        public void CodeDefault_MatchesTheModMenu()
        {
            Assert.Equal(FastForward.DEFAULT_MULTIPLIER, Setting()["defaultValue"].AsDouble);
        }
    }
}
