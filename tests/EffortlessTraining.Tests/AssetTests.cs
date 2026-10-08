using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace EffortlessTraining.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private static string SteamDescription() => File.ReadAllText(TestGame.ModAsset("steam description.txt"));

        [Fact]
        public void InfoJsonDescription_SaysWhatTheModDoes()
        {
            string csproj = File.ReadAllText(TestGame.ModFile("Effortless Training.csproj"));
            Assert.Equal("Training costs 3x less stamina.", Regex.Match(csproj, "<ModDescription>(.+)</ModDescription>").Groups[1].Value);
        }

        [Fact]
        public void SteamDescription_SaysWhatTheModDoes()
        {
            Assert.Contains("vocal/dance stamina cost is reduced to 1 pt/day", SteamDescription());
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
