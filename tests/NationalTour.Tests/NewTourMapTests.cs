using System;
using System.Linq;
using UnityEngine;
using Xunit;
using static NationalTour.Prefectures;
using Country = SEvent_Tour._country;

namespace NationalTour.Tests
{
    /// <summary>
    /// The new tour popup has two modes. Opened from the National Tour tab, it shows the map of Japan with one
    /// pin per prefecture; opened any other way, it is the game's world map with the game's pins.
    /// </summary>
    public class NewTourMapTests
    {
        private readonly Scene scene;

        public NewTourMapTests()
        {
            TestGame.Reset();
            TestGame.LoadImages();
            TestGame.LoadCountries();
            scene = new Scene();
        }

        private void OpenNational() => Seams.OpenNationalMap(scene.NewTour);

        // The game's own resets (the World Tour tab's Continue, the event list) call Reset, then the mod's postfix
        private void OpenWorld()
        {
            scene.NewTour.Tour = new SEvent_Tour.tour();
            Seams.NewTourReset(scene.NewTour);
        }

        /// <summary>
        /// Where a prefecture's pin goes in the popup's own units.
        /// </summary>
        private static Vector3 PopupPosition(Prefecture prefecture) => MapPosition[prefecture] * POPUP_UNITS_PER_MAP_UNIT;

        private static Prefecture PrefectureOf(Tour_Country pin) => (Prefecture)(int)pin.CountryType;

        // National mode

        [Fact]
        public void National_ShowsTheMapOfJapan()
        {
            OpenNational();

            Assert.True(NewTourMap.NationalMode);
            Assert.Same(Pictures.TOUR_map_2, scene.NewTourMap.sprite);
        }

        [Fact]
        public void National_HasOnePinPerPrefecture_CopiedFromTheGamesPins()
        {
            OpenNational();

            Tour_Country[] pins = scene.PrefecturePins();
            Assert.Equal(Enum.GetValues(typeof(Prefecture)).Cast<Prefecture>().OrderBy(p => p), pins.Select(PrefectureOf).OrderBy(p => p));
            Assert.All(pins, p => Assert.StartsWith(NewTourMap.PIN_NAME_PREFIX, Seams.NameOf(p)));
            Assert.All(pins, p => Assert.True(Seams.IsActive(p)));
        }

        [Fact]
        public void National_HidesTheGamesPins()
        {
            OpenNational();

            Assert.Equal(18, scene.GamePins().Length);
            Assert.All(scene.GamePins(), p => Assert.False(Seams.IsActive(p)));
        }

        [Fact]
        public void National_PinsArePlacedAndShowTheirPrefecture()
        {
            OpenNational();

            foreach (Tour_Country pin in scene.PrefecturePins())
            {
                Assert.Equal(PopupPosition(PrefectureOf(pin)), scene.PositionOf(pin));
                // UpdateData reads the prefecture's name, stars and costs (GetCountry finds it: PrefectureTests)
                Assert.Equal(1, Seams.CountCalls(nameof(Tour_Country.UpdateData), pin));
            }
        }

        [Fact]
        public void NationalAgain_KeepsThePins()
        {
            OpenNational();
            OpenNational();

            Assert.Equal(18, scene.PrefecturePins().Length);
            Assert.Equal(PopupPosition(Prefecture.hokkaido), scene.PositionOf(scene.PrefecturePins().Single(p => PrefectureOf(p) == Prefecture.hokkaido)));
        }

        /// <summary>
        /// The game resets the popup at the scale it's at: 1 the first time, 0.5 after its close animation, 0.3 at
        /// the start of its open animation. Each pin lands on the same spot once the popup is full size.
        /// </summary>
        [Theory]
        [InlineData(1f)]
        [InlineData(0.5f)]
        [InlineData(0.3f)]
        public void Pins_DontDependOnThePopupsScale(float scale)
        {
            Seams.Scales[Seams.TransformOf(scene.NewTour)] = scale;

            OpenNational();

            foreach (Tour_Country pin in scene.PrefecturePins())
            {
                Vector3 fullSize = scene.PositionOf(pin) / scale;
                Assert.True(Vector3.Distance(PopupPosition(PrefectureOf(pin)), fullSize) < 0.001f, pin.CountryType + " is at " + fullSize + " at full size");
            }
        }

