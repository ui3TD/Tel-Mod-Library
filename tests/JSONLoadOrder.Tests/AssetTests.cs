using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace JSONLoadOrder.Tests
{
    /// <summary>
    /// The in-game, Steam and guide text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private const string Rules = "Default JSONLoadOrder number is 0. Lower numbers load early, and higher numbers load late.";

        private static string Csproj() => File.ReadAllText(TestGame.ModFile("JSON Load Order.csproj"));

        private static string CsprojProperty(string name) =>
            Regex.Match(Csproj(), $"<{name}>(.+)</{name}>").Groups[1].Value;

        private static string SteamDescription() => File.ReadAllText(TestGame.ModAsset("steam description.txt"));

        private static string GuideExample() =>
            Regex.Match(TestGame.Guide(), "```json\\s*(\\{.*?\\})\\s*```", RegexOptions.Singleline).Groups[1].Value;

        [Fact]
        public void InfoJsonDescription_StatesTheRules()
        {
            Assert.Contains("add a JSONLoadOrder attribute to info.json. " + Rules, CsprojProperty("ModDescription"));
        }

        [Fact]
        public void SteamDescription_StatesTheRules()
        {
            Assert.Contains("add a JSONLoadOrder attribute to info.json. The d" + Rules.Substring(1), SteamDescription());
        }

        [Fact]
        public void Guide_StatesTheRules()
        {
            string guide = TestGame.Guide();
            Assert.Contains("The default is `0`.", guide);
            Assert.Contains("Lower numbers load early, and higher numbers load late.", guide);
        }

        /// <summary>
        /// The guide's example info.json loads late with the mod, and its HarmonyID is the mod's real one.
        /// </summary>
        [Fact]
        public void GuideExample_LoadsLate()
        {
            TestGame.Reset();
            TestGame.ModWithInfo("Example", GuideExample());
            TestGame.Mod("Other");

            TestGame.LoadLanguage();

            Assert.Equal(100, ModLoadOrder.modOrders["Example"]);
            Assert.Equal(new[] { "Other", "Example" }, TestGame.LoadedNames());
            Assert.Contains($"\"HarmonyID\": \"{CsprojProperty("HarmonyID")}\"", GuideExample());
        }

        /// <summary>
        /// The mod's own info.json uses the order the guide's example shows.
        /// </summary>
        [Fact]
        public void ModsOwnOrder_MatchesTheGuide()
        {
            Assert.Equal("100", CsprojProperty("JSONLoadOrder"));
            Assert.Contains("\"JSONLoadOrder\": 100", GuideExample());
        }

        [Fact]
        public void SteamDescription_LinksTheRequirementGuideAndSource()
        {
            string description = SteamDescription();
            Assert.Contains("[h1]REQUIRES: IM-HarmonyIntegration[/h1]", description);
            Assert.Contains("[url=https://github.com/ui3TD/IM-HarmonyIntegration]", description);
            Assert.Contains("[url=https://github.com/ui3TD/Tel-Mod-Library/blob/main/docs/JSONLoadOrder.md]", description);
            Assert.Contains("[url=https://github.com/ui3TD/Tel-Mod-Library]source code[/url]", description);
        }
    }
}
