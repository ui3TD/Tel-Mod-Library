using System.Linq;
using TourStamina;
using UnityEngine;
using Xunit;
using static TourStamina.TourStamina;
using _country = SEvent_Tour._country;

namespace TourStaminaLimit.Tests
{
    /// <summary>
    /// "World tours are limited to 100 stamina so you can't go to all the countries at once",
    /// through the game's own country picker.
    /// </summary>
    public class StaminaCapTests
    {
        public StaminaCapTests() => TestGame.Reset(patched: true);

        private static _country[] Picked(SEvent_Tour.tour tour) => tour.SelectedCountries.Select(c => c.Country.Type).ToArray();

        [Fact]
        public void CapIs100()
        {
            Assert.Equal(100, TOUR_STAM_CAP);
        }

        /// <summary>
        /// Five 20-stamina countries reach the cap exactly, which is allowed.
        /// </summary>
        [Fact]
        public void CountriesUpToTheCap_CanBePicked()
        {
            SEvent_Tour.tour tour = new();
            SEvent_Tour.country[] countries = TestGame.Countries(5);

            foreach (SEvent_Tour.country country in countries)
                tour.SelectCountry(country, 1);

            Assert.Equal(countries.Select(c => c.Type), Picked(tour));
            Assert.Equal(100, tour.Stamina);
        }

        [Fact]
        public void CountryOverTheCap_IsNotPicked()
        {
            SEvent_Tour.country[] countries = TestGame.Countries(6);
            SEvent_Tour.tour tour = new();
            foreach (SEvent_Tour.country country in countries.Take(5))
                tour.SelectCountry(country, 1);

            tour.SelectCountry(countries[5], 3);

            Assert.Equal(countries.Take(5).Select(c => c.Type), Picked(tour));
            Assert.Equal(100, tour.Stamina);
        }

        /// <summary>
        /// France costs 10 stamina, so it fits where a 20-stamina country doesn't, and only while it fits.
        /// </summary>
        [Fact]
        public void France_CountsItsOwnStaminaCost()
        {
            SEvent_Tour.tour tour = TestGame.Tour(TestGame.Countries(4));
            SEvent_Tour.country france = TestGame.Country(_country.france);
            SEvent_Tour.country fifth = TestGame.Countries(5)[4];

            tour.SelectCountry(france, 1);
            Assert.Equal(90, tour.Stamina);

            tour.SelectCountry(fifth, 1);
            Assert.Equal(90, tour.Stamina);
            Assert.DoesNotContain(fifth.Type, Picked(tour));
        }

        /// <summary>
        /// At the cap, the player can still change a picked country's level: stamina doesn't depend on it.
        /// </summary>
        [Fact]
        public void AtTheCap_PickedCountryCanChangeLevel()
        {
            SEvent_Tour.country[] countries = TestGame.Countries(5);
            SEvent_Tour.tour tour = TestGame.Tour(countries);

            tour.SelectCountry(countries[2], 4);

            Assert.Equal(4, tour.GetCountry(countries[2]).Level);
            Assert.Equal(100, tour.Stamina);
        }

        /// <summary>
        /// Clicking a picked country's level again removes it, which frees its stamina for another country.
        /// </summary>
        [Fact]
        public void AtTheCap_PickedCountryCanBeRemoved_AndReplaced()
        {
            SEvent_Tour.country[] countries = TestGame.Countries(6);
            SEvent_Tour.tour tour = TestGame.Tour(countries.Take(5).ToArray());

            tour.SelectCountry(countries[0], 1);
            Assert.Equal(80, tour.Stamina);

            tour.SelectCountry(countries[5], 2);
            Assert.Contains(countries[5].Type, Picked(tour));
            Assert.Equal(100, tour.Stamina);
        }

        /// <summary>
        /// A tour planned before the mod was installed can be over the cap: its countries can still be
        /// changed or removed, but no more added.
        /// </summary>
        [Fact]
        public void TourAlreadyOverTheCap_CanOnlyShrink()
        {
            SEvent_Tour.country[] countries = TestGame.Countries(7);
            SEvent_Tour.tour tour = TestGame.Tour(countries.Take(6).ToArray());
            Assert.Equal(120, tour.Stamina);

            tour.SelectCountry(countries[6], 1);
            Assert.DoesNotContain(countries[6].Type, Picked(tour));

            tour.SelectCountry(countries[1], 5);
            Assert.Equal(5, tour.GetCountry(countries[1]).Level);

            tour.SelectCountry(countries[0], 1);
            Assert.Equal(100, tour.Stamina);
        }

        /// <summary>
        /// The cap is per tour: an empty tour takes any country.
        /// </summary>
        [Theory]
        [InlineData(_country.france)]
        [InlineData(_country.US)]
        public void EmptyTour_TakesAnyCountry(_country type)
        {
            SEvent_Tour.tour tour = new();

            tour.SelectCountry(TestGame.Country(type), 1);

            Assert.Equal(new[] { type }, Picked(tour));
        }
    }

    /// <summary>
    /// "World tours give 3.5x more fans to compensate for stamina limitation", on the game's fan roll.
    /// </summary>
    public class FanTests
    {
        public FanTests() => TestGame.Reset(patched: true);

        [Fact]
        public void FanMultiplierIs3Point5()
        {
            Assert.Equal(3.5f, TOUR_FAN_COEFF);
        }

