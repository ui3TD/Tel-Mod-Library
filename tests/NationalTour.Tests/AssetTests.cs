using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static NationalTour.Pictures;

namespace NationalTour.Tests
{
    /// <summary>
    /// The text, pictures and files the mod ships. It adds its own text and leaves the World Tour's to the game,
    /// so the story and the World Tour read as in the base game.
    /// </summary>
    public class AssetTests
    {
        private static readonly string[] AwardKeys = { "AWARDS__MOST_PROLIFIC_GROUP_NOM_REQ", "AWARDS__MOST_PROLIFIC_GROUP_NOM_REWARD" };

        private static Dictionary<string, string> Labels()
        {
            JSONNode json = JSON.Parse(File.ReadAllText(TestGame.ModAsset(Path.Combine("JSON", "Constants", "constants.json"))));
            return Enumerable.Range(0, json.Count).Select(i => json[i]).ToDictionary(n => n["id"].Value, n => n["text"].Value);
        }

        [Fact]
        public void Labels_AreTheModsOwnPlusTheTwoAwardTexts()
        {
            IEnumerable<string> expected = new[] { NationalTab.TITLE_KEY, NationalTab.DESC_KEY, "NATIONAL_TOUR__PREFECTURES" }
                .Concat(Enum.GetValues(typeof(Prefectures.Prefecture)).Cast<Prefectures.Prefecture>().Select(Prefectures.LanguageKey))
                .Concat(AwardKeys);
            Assert.Equal(expected.OrderBy(k => k), Labels().Keys.OrderBy(k => k));
        }

        /// <summary>
        /// Most Prolific Group counts national tours too, so its texts say "tours" where the game says "world tours".
        /// </summary>
        [Fact]
        public void AwardTexts_SayTours()
        {
            Dictionary<string, string> labels = Labels();
            Assert.Equal("Total sum of released singles, concerts, tours and elections is at least @", labels[AwardKeys[0]]);
            Assert.Equal("Production costs of singles, concerts, elections and tours are @ lower", labels[AwardKeys[1]]);
        }

        [Theory]
        [MemberData(nameof(PrefectureTests.Countries), MemberType = typeof(PrefectureTests))]
        public void PrefectureNames_AreInTheLanguageFile(SEvent_Tour._country country)
        {
            Prefectures.Prefecture prefecture = Prefectures.ForCountry[country];
            string name = prefecture.ToString();
            Assert.Equal(char.ToUpper(name[0]) + name.Substring(1), Labels()[Prefectures.LanguageKey(prefecture)]);
        }

        [Fact]
        public void NationalTourText()
        {
            Dictionary<string, string> labels = Labels();
            Assert.Equal("National Tour", labels[NationalTab.TITLE_KEY]);
            Assert.Equal("Prefectures", labels["NATIONAL_TOUR__PREFECTURES"]);
            Assert.StartsWith(NationalTab.DESC_FALLBACK, labels[NationalTab.DESC_KEY]);
            Assert.DoesNotMatch(new Regex(@"\bcountr|world", RegexOptions.IgnoreCase), labels[NationalTab.DESC_KEY]);
        }

        /// <summary>
        /// The story follows the world tour, as in the base game: the mod no longer rewords its scenes or events.
        /// The game reads only the last countries.json loaded, so a copy would put the game's tour costs back
        /// over another mod's.
        /// </summary>
        [Fact]
        public void ShipsNoStoryEventsOrCountries()
        {
            Assert.False(Directory.Exists(TestGame.ModAsset(Path.Combine("JSON", "Events"))));
            Assert.False(Directory.Exists(TestGame.ModAsset(Path.Combine("JSON", "SpecialEvents"))));
        }

        [Theory]
        [InlineData(TOUR_MAP_FILE)]
        [InlineData(TOUR_POPUP_BG_FILE)]
        [InlineData(TOUR_POPUP_BG_BLURRED_FILE)]
        public void Pictures_AreJpegs(string file)
        {
            byte[] bytes = File.ReadAllBytes(TestGame.ModAsset(Path.Combine("Textures", MOD_TEXTURE_DIR, file)));
            Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, bytes.Take(3));
        }

        [Fact]
        public void InfoJson_NamesTheTitleTheModLooksFor()
        {
            string csproj = File.ReadAllText(TestGame.ModFile("National Tour.csproj"));
            Assert.Equal(MOD_TITLE, Regex.Match(csproj, "<ModName>(.+)</ModName>").Groups[1].Value);
        }

        /// <summary>
        /// Version 3 of the same mod: same Workshop item, same Harmony ID (which also names the saved variable).
        /// </summary>
        [Fact]
        public void InfoJson_IsVersion3OfTheSameMod()
        {
            string csproj = File.ReadAllText(TestGame.ModFile("National Tour.csproj"));
            Assert.Equal("3.0.0", Regex.Match(csproj, "<Version>(.+)</Version>").Groups[1].Value);
            Assert.Equal("Adds National Tours alongside World Tours.", Regex.Match(csproj, "<ModDescription>(.+)</ModDescription>").Groups[1].Value);
            Assert.Equal("2872425710", Regex.Match(csproj, "<WorkshopID>(.+)</WorkshopID>").Groups[1].Value);
            Assert.Equal(SaveState.VARIABLE, Regex.Match(csproj, "<HarmonyID>(.+)</HarmonyID>").Groups[1].Value);
        }

        private static string SteamDescription() => File.ReadAllText(TestGame.ModAsset("Steam Description.txt"));

        [Theory]
        [InlineData("Adds National Tours alongside World Tours.")]
        [InlineData("A national tour and a world tour can be in production at the same time. Each has its own 3-month cooldown.")]
        [InlineData("Osaka costs 10 stamina, like France. The others cost 20.")]
        [InlineData("National tours don't start story scenes, Aya's task or the World Tour achievement.")]
        [InlineData("Updating from version 2: the tours already in your save count as world tours, and every prefecture starts at the smallest venue.")]
        [InlineData("If a world tour and a national tour are both in production, finish or cancel one of them before you remove the mod.")]
        public void SteamDescription_SaysWhatTheModDoes(string text)
        {
            Assert.Contains(text, SteamDescription());
        }

        [Fact]
        public void SteamDescription_NoLongerSaysItReplacesTheWorldTour()
        {
            Assert.DoesNotContain("replaced by", SteamDescription());
            Assert.DoesNotContain("replaces event and story dialogue", SteamDescription());
        }

        [Fact]
        public void Changelog_StartsWithThisVersion()
        {
            string description = SteamDescription();
            string firstEntry = description.Substring(description.IndexOf("[h3]Changelog[/h3]")).Split('\n')[1].Trim();
            Assert.Equal("- 3.0.0: National tours run alongside world tours. Past tours count as world tours.", firstEntry);
        }

        /// <summary>
        /// The regions the Steam description lists are the game's discount areas.
        /// </summary>
        [Theory]
        [InlineData("Hokkaido + Tohoku")]
        [InlineData("Kanto + Hokuriku + Tokai")]
        [InlineData("Kansai + Chugoku + Shikoku")]
        [InlineData("Kyushu")]
        [InlineData("Okinawa")]
        public void SteamDescription_ListsEachRegion(string region)
        {
            Assert.Contains("- " + region, SteamDescription());
            Assert.True(PrefectureTests.Regions.ContainsKey(region));
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
