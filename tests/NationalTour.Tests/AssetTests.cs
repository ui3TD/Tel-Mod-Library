using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static NationalTour.Utility;
using Country = SEvent_Tour._country;

namespace NationalTour.Tests
{
    /// <summary>
    /// The JSON, pictures and Steam text the mod ships say National Tour wherever the game said World Tour,
    /// and keep the World Tour's numbers.
    /// </summary>
    public class AssetTests
    {
        /// <summary>
        /// Wording that only makes sense for a tour abroad.
        /// </summary>
        private static readonly Regex Abroad = new(@"world tour|world concert|international|overseas|abroad|globetrotter|jet.?lag|world-traveler|language barrier|France|Italy|America|Eiffel", RegexOptions.IgnoreCase);

        private static JSONNode Json(string relativePath) => JSON.Parse(File.ReadAllText(TestGame.ModAsset(relativePath)));

        private static Dictionary<string, string> Labels() =>
            Children(Json(Path.Combine("JSON", "Constants", "constants.json")).AsArray).ToDictionary(n => n["id"].Value, n => n["text"].Value);

        private static string SteamDescription() => File.ReadAllText(TestGame.ModAsset("Steam Description.txt"));

        private static IEnumerable<JSONNode> Children(JSONNode node) => Enumerable.Range(0, node.Count).Select(i => node[i]);

        private static List<string> Texts(JSONNode node)
        {
            List<string> texts = new();
            if (node is JSONArray || node is JSONClass)
            {
                foreach (JSONNode child in Children(node))
                    texts.AddRange(Texts(child));
            }
            else if (node != null)
                texts.Add(node.Value);
            return texts;
        }

        // countries.json

        /// <summary>
        /// The vanilla World Tour's costs and ticket prices, which the Steam description says are kept.
        /// </summary>
        [Theory]
        [InlineData(Country.china, "asia", 45300, 2245)]
        [InlineData(Country.southKorea, "asia", 15100, 4339)]
        [InlineData(Country.india, "asia", 75500, 1286)]
        [InlineData(Country.thailand, "asia", 52850, 1925)]
        [InlineData(Country.philippines, "asia", 46810, 1468)]
        [InlineData(Country.indonesia, "asia", 52850, 1582)]
        [InlineData(Country.australia, "asia", 105700, 4817)]
        [InlineData(Country.southAfrica, "africa", 135900, 1828)]
        [InlineData(Country.canada, "namerica", 196300, 5734)]
        [InlineData(Country.US, "namerica", 151000, 7271)]
        [InlineData(Country.brazil, "samerica", 151000, 2510)]
        [InlineData(Country.argentina, "samerica", 196300, 2903)]
        [InlineData(Country.UK, "europe", 135900, 5174)]
        [InlineData(Country.france, "europe", 90600, 5123)]
        [InlineData(Country.germany, "europe", 120800, 5500)]
        [InlineData(Country.italy, "europe", 120800, 4400)]
        [InlineData(Country.spain, "europe", 75500, 4094)]
        [InlineData(Country.russia, "europe", 108720, 2577)]
        public void Countries_KeepTheWorldToursCostsAndDiscountAreas(Country type, string area, int cost, int ticketPrice)
        {
            SEvent_Tour.country country = LoadCountries().Single(c => c.Type == type);
            Assert.Equal(area, country.Area.Type.ToString());
            Assert.Equal(cost, country.Cost);
            Assert.Equal(ticketPrice, country.TicketPrice);
            Assert.Equal(1, country.Level);
        }

        [Fact]
        public void Countries_ListEachCountryOnce()
        {
            Assert.Equal(Enum.GetValues(typeof(Country)).Cast<Country>().OrderBy(c => c), LoadCountries().Select(c => c.Type).OrderBy(c => c));
        }

        /// <summary>
        /// Each discount area holds one of the regions the Steam description lists.
        /// </summary>
        [Theory]
        [InlineData("Hokkaido + Tohoku", "namerica")]
        [InlineData("Kanto + Hokuriku + Tokai", "asia")]
        [InlineData("Kansai + Chugoku + Shikoku", "europe")]
        [InlineData("Kyushu", "samerica")]
        [InlineData("Okinawa", "africa")]
        public void Regions_ShareADiscountArea(string region, string area)
        {
            Assert.Contains("- " + region, SteamDescription());
            IEnumerable<Prefectures> inArea = LoadCountries().Where(c => c.Area.Type.ToString() == area).Select(c => tourLocations[c.Type]);
            Assert.Equal(MapTests.Regions[region].OrderBy(p => p), inArea.OrderBy(p => p));
        }

