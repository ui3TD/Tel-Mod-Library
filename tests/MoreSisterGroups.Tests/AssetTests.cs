using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace MoreSisterGroups.Tests
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
            string csproj = File.ReadAllText(TestGame.ModFile("More Sister Groups.csproj"));
            Assert.Equal("Unlimited sister groups, but they're nerfed to maintain balance.",
                Regex.Match(csproj, "<ModDescription>(.+)</ModDescription>").Groups[1].Value);
        }

        [Theory]
        [InlineData("Unlimited sister groups, but they are nerfed to maintain balance.")]
        [InlineData("- Sister group minimum member limit for creation removed")]
        [InlineData("- When releasing a single, the penalty for a decrease in fame and appeal now only considers past singles of the same group")]
        [InlineData("- In Unfair, sister group new fans are reduced 5x")]
        [InlineData("- In Unfair, sister group new fans are reduced if the group has less than 10 members, by the square of its size. A group of 1 member gets 1% of the new fans, 2 members get 4%, 5 members get 25%, and 10 or more members get the full amount.")]
        [InlineData("- In Unfair, only the members who sing on a single count. If 5 of a group's 10 members sing, the single gets half the new fans.")]
        [InlineData("- In Unfair, each sister group has its own release cooldown. A single released less than 31 days after the same group's previous single gets fewer new fans, the same way the main group's sales drop. Other groups' singles don't count.")]
        public void SteamDescription_ListsEachChange(string text)
        {
            Assert.Contains(text, SteamDescription());
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
