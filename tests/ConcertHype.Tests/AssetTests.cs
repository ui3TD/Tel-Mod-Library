using SimpleJSON;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ConcertHype.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private static string ModFile(params string[] parts) =>
            Path.Combine(new[] { ConfigFileTests.RepoRoot(), "mods", "Concert Hype" }.Concat(parts).ToArray());

        private static string SteamDescription() => File.ReadAllText(ModFile("assets", "steam description.txt"));

        /// <summary>
        /// The mod's constants, parsed the way the game's Language loader does.
        /// </summary>
        private static Dictionary<string, string> Constants()
        {
            JSONNode constants = mainScript.ProcessInboundData(File.ReadAllText(ModFile("assets", "JSON", "Constants", "constants.json")));
            return Enumerable.Range(0, constants.Count).ToDictionary(i => constants[i]["id"].Value, i => constants[i]["text"].Value);
        }

        [Fact]
        public void InfoJsonDescription_SaysWhatTheModDoes()
        {
            string csproj = File.ReadAllText(ModFile("Concert Hype.csproj"));
            Assert.Equal("A new concert minigame: manage each song's lineup to build and hold the crowd's hype.",
                Regex.Match(csproj, "<ModDescription>(.+)</ModDescription>").Groups[1].Value);
        }

        [Fact]
        public void SteamDescription_LinksTheRequirementAndSource()
        {
            string description = SteamDescription();
            Assert.Contains("[h1]REQUIRES: IM-HarmonyIntegration[/h1]", description);
            Assert.Contains("[url=https://github.com/ui3TD/IM-HarmonyIntegration]", description);
            Assert.Contains("[url=https://github.com/ui3TD/Tel-Mod-Library]source code[/url]", description);
        }

        [Fact]
        public void SteamDescription_StatesTheRulesTheCodeUses()
        {
            string description = SteamDescription();
            Assert.Contains("triangle formation", description);
            Assert.Contains("every idol in your agency who isn't on stage", description);
            Assert.Contains("swap them in, as many as you like", description);
            Assert.Contains("replaced by the best idol on the bench", description);
            Assert.Contains("twice per show", description);
            Assert.Contains("play in 5 seconds instead of 10", description);
            Assert.Contains("it pays what 200% hype paid before", description);
            Assert.Contains("Action cards, the accident wheel and talk breaks in the setlist are off", description);
            ConcertHype.Model.Config config = new();
            Assert.Equal(2, config.McCharges);
            Assert.Equal(5, config.SongSeconds);
            Assert.Equal(10, Pace.GameSeconds);
        }

        [Fact]
        public void EveryIcon_IsA64PixelRgbaPng_AndEveryPngIsAnIcon()
        {
            string folder = ModFile("assets", Icons.Folder);
            List<string> expected = System.Enum.GetValues(typeof(IconKind)).Cast<IconKind>().Where(k => k != IconKind.None).Select(Icons.FileName).ToList();
            Assert.Equal(expected.OrderBy(n => n), Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n));
            foreach (string name in expected)
            {
                byte[] png = File.ReadAllBytes(Path.Combine(folder, name));
                Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8));
                int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
                int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
                Assert.True(width == 64 && height == 64, $"{name} is {width}x{height}");
                Assert.True(png[24] == 8 && png[25] == 6, $"{name} isn't 8-bit RGBA");
            }
        }

        [Fact]
        public void ModMenu_SwitchHasItsLabel_AndDefaultsOn()
        {
            JSONNode rows = JSON.Parse(File.ReadAllText(ModFile("assets", "JSON", "Mod Menu", "modmenu.json")));
            Assert.Equal(1, rows.Count);
            Assert.Equal(Switch.VarId, rows[0]["varID"].Value);
            Assert.Equal("true", rows[0]["defaultValue"].Value);
            Assert.Equal("New concert minigame (off: original concerts)", Constants()[rows[0]["labelID"].Value]);
        }
    }
}