        /// <summary>
        /// Loads the mod's countries.json with the game's own parser.
        /// </summary>
        private static List<SEvent_Tour.country> LoadCountries()
        {
            SEvent_Tour.Const_Area.Clear();
            foreach (SEvent_Tour._areaType type in Enum.GetValues(typeof(SEvent_Tour._areaType)))
                SEvent_Tour.Const_Area.Add(new SEvent_Tour._area { Type = type });

            List<SEvent_Tour.country> countries = new();
            foreach (JSONNode node in Json(Path.Combine("JSON", "SpecialEvents", "WorldTour", "countries.json")).AsArray)
            {
                SEvent_Tour.country country = new();
                country.Set(node);
                countries.Add(country);
            }
            return countries;
        }

        // constants.json

        [Theory]
        [MemberData(nameof(MapTests.Countries), MemberType = typeof(MapTests))]
        public void CountryNames_ArePrefectureNames(Country country)
        {
            TestGame.Reset();
            foreach (KeyValuePair<string, string> label in Labels())
                Language.Data[label.Key] = label.Value;

            string prefecture = tourLocations[country].ToString();
            Assert.Equal(char.ToUpper(prefecture[0]) + prefecture.Substring(1), SEvent_Tour.GetCountryTitle(country));
        }

        /// <summary>
        /// Aya's chapter 3 task is done by touring the US, France and Italy (tasks.OnWorldTour).
        /// </summary>
        [Fact]
        public void AyaTask_NamesThePrefecturesItChecks()
        {
            Assert.Equal("Go on a national tour and visit Miyagi, Osaka and Hiroshima", Labels()["TASK__CH3_AYA_2"]);
            Assert.Equal(Prefectures.miyagi, tourLocations[Country.US]);
            Assert.Equal(Prefectures.osaka, tourLocations[Country.france]);
            Assert.Equal(Prefectures.hiroshima, tourLocations[Country.italy]);
        }

        /// <summary>
        /// Every vanilla English label that mentions the World Tour or its countries.
        /// </summary>
        [Fact]
        public void Labels_ReplaceEveryWorldTourLabel()
        {
            string[] countries = { "RUSSIA", "CHINA", "SOUTH_KOREA", "INDIA", "THAILAND", "PHILIPPINES", "INDONESIA", "AUSTRALIA", "SOUTH_AFRICA",
                "CANADA", "USA", "BRAZIL", "ARGENTINA", "UK", "FRANCE", "GERMANY", "ITALY", "SPAIN" };
            string[] tour = { "ACTIVITIES__WORLDTOUR", "ACTIVITIES__TOUREVERY", "SEVENT__WORLD_TOUR", "SEVENT__WORLD_TOUR_DESC", "TOUR__COUNTRIES",
                "AWARDS__MOST_PROLIFIC_GROUP_NOM_REQ", "AWARDS__MOST_PROLIFIC_GROUP_NOM_REWARD", "TASK__CH3_AYA_2" };
            Assert.Equal(countries.Concat(tour).OrderBy(id => id), Labels().Keys.OrderBy(id => id));
        }

        [Fact]
        public void Labels_SayNationalTour()
        {
            Dictionary<string, string> labels = Labels();
            Assert.Equal("National Tour", labels["SEVENT__WORLD_TOUR"]);
            Assert.Equal("Prefectures", labels["TOUR__COUNTRIES"]);
            Assert.All(labels.Values, text => Assert.DoesNotMatch(Abroad, text));
            Assert.All(labels.Values, text => Assert.DoesNotMatch(new Regex(@"\bcountr", RegexOptions.IgnoreCase), text));
        }

        // dialogues.json and random_events.json

