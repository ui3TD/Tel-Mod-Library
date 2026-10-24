using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Xunit;
using static NationalTour.Prefectures;
using Country = SEvent_Tour._country;

namespace NationalTour.Tests
{
    /// <summary>
    /// The 18 prefectures are tour destinations of their own, with the numbers of the countries they stand in
    /// for, and stay out of the game's country list.
    /// </summary>
    public class PrefectureTests
    {
        public static IEnumerable<object[]> Countries() =>
            Enum.GetValues(typeof(Country)).Cast<Country>().Select(c => new object[] { c });

        public PrefectureTests()
        {
            TestGame.Reset();
            TestGame.LoadCountries();
        }

        private static SEvent_Tour.country PrefectureFor(Country country) => Get(TypeOf(ForCountry[country]));

        [Theory]
        [MemberData(nameof(Countries))]
        public void Prefecture_HasItsCountrysCostTicketPriceAndDiscountArea(Country country)
        {
            SEvent_Tour.country paired = TestGame.Country(country);
            SEvent_Tour.country prefecture = PrefectureFor(country);

            Assert.Equal(paired.Cost, prefecture.Cost);
            Assert.Equal(paired.TicketPrice, prefecture.TicketPrice);
            // The same area object, so the game's regional discount works within each region of Japan
            Assert.Same(paired.Area, prefecture.Area);
            Assert.Equal(1, prefecture.Level);
        }

        [Fact]
        public void Prefectures_StayOutOfTheGamesCountryList()
        {
            Assert.Equal(18, All.Count);
            Assert.Equal(18, SEvent_Tour.Countries.Count);
            Assert.All(SEvent_Tour.Countries, c => Assert.False(IsPrefecture(c.Type)));
            Assert.All(All, p => Assert.True(IsPrefecture(p.Type)));
        }

        /// <summary>
        /// The game loads its countries once per game scene; the prefectures start over with them.
        /// </summary>
        [Fact]
        public void LoadingTheCountriesAgain_BuildsThePrefecturesOnce()
        {
            PrefectureFor(Country.US).Level = 3;

            SEvent_Tour_LoadCountries.Postfix();

            Assert.Equal(18, All.Count);
            Assert.Equal(1, PrefectureFor(Country.US).Level);
        }

        /// <summary>
        /// A country another mod removed leaves its prefecture out instead of stopping the others.
        /// </summary>
        [Fact]
        public void CountryMissingFromTheGamesList_LeavesItsPrefectureOut()
        {
            SEvent_Tour.Countries.RemoveAll(c => c.Type == Country.canada);

            Build();

            Assert.Equal(17, All.Count);
            Assert.Null(Get(TypeOf(Prefecture.hokkaido)));
            Assert.True(Readiness.IsReady(Readiness.Module.Prefectures));
        }

        [Fact]
        public void NoGameCountries_TurnsThePrefecturesOff()
        {
            SEvent_Tour.Countries.Clear();
            using LogRecorder log = new();

            Build();

            Assert.Empty(All);
            Assert.False(Readiness.IsReady(Readiness.Module.Prefectures));
            Assert.Single(log.Errors);
        }

        // Lookups

        [Theory]
        [MemberData(nameof(Countries))]
        public void GetCountry_FindsThePrefectureByItsNumber(Country country)
        {
            SEvent_Tour.country result = null;
            SEvent_Tour_GetCountry.Postfix(TypeOf(ForCountry[country]), ref result);
            Assert.Same(PrefectureFor(country), result);
        }

        [Fact]
        public void GetCountry_KeepsAResultItAlreadyHas()
        {
            SEvent_Tour.country anotherMods = new() { Type = TypeOf(Prefecture.osaka) };
            SEvent_Tour.country result = anotherMods;

            SEvent_Tour_GetCountry.Postfix(TypeOf(Prefecture.osaka), ref result);

            Assert.Same(anotherMods, result);
        }

