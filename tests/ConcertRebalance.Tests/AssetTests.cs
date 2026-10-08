using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ConcertRebalance.Tests
{
    /// <summary>
    /// The in-game text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        /// <summary>
        /// The mod's constants, parsed the way the game's Language loader does.
        /// </summary>
        private static Dictionary<string, string> Constants()
        {
            string path = Path.Combine(RepoRoot(), "mods", "Concert Rebalance", "assets", "JSON", "Constants", "constants.json");
            JSONNode constants = mainScript.ProcessInboundData(File.ReadAllText(path));
            return Enumerable.Range(0, constants.Count).ToDictionary(i => (string)constants[i]["id"], i => (string)constants[i]["text"]);
        }

        [Fact]
        public void Constants_ReplaceOnlyTheVenueUnlockText()
        {
            Assert.Equal(new[] { "CONCERT__VENUE_UNLOCK", "CONCERT__VENUE_UNLOCK_2" }, Constants().Keys.OrderBy(id => id));
        }

        [Fact]
        public void LockedVenueTooltip_StatesTheUnlockRule()
        {
            // Concert_Venue joins the two around the previous venue's name.
            // The game's text is "Hold a Club concert to unlock this venue".
            Dictionary<string, string> text = Constants();
            Assert.Equal("Sell out a Club concert with a profit to unlock this venue",
                text["CONCERT__VENUE_UNLOCK"] + "Club" + text["CONCERT__VENUE_UNLOCK_2"]);
        }
    }
}