        [Fact]
        public void National_EachResetIsANewEmptyTour()
        {
            OpenNational();
            SEvent_Tour.tour first = scene.NewTour.Tour;
            first.SelectedCountries.Add(new SEvent_Tour.tour.selectedCountry { Country = Get(TypeOf(Prefecture.osaka)), Level = 1 });

            OpenNational();

            Assert.NotSame(first, scene.NewTour.Tour);
            Assert.Empty(scene.NewTour.Tour.SelectedCountries);
        }

        /// <summary>
        /// A prefecture that couldn't be built (its country was removed by another mod) gets no pin.
        /// </summary>
        [Fact]
        public void PrefectureMissing_HasNoPin()
        {
            All.RemoveAll(p => p.Type == TypeOf(Prefecture.hokkaido));

            OpenNational();

            Assert.Equal(17, scene.PrefecturePins().Length);
        }

        // Continue

        [Fact]
        public void National_ContinueIsTheGames_WhenNationalToursCanBeCreated()
        {
            Seams.AddHandler(scene.Handler);
            OpenNational();

            Seams.Activated.Clear();
            Seams.NewTourRender(scene.NewTour);

            Assert.False(Seams.Activated.ContainsKey(scene.NewTourContinue));
        }

        /// <summary>
        /// Without a national handler (another mod already added a second one), a national tour would go in the
        /// World Tour's slot: Continue stays greyed instead.
        /// </summary>
        [Fact]
        public void National_ContinueStaysGreyed_WithoutANationalHandler()
        {
            OpenNational();

            Assert.False(Seams.Activated[scene.NewTourContinue]);
        }

        [Fact]
        public void National_ContinueStaysGreyed_WhenRoutingIsOff()
        {
            Seams.AddHandler(scene.Handler);
            using LogRecorder log = new();
            Readiness.Fail(Readiness.Module.Routing, "test");

            OpenNational();
            Seams.NewTourRender(scene.NewTour);

            Assert.False(Seams.Activated[scene.NewTourContinue]);
        }

        [Fact]
        public void World_ContinueIsTheGames()
        {
            OpenNational();
            OpenWorld();

            Seams.Activated.Clear();
            Seams.NewTourRender(scene.NewTour);

            Assert.False(Seams.Activated.ContainsKey(scene.NewTourContinue));
        }

        // World mode

        [Fact]
        public void World_FirstTime_IsTheGamesMapAndPins()
        {
            OpenWorld();

            Assert.False(NewTourMap.NationalMode);
            Assert.Same(scene.GameMap, scene.NewTourMap.sprite);
            Assert.Empty(scene.PrefecturePins());
            Assert.All(scene.GamePins(), p => Assert.Equal(Scene.VanillaPosition, scene.PositionOf(p)));
        }

        [Fact]
        public void World_AfterNational_PutsBackTheGamesMapAndPins()
        {
            OpenNational();
            Seams.Calls.Clear();

            OpenWorld();

            Assert.Same(scene.GameMap, scene.NewTourMap.sprite);
            Assert.All(scene.PrefecturePins(), p => Assert.False(Seams.IsActive(p)));
            foreach (Tour_Country pin in scene.GamePins())
            {
                Assert.True(Seams.IsActive(pin));
                Assert.Equal(Scene.VanillaPosition, scene.PositionOf(pin));
                // The game's reset skipped it while it was hidden
                Assert.Equal(1, Seams.CountCalls(nameof(Tour_Country.UpdateData), pin));
            }
        }

        /// <summary>
        /// Another mod's change to the world map stays: world mode only puts back what national mode changed.
        /// </summary>
        [Fact]
        public void World_KeepsAnotherModsMapAndHiddenPins()
        {
            Sprite anotherMods = Seams.Fake<Sprite>();
            Seams.SetSprite(scene.NewTourMap, anotherMods);
            Seams.SetActive(scene.Countries[Country.canada], false);

            OpenWorld();
            OpenNational();
            OpenWorld();

            Assert.Same(anotherMods, scene.NewTourMap.sprite);
            Assert.False(Seams.IsActive(scene.Countries[Country.canada]));
            Assert.True(Seams.IsActive(scene.Countries[Country.US]));
        }

        [Fact]
        public void NationalWithoutAMap_FailsSoft()
        {
            Seams.Rename(Seams.GameObjectOf(scene.NewTourMap), "Something else");
            using LogRecorder log = new();

            OpenNational();

            Assert.False(NewTourMap.NationalMode);
            Assert.False(Readiness.IsReady(Readiness.Module.NewTourMap));
            Assert.Single(log.Errors);
            Assert.All(scene.GamePins(), p => Assert.True(Seams.IsActive(p)));
        }
    }
}