        [Fact]
        public void GetCountry_LeavesOtherNumbersAlone()
        {
            SEvent_Tour.country result = null;
            SEvent_Tour_GetCountry.Postfix((Country)999, ref result);
            Assert.Null(result);
        }

        [Fact]
        public void CountryTitle_IsThePrefectureName()
        {
            Language.Data["NATIONAL_TOUR__OSAKA"] = "Osaka (from the language file)";
            string title = "ERROR";

            SEvent_Tour_GetCountryTitle.Postfix(TypeOf(Prefecture.osaka), ref title);

            Assert.Equal("Osaka (from the language file)", title);
        }

        [Fact]
        public void CountryTitle_WithoutTheModsText_IsTheEnglishName()
        {
            string title = "ERROR";
            SEvent_Tour_GetCountryTitle.Postfix(TypeOf(Prefecture.hokkaido), ref title);
            Assert.Equal("Hokkaido", title);
        }

        [Fact]
        public void CountryTitle_KeepsAnotherModsName()
        {
            string title = "Another mod's place";
            SEvent_Tour_GetCountryTitle.Postfix(TypeOf(Prefecture.osaka), ref title);
            Assert.Equal("Another mod's place", title);
        }

        [Theory]
        [InlineData(Country.france, "France")]
        [InlineData((Country)999, "ERROR")]
        public void CountryTitle_LeavesOtherNumbersAlone(Country country, string gameTitle)
        {
            string title = gameTitle;
            SEvent_Tour_GetCountryTitle.Postfix(country, ref title);
            Assert.Equal(gameTitle, title);
        }

        // Stamina

        [Theory]
        [MemberData(nameof(Countries))]
        public void Stamina_OsakaCosts10_OthersCost20(Country country)
        {
            SEvent_Tour.country prefecture = PrefectureFor(country);
            int cost = GAME_STAMINA_COST;

            SEvent_Tour_country_GetStaminaCost.Postfix(prefecture, ref cost);

            Assert.Equal(country == Country.france ? 10 : 20, cost);
        }

        [Fact]
        public void Stamina_KeepsAnotherModsCostForOsaka()
        {
            int cost = 35;
            SEvent_Tour_country_GetStaminaCost.Postfix(Get(TypeOf(Prefecture.osaka)), ref cost);
            Assert.Equal(35, cost);
        }

        [Fact]
        public void Stamina_LeavesTheWorldTourAlone()
        {
            int cost = GAME_STAMINA_COST;
            SEvent_Tour_country_GetStaminaCost.Postfix(TestGame.Country(Country.china), ref cost);
            Assert.Equal(20, cost);
        }

        // Which tours are national

        [Fact]
        public void TourToPrefectures_IsNational()
        {
            Assert.True(IsNational(TestGame.Tour(PrefectureFor(Country.US), PrefectureFor(Country.france))));
        }

        [Fact]
        public void TourToCountries_IsNotNational()
        {
            Assert.False(IsNational(TestGame.Tour(TestGame.Country(Country.US))));
        }

        [Fact]
        public void TourWithoutStops_IsNotNational()
        {
            Assert.False(IsNational(TestGame.Tour()));
            Assert.False(IsNational(null));
        }

        // The pairing and the map

        [Fact]
        public void EveryCountry_HasItsOwnPrefecture()
        {
            Assert.Equal(Enum.GetValues(typeof(Country)).Cast<Country>().OrderBy(c => c), ForCountry.Keys.OrderBy(c => c));
            Assert.Equal(Enum.GetValues(typeof(Prefecture)).Cast<Prefecture>().OrderBy(p => p), ForCountry.Values.OrderBy(p => p));
        }

        [Fact]
        public void PrefectureNumbers_AreNotTheGames()
        {
            Assert.All(Enum.GetValues(typeof(Prefecture)).Cast<int>(), n => Assert.False(Enum.IsDefined(typeof(Country), n)));
        }

