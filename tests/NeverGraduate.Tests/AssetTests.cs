using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace NeverGraduate.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private const string Description = "This makes the game never check for graduations, unless the girl is fired.";

        private static string SteamDescription() => File.ReadAllText(TestGame.ModAsset("steam description.txt"));

        [Fact]
        public void InfoJsonDescription_SaysWhatTheModDoes()
        {
            string csproj = File.ReadAllText(TestGame.ModFile("Never Graduate.csproj"));
            Assert.Equal(Description, Regex.Match(csproj, "<ModDescription>(.+)</ModDescription>").Groups[1].Value);
        }

        [Fact]
        public void SteamDescription_SaysWhatTheModDoes()
        {
            Assert.Contains(Description, SteamDescription());
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
