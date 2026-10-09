using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Xunit;
using static NationalTour.Utility;
using Country = SEvent_Tour._country;

namespace NationalTour.Tests
{
    /// <summary>
    /// The new tour popup shows a map of Japan, with each of the game's countries pinned on the prefecture
    /// that replaces it.
    /// </summary>
    public class MapTests
    {
        public static IEnumerable<object[]> Countries() =>
            Enum.GetValues(typeof(Country)).Cast<Country>().Select(c => new object[] { c });

        public MapTests()
        {
            TestGame.Reset();
            TestGame.LoadImages();
        }

        [Fact]
        public void NewTour_ShowsTheMapOfJapan()
        {
            Scene scene = new();
            Seams.NewTourReset(scene.NewTour);
            Assert.Same(TOUR_map_2, scene.NewTourMap.sprite);
        }

        [Theory]
        [MemberData(nameof(Countries))]
        public void NewTour_PinsEachCountryOnItsPrefecture(Country country)
        {
            Scene scene = new();
            Seams.NewTourReset(scene.NewTour);
            Assert.Equal(locationDict[tourLocations[country]], scene.PositionOf(country));
        }

        [Fact]
        public void NewTour_OpenedAgain_KeepsThePins()
        {
            Scene scene = new();
            Seams.NewTourReset(scene.NewTour);
            Seams.NewTourReset(scene.NewTour);
            Assert.Equal(locationDict[Prefectures.hokkaido], scene.PositionOf(Country.canada));
        }

        [Fact]
        public void EveryCountry_HasItsOwnPrefecture()
        {
            Assert.Equal(Enum.GetValues(typeof(Country)).Cast<Country>().OrderBy(c => c), tourLocations.Keys.OrderBy(c => c));
            Assert.Equal(Enum.GetValues(typeof(Prefectures)).Cast<Prefectures>().OrderBy(p => p), tourLocations.Values.OrderBy(p => p));
        }

        [Fact]
        public void EveryPrefecture_HasItsOwnSpotOnTheMap()
        {
            Assert.Equal(Enum.GetValues(typeof(Prefectures)).Cast<Prefectures>().OrderBy(p => p), locationDict.Keys.OrderBy(p => p));
            Assert.Equal(locationDict.Count, locationDict.Values.Distinct().Count());
        }

        /// <summary>
        /// The Steam description's regions, which keep the World Tour's regional discounts.
        /// </summary>
        public static readonly Dictionary<string, Prefectures[]> Regions = new()
        {
            ["Hokkaido + Tohoku"] = new[] { Prefectures.hokkaido, Prefectures.miyagi },
            ["Kanto + Hokuriku + Tokai"] = new[] { Prefectures.saitama, Prefectures.kanagawa, Prefectures.niigata, Prefectures.aichi, Prefectures.chiba, Prefectures.shizuoka, Prefectures.ishikawa },
            ["Kansai + Chugoku + Shikoku"] = new[] { Prefectures.ehime, Prefectures.osaka, Prefectures.okayama, Prefectures.hiroshima, Prefectures.hyogo, Prefectures.kyoto },
            ["Kyushu"] = new[] { Prefectures.fukuoka, Prefectures.kumamoto },
            ["Okinawa"] = new[] { Prefectures.okinawa },
        };

        /// <summary>
        /// Okinawa sits in an inset, as on most maps of Japan; the main islands' regions run west to east.
        /// </summary>
        [Fact]
        public void Pins_RunWestToEastByRegion()
        {
            string[] westToEast = { "Kyushu", "Kansai + Chugoku + Shikoku", "Kanto + Hokuriku + Tokai", "Hokkaido + Tohoku" };
            for (int i = 1; i < westToEast.Length; i++)
            {
                float westEdge = Regions[westToEast[i - 1]].Max(p => locationDict[p].x);
                float eastEdge = Regions[westToEast[i]].Min(p => locationDict[p].x);
                Assert.True(westEdge < eastEdge, $"{westToEast[i - 1]} reaches x={westEdge}, east of {westToEast[i]} at x={eastEdge}");
            }
        }

        [Theory]
        [InlineData(Prefectures.hokkaido, Prefectures.miyagi)]
        [InlineData(Prefectures.miyagi, Prefectures.saitama)]
        [InlineData(Prefectures.niigata, Prefectures.saitama)]
        [InlineData(Prefectures.saitama, Prefectures.kanagawa)]
        [InlineData(Prefectures.kyoto, Prefectures.osaka)]
        [InlineData(Prefectures.hiroshima, Prefectures.ehime)]
        [InlineData(Prefectures.fukuoka, Prefectures.kumamoto)]
        public void Pins_NorthOf(Prefectures north, Prefectures south)
        {
            Assert.True(locationDict[north].y > locationDict[south].y);
        }

        [Fact]
        public void Okinawa_IsInTheInset()
        {
            Vector3 okinawa = locationDict[Prefectures.okinawa];
            Assert.All(locationDict.Where(l => l.Key != Prefectures.okinawa), l => Assert.True(l.Value.x > okinawa.x));
        }
    }
}
