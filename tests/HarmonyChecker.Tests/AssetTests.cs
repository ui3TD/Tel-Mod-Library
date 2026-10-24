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
        /// The mod ships four labels: the game's own Mods label, changed to say IM-HI isn't installed (shown when
        /// only the mod's JSON loads), the update button's label with the required version (@2), and the badge's
        /// two tooltips, each worded like the mod's fallback.
        /// </summary>
        [Fact]
        public void Constants_DefineAllLabels()
        {
            JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");

            Dictionary<string, string> text = Enumerable.Range(0, constants.Count).ToDictionary(i => constants[i]["id"].Value, i => constants[i]["text"].Value);
            Assert.Equal(4, text.Count);
            Assert.Equal(TestGame.NotInstalledText, text[TestGame.VanillaConstant]);
            Assert.Equal(IMHIUpdate.FALLBACK_LABEL, text[IMHIUpdate.BUTTON_LABEL]);
            Assert.Equal(StatusBadge.OK_FALLBACK, text[StatusBadge.OK_TEXT]);
            Assert.Equal(StatusBadge.OUTDATED_FALLBACK, text[StatusBadge.OUTDATED_TEXT]);
        }

        /// <summary>
        /// The badge pictures are round: square, twice the badge's size (the menu is drawn at twice its pixel
        /// density), clear in the corners and solid in the middle.
        /// </summary>
        [Theory]
        [InlineData("IMHI_Badge_OK.png")]
        [InlineData("IMHI_Badge_Outdated.png")]
        public void BadgePictures_AreRound(string name)
        {
            using System.Drawing.Bitmap picture = new(TestGame.ModAsset(Path.Combine("Images", name)));
            int size = (int)(2 * StatusBadge.SIZE);

            Assert.Equal((size, size), (picture.Width, picture.Height));
            Assert.Equal(0, picture.GetPixel(0, 0).A);
            Assert.Equal(0, picture.GetPixel(size - 1, size - 1).A);
            Assert.Equal(255, picture.GetPixel(size / 2, 4).A);
        }

        [Fact]
        public void BadgePictures_AreTheOnesLoaded()
        {
            Assert.Equal(Path.Combine("Images", "IMHI_Badge_OK.png"), StatusBadge.Ok.RelativePath);
            Assert.Equal(Path.Combine("Images", "IMHI_Badge_Outdated.png"), StatusBadge.Outdated.RelativePath);
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
        /// The Steam changelog starts with an entry for the version in the project file, short enough for
        /// the game's 100-character change notes box.
        /// </summary>
        [Fact]
        public void SteamChangelog_ListsCurrentVersion()
        {
            string version = Regex.Match(File.ReadAllText(TestGame.ModFile("Harmony Checker.csproj")), "<Version>(.+)</Version>").Groups[1].Value;
            string first = File.ReadAllLines(TestGame.ModAsset("steam description.txt")).SkipWhile(l => l != "[h3]Changelog[/h3]").ElementAt(1);
            Assert.StartsWith($"- {version}: ", first);
            Assert.InRange(first.Length - "- ".Length, 1, 100);
        }
    }
}