        [Fact]
        public void EveryPrefecture_HasItsOwnSpotOnTheMap()
        {
            Assert.Equal(Enum.GetValues(typeof(Prefecture)).Cast<Prefecture>().OrderBy(p => p), MapPosition.Keys.OrderBy(p => p));
            Assert.Equal(MapPosition.Count, MapPosition.Values.Distinct().Count());
        }

        /// <summary>
        /// The regions of Japan the game's discount areas stand for.
        /// </summary>
        public static readonly Dictionary<string, Prefecture[]> Regions = new()
        {
            ["Hokkaido + Tohoku"] = new[] { Prefecture.hokkaido, Prefecture.miyagi },
            ["Kanto + Hokuriku + Tokai"] = new[] { Prefecture.saitama, Prefecture.kanagawa, Prefecture.niigata, Prefecture.aichi, Prefecture.chiba, Prefecture.shizuoka, Prefecture.ishikawa },
            ["Kansai + Chugoku + Shikoku"] = new[] { Prefecture.ehime, Prefecture.osaka, Prefecture.okayama, Prefecture.hiroshima, Prefecture.hyogo, Prefecture.kyoto },
            ["Kyushu"] = new[] { Prefecture.fukuoka, Prefecture.kumamoto },
            ["Okinawa"] = new[] { Prefecture.okinawa },
        };

        [Theory]
        [InlineData("Hokkaido + Tohoku", SEvent_Tour._areaType.namerica)]
        [InlineData("Kanto + Hokuriku + Tokai", SEvent_Tour._areaType.asia)]
        [InlineData("Kansai + Chugoku + Shikoku", SEvent_Tour._areaType.europe)]
        [InlineData("Kyushu", SEvent_Tour._areaType.samerica)]
        [InlineData("Okinawa", SEvent_Tour._areaType.africa)]
        public void Regions_ShareADiscountArea(string region, SEvent_Tour._areaType area)
        {
            IEnumerable<Prefecture> inArea = All.Where(p => p.Area.Type == area).Select(p => (Prefecture)(int)p.Type);
            Assert.Equal(Regions[region].OrderBy(p => p), inArea.OrderBy(p => p));
        }

        /// <summary>
        /// Okinawa sits in an inset, as on most maps of Japan; the main islands' regions run west to east.
        /// </summary>
        [Fact]
        public void Pins_RunWestToEastByRegion()
        {
            string[] westToEast = { "Kyushu", "Kansai + Chugoku + Shikoku", "Kanto + Hokuriku + Tokai", "Hokkaido + Tohoku" };
            for (int i = 1; i < westToEast.Length; i++)
            {
                float westEdge = Regions[westToEast[i - 1]].Max(p => MapPosition[p].x);
                float eastEdge = Regions[westToEast[i]].Min(p => MapPosition[p].x);
                Assert.True(westEdge < eastEdge, $"{westToEast[i - 1]} reaches x={westEdge}, east of {westToEast[i]} at x={eastEdge}");
            }
        }

        [Theory]
        [InlineData(Prefecture.hokkaido, Prefecture.miyagi)]
        [InlineData(Prefecture.miyagi, Prefecture.saitama)]
        [InlineData(Prefecture.niigata, Prefecture.saitama)]
        [InlineData(Prefecture.saitama, Prefecture.kanagawa)]
        [InlineData(Prefecture.kyoto, Prefecture.osaka)]
        [InlineData(Prefecture.hiroshima, Prefecture.ehime)]
        [InlineData(Prefecture.fukuoka, Prefecture.kumamoto)]
        public void Pins_NorthOf(Prefecture north, Prefecture south)
        {
            Assert.True(MapPosition[north].y > MapPosition[south].y);
        }

        [Fact]
        public void Okinawa_IsInTheInset()
        {
            Vector3 okinawa = MapPosition[Prefecture.okinawa];
            Assert.All(MapPosition.Where(l => l.Key != Prefecture.okinawa), l => Assert.True(l.Value.x > okinawa.x));
        }
    }
}