        /// <summary>
        /// The game gives 1-20% of each concert's attendance as new fans; the mod multiplies the result.
        /// </summary>
        [Theory]
        [InlineData(1000, 10f, 350)]
        [InlineData(1000, 1f, 35)]
        [InlineData(20000, 20f, 14000)]
        [InlineData(0, 15f, 0)]
        public void NewFans_Are3Point5TimesTheGames(int attendance, float roll, int expected)
        {
            Seams.Range = (min, max) => roll;

            Assert.Equal(expected, Seams.NewFansByAttendance(new SEvent_Tour.tour(), attendance));
            Assert.Equal(new[] { (1f, 20f) }, Seams.RangesRolled);
        }

        /// <summary>
        /// The mod multiplies the game's rounded fan count, and rounds the way the game does (to even).
        /// </summary>
        [Theory]
        [InlineData(30, 10f)]
        [InlineData(37, 7f)]
        [InlineData(123, 13.7f)]
        [InlineData(5000, 19.99f)]
        public void NewFans_RoundLikeTheGame(int attendance, float roll)
        {
            Seams.Range = (min, max) => roll;
            int gameFans = Mathf.RoundToInt(attendance * (roll / 100f));

            Assert.Equal(Mathf.RoundToInt(gameFans * 3.5f), Seams.NewFansByAttendance(new SEvent_Tour.tour(), attendance));
        }

        /// <summary>
        /// 3 game fans make 10.5, which rounds to even.
        /// </summary>
        [Fact]
        public void NewFans_HalfRoundsToEven()
        {
            Seams.Range = (min, max) => 10f;

            Assert.Equal(10, Seams.NewFansByAttendance(new SEvent_Tour.tour(), 30));
        }
    }

    /// <summary>
    /// The country tooltip warns when picking the country would go over the cap.
    /// </summary>
    public class TooltipTests
    {
        public TooltipTests() => TestGame.Reset(patched: true);

        private static string Warning => "<color=" + mainScript.red + ">Stamina cannot exceed 100pt</color>\n";

        private static string Tooltip(SEvent_Tour.country country, SEvent_Tour.tour tour, int star = 0)
        {
            Seams.Tooltips.Clear();
            TestGame.Star(country, tour, star).SetTooltip();
            return Assert.Single(Seams.Tooltips);
        }

        /// <summary>
        /// The game's tooltip, without the mod's warning.
        /// </summary>
        private static string GameTooltip(SEvent_Tour.country country, int star = 0) => Tooltip(country, new SEvent_Tour.tour(), star);

        [Fact]
        public void GameTooltip_StartsWithStaminaCost()
        {
            Assert.StartsWith("Stamina: <color=" + mainScript.red + ">-20pt</color>\nCost: ", GameTooltip(TestGame.Country()));
        }

        [Fact]
        public void OverTheCap_WarningGoesFirst()
        {
            SEvent_Tour.country country = TestGame.Countries(6)[5];

            Assert.Equal(Warning + GameTooltip(country), Tooltip(country, TestGame.Tour(TestGame.Countries(5))));
        }

        /// <summary>
        /// Every star (level) of the country shows the warning.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(4)]
        public void OverTheCap_EveryStarWarns(int star)
        {
            SEvent_Tour.country country = TestGame.Countries(6)[5];

            Assert.Equal(Warning + GameTooltip(country, star), Tooltip(country, TestGame.Tour(TestGame.Countries(5)), star));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(4)]
        public void UpToTheCap_NoWarning(int alreadyPicked)
        {
            SEvent_Tour.country country = TestGame.Countries(6)[5];

            Assert.Equal(GameTooltip(country), Tooltip(country, TestGame.Tour(TestGame.Countries(alreadyPicked))));
        }

        /// <summary>
        /// A picked country can still change level or be removed, so it doesn't warn even over the cap.
        /// </summary>
        [Fact]
        public void PickedCountry_NoWarning()
        {
            SEvent_Tour.country[] countries = TestGame.Countries(6);

            Assert.Equal(GameTooltip(countries[0]), Tooltip(countries[0], TestGame.Tour(countries)));
        }

        [Fact]
        public void France_WarnsByItsOwnCost()
        {
            SEvent_Tour.country france = TestGame.Country(_country.france);

            Assert.Equal(GameTooltip(france), Tooltip(france, TestGame.Tour(TestGame.Countries(4))));
            Assert.Equal(Warning + GameTooltip(france), Tooltip(france, TestGame.Tour(TestGame.Countries(5))));
        }

        /// <summary>
        /// The warning goes into the game's shared "Stamina" text only while the tooltip is built.
        /// </summary>
        [Fact]
        public void StaminaText_IsRestored()
        {
            SEvent_Tour.tour full = TestGame.Tour(TestGame.Countries(5));

            Tooltip(TestGame.Countries(6)[5], full);
            Tooltip(TestGame.Countries(7)[6], full);

            Assert.Equal("Stamina", Language.Data["STAMINA"]);
            Assert.Equal(Warning + GameTooltip(TestGame.Country(_country.US)), Tooltip(TestGame.Country(_country.US), full));
        }
    }

    /// <summary>
    /// Picking a country redraws every country button, so their tooltips follow the new stamina total.
    /// </summary>
    public class ClickTests
    {
        public ClickTests() => TestGame.Reset();

        [Fact]
        public void Click_RedrawsEveryCountry()
        {
            SEvent_Tour.tour tour = new();
            Tour_Country clicked = TestGame.CountryButton(TestGame.Country(), tour);
            clicked.TourPopup.CountriesContainer = TestGame.Component<GameObject>();
            Tour_Country[] buttons = { TestGame.Component<Tour_Country>(), clicked, TestGame.Component<Tour_Country>() };
            Seams.Children[Seams.TransformOf(clicked.TourPopup.CountriesContainer)] = buttons;

            Seams.OnClickPostfix(clicked);

            Assert.Equal(buttons, Seams.Updated, new SameObject());
        }
    }
}
