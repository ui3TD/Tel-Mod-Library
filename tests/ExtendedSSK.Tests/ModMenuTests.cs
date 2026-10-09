using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace ExtendedSSK.Tests
{
    /// <summary>
    /// With ModMenus installed, the number of ranks starts at the menu's default; without it, at the code's.
    /// The two must be the same.
    /// </summary>
    public class ModMenuTests
    {
        private static SimpleJSON.JSONNode Setting([CallerFilePath] string testFile = "")
        {
            string menu = Path.Combine(Path.GetDirectoryName(testFile), "..", "..", "mods", "Extended SSK", "assets", "JSON", "Mod Menu", "modmenu.json");
            SimpleJSON.JSONArray items = SimpleJSON.JSON.Parse(File.ReadAllText(menu)).AsArray;
            return Enumerable.Range(0, items.Count).Select(i => items[i]).Single(i => i["varID"].Value == ExtendedSSK.varID);
        }

        [Fact]
        public void CodeDefault_MatchesTheModMenu()
        {
            Assert.Equal(ExtendedSSK.defaultRankings, Setting()["defaultValue"].AsInt);
        }
    }
}
