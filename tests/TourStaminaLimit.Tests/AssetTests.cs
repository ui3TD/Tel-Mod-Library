using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static TourStamina.TourStamina;

namespace TourStaminaLimit.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        private static string Coeff => TOUR_FAN_COEFF.ToString(CultureInfo.InvariantCulture);

        private static string SteamDescription() => File.ReadAllText(TestGame.ModAsset("steam description.txt"));

        [Fact]
        public void Constants_WarningGivesTheCap()
        {
            Assert.Equal($"Stamina cannot exceed {TOUR_STAM_CAP}pt", TestGame.Constants()[TOUR_STAM_TOOLTIP_ID]);
        }

        [Fact]
        public void Constants_HaveNoOtherEntries()
        {
            Assert.Equal(new[] { TOUR_STAM_TOOLTIP_ID }, TestGame.Constants().Keys.ToArray());
        }

        [Fact]
        public void InfoJsonDescription_GivesTheCapAndFanMultiplier()
        {
            string csproj = File.ReadAllText(TestGame.ModFile("Tour Stamina Limit.csproj"));
            Assert.Equal($"World Tour has a {TOUR_STAM_CAP} stamina limit. Fans increased {Coeff}x to compensate.",
                Regex.Match(csproj, "<ModDescription>(.+)</ModDescription>").Groups[1].Value);
        }

        [Fact]
        public void SteamDescription_GivesTheCapAndFanMultiplier()
        {
            string description = SteamDescription();
            Assert.Contains($"- World tours are limited to {TOUR_STAM_CAP} stamina so you can't go to all the countries at once.", description);
            Assert.Contains($"- World tours give {Coeff}x more fans to compensate for stamina limitation.", description);
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
