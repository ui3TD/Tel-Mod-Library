using SimpleJSON;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HarmonyChecker.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        public AssetTests() => TestGame.Reset();

        /// <summary>
        /// The mod ships two labels: the game's own Mods label, changed to say IM-HI isn't installed (shown when
        /// only the mod's JSON loads), and the label the patches switch to.
        /// </summary>
        [Fact]
        public void Constants_DefineBothLabels()
        {
            JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");

            Dictionary<string, string> text = Enumerable.Range(0, constants.Count).ToDictionary(i => constants[i]["id"].Value, i => constants[i]["text"].Value);
            Assert.Equal(2, text.Count);
            Assert.Equal(TestGame.NotInstalledText, text[TestGame.VanillaConstant]);
            Assert.Equal("Mods [IM-HI installed]", text[HarmonyCheckerStatus.BUTTON_LABEL]);
        }

        /// <summary>
        /// The Steam description says what the label shows and where to get IM-HI. SteamCMD uploads can't
        /// contain double quotes.
        /// </summary>
        [Fact]
        public void SteamDescription_PointsToIMHI()
        {
            string text = File.ReadAllText(TestGame.ModAsset("steam description.txt"));

            Assert.Contains("https://github.com/ui3TD/IM-HarmonyIntegration", text);
            Assert.DoesNotContain("\"", text);
        }

        /// <summary>
        /// The Steam changelog ends with an entry for the version in the project file, short enough for
        /// the game's 100-character change notes box.
        /// </summary>
        [Fact]
        public void SteamChangelog_ListsCurrentVersion()
        {
            string version = Regex.Match(File.ReadAllText(TestGame.ModFile("Harmony Checker.csproj")), "<Version>(.+)</Version>").Groups[1].Value;
            string last = File.ReadAllLines(TestGame.ModAsset("steam description.txt")).Last();
            Assert.StartsWith($"- {version}: ", last);
            Assert.InRange(last.Length - "- ".Length, 1, 100);
        }
    }
}
