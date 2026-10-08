using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static MenuHotkeys.Tests.TestGame;

namespace MenuHotkeys.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private static string SteamDescription() => File.ReadAllText(ModAsset("steam description.txt"));

        [Fact]
        public void InfoJsonDescription_SaysWhatTheModDoes()
        {
            string csproj = File.ReadAllText(ModFile("Menu Hotkeys.csproj"));
            Assert.Equal("Home row on the keyboard (asdfg...) are hotkeys for the main menu.", Regex.Match(csproj, "<ModDescription>(.+)</ModDescription>").Groups[1].Value);
        }

        /// <summary>
        /// The description lists exactly the hotkeys HotkeyTests checks, in home row order.
        /// </summary>
        [Fact]
        public void SteamDescription_ListsEveryHotkey()
        {
            string[] listed = Regex.Matches(SteamDescription(), @"^([A-Z]) - (.+?)\r?$", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(m => $"{m.Groups[1].Value} - {m.Groups[2].Value}")
                .ToArray();

            Assert.Equal(Hotkeys.Select(h => $"{h.Key} - {h.Name}"), listed);
        }

        [Fact]
        public void SteamDescription_LinksTheRequirementAndSource()
        {
            string description = SteamDescription();
            Assert.Contains("[h1]REQUIRES: IM-HarmonyIntegration[/h1]", description);
            Assert.Contains("[url=https://github.com/ui3TD/IM-HarmonyIntegration]", description);
            Assert.Contains("[url=https://github.com/ui3TD/Tel-Mod-Library]source code[/url]", description);
        }
    }
}