        [Fact]
        public void Dialogues_LoadInTheGame()
        {
            List<data_dialogues._dialogue> loaded = new();
            AccessTools.Method(typeof(data_dialogues), "LoadFilePath").Invoke(null, new object[] { TestGame.ModAsset(Path.Combine("JSON", "Events", "dialogues.json")), loaded });
            Assert.Equal(new[] { "story_chapter_3_aya_favors", "story_chapter_5_45_aya", "world_traveler", "date_fun_50_walk", "story_chapter_3_aya_favors_10" }, loaded.Select(d => d.id));
        }

        [Fact]
        public void Dialogues_DontTalkAboutGoingAbroad()
        {
            JSONArray dialogues = Json(Path.Combine("JSON", "Events", "dialogues.json")).AsArray;
            Assert.All(Children(dialogues), d => Assert.All(Texts(d["script"]), text => Assert.DoesNotMatch(Abroad, text)));
        }

        /// <summary>
        /// Aya hopes to visit the places her tour task then asks for: Osaka, Matsushima (Miyagi) and Miyajima (Hiroshima).
        /// </summary>
        [Fact]
        public void Aya_WantsToVisitTheTasksPrefectures()
        {
            JSONNode favors = Children(Json(Path.Combine("JSON", "Events", "dialogues.json")).AsArray).Single(d => d["id"].Value == "story_chapter_3_aya_favors");
            string text = string.Join(" ", Texts(favors["script"]));
            Assert.Contains("I've always wanted to visit Osaka", text);
            Assert.Contains("and Matsushima", text);
            Assert.Contains("and Miyajima...", text);
        }

        /// <summary>
        /// The game's "has gone on a world tour" check still picks the line, so it fits a national tour too.
        /// </summary>
        [Fact]
        public void Walk_StillChecksForATour()
        {
            JSONNode walk = Children(Json(Path.Combine("JSON", "Events", "dialogues.json")).AsArray).Single(d => d["id"].Value == "date_fun_50_walk");
            Assert.Contains("\"parameter\":\"world_tour\"", walk.ToString().Replace(" ", ""));
        }

        /// <summary>
        /// Japan has one time zone, so the flight home from a national tour leaves Aya tired, not jet-lagged.
        /// </summary>
        [Fact]
        public void AyaFlightHome_IsExhaustingNotJetLagged()
        {
            JSONNode flight = Children(Json(Path.Combine("JSON", "Events", "dialogues.json")).AsArray).Single(d => d["id"].Value == "story_chapter_3_aya_favors_10");
            Assert.Contains("so that I don't feel too exhausted when we touch down.", string.Join(" ", Texts(flight["script"])));
        }

        [Fact]
        public void AyaTourEvent_SaysNationalTour()
        {
            JSONArray events = Json(Path.Combine("JSON", "Events", "random_events.json")).AsArray;
            JSONNode tour = Assert.Single(Children(events));
            Assert.Equal("story_chapter_3_aya_tour", tour["id"].Value);
            Assert.Contains("[groupname] national tour", tour["description"].Value);
            Assert.All(Texts(tour), text => Assert.DoesNotMatch(Abroad, text));
        }

        // Pictures

        [Theory]
        [InlineData(TOUR_MAP_FILE)]
        [InlineData(TOUR_POPUP_BG_FILE)]
        [InlineData(TOUR_POPUP_BG_BLURRED_FILE)]
        public void Pictures_AreJpegs(string file)
        {
            byte[] bytes = File.ReadAllBytes(TestGame.ModAsset(Path.Combine("Textures", MOD_TEXTURE_DIR, file)));
            Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, bytes.Take(3));
        }

        // Steam and info.json

        [Fact]
        public void InfoJson_DescribesTheModAndNamesTheTitleTheModLooksFor()
        {
            string csproj = File.ReadAllText(TestGame.ModFile("National Tour.csproj"));
            Assert.Equal("Replaces World Tours with National Tours.", Regex.Match(csproj, "<ModDescription>(.+)</ModDescription>").Groups[1].Value);
            Assert.Equal(MOD_TITLE, Regex.Match(csproj, "<ModName>(.+)</ModName>").Groups[1].Value);
        }

        [Theory]
        [InlineData("World Tours replaced by National Tours.")]
        [InlineData("Also replaces event and story dialogue to make sense with a National Tour.")]
        [InlineData("I wanted to preserve the original costs and regional discounts.")]
        public void SteamDescription_SaysWhatTheModDoes(string text)
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
