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
            Assert.Contains("twice per show", description);
            Assert.Contains("it pays what 200% hype paid before", description);
            Assert.Contains("Action cards, the accident wheel and MC breaks in the setlist are off", description);
            Assert.Equal(2, new ConcertHype.Model.Config().McCharges);
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
