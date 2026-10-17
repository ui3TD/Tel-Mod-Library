using System;
using HarmonyLib;
using UnityEngine;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// The fan age pie shows teen, young adult and adult slices in proportion. The prefab draws Teen as the
    /// base image with Adult and Young Adult on top, so each fill is cumulative.
    /// </summary>
    public class FanPieTests
    {
        public FanPieTests() => TestGame.Reset();

        private static Profile_Fans_Pies Pies(float teen, float youngAdult, float adultPlusTeen)
        {
            Profile_Fans_Pies pies = TestGame.Component<Profile_Fans_Pies>();
            pies.Fans_Pie_Teen = Seams.ImageObject(teen);
            pies.Fans_Pie_YA = Seams.ImageObject(youngAdult);
            pies.Fans_Pie_Adult = Seams.ImageObject(adultPlusTeen);
            return pies;
        }

        // Fans per unit of ratio, so ratios like 0.2 are exact counts.
        private const long FansPerRatio = 1000;

        /// <summary>
        /// An idol whose teen, young adult and adult fans are in these ratios.
        /// </summary>
        private static data_girls.girls IdolWithAges(float teen, float youngAdult, float adult)
        {
            data_girls.girls girl = TestGame.Idol();
            girl.Fans.Add(new resources._fan { age = resources.fanType.teen, people = (long)Math.Round(teen * FansPerRatio) });
            girl.Fans.Add(new resources._fan { age = resources.fanType.youngAdult, people = (long)Math.Round(youngAdult * FansPerRatio) });
            girl.Fans.Add(new resources._fan { age = resources.fanType.adult, people = (long)Math.Round(adult * FansPerRatio) });
            return girl;
        }

        /// <summary>
        /// Renders the pies after the game left them: Teen and Young Adult at their ratios, Adult at adult + teen.
        /// </summary>
        private static Profile_Fans_Pies Render(float teen, float youngAdult, float adult)
        {
            Profile_Fans_Pies pies = Pies(teen, youngAdult, adult + teen);
            Profile_Fans_Pies_Render_Pies.Postfix(pies, IdolWithAges(teen, youngAdult, adult));
            return pies;
        }

        [Theory]
        [InlineData(0.2f, 0.3f, 0.5f)]
        [InlineData(0.6f, 0.1f, 0.3f)]
        [InlineData(0f, 0f, 1f)]
        [InlineData(1f, 0f, 0f)]
        public void Slices_MatchTheRatios(float teen, float youngAdult, float adult)
        {
            Profile_Fans_Pies pies = Render(teen, youngAdult, adult);

            // Visible: Young Adult 0..ya, Adult ya..ya+adult, Teen the rest
            Assert.Equal(youngAdult, Seams.Fill(pies.Fans_Pie_YA), 4);
            Assert.Equal(youngAdult + adult, Seams.Fill(pies.Fans_Pie_Adult), 4);
            Assert.Equal(1f, Seams.Fill(pies.Fans_Pie_Teen), 4);
        }

        [Fact]
        public void PiesAnotherModAlreadyFixed_EndTheSame()
        {
            // Another mod already drew the cumulative fills: Young Adult, Young Adult + Adult, then the whole pie
            Profile_Fans_Pies pies = Pies(1f, 0.3f, 0.8f);

            Profile_Fans_Pies_Render_Pies.Postfix(pies, IdolWithAges(0.2f, 0.3f, 0.5f));

            Assert.Equal(0.3f, Seams.Fill(pies.Fans_Pie_YA), 4);
            Assert.Equal(0.8f, Seams.Fill(pies.Fans_Pie_Adult), 4);
            Assert.Equal(1f, Seams.Fill(pies.Fans_Pie_Teen), 4);
        }

        [Fact]
        public void RoundingOvershoot_IsScaledBackToAFullCircle()
        {
            Profile_Fans_Pies pies = Pies(0f, 0f, 0f);

            Profile_Fans_Pies_Render_Pies.SetFills(pies, 0.34f, 0.34f, 0.34f);

            Assert.Equal(1f, Seams.Fill(pies.Fans_Pie_Teen), 4);
            Assert.Equal(2f / 3f, Seams.Fill(pies.Fans_Pie_Adult), 4);
            Assert.Equal(1f / 3f, Seams.Fill(pies.Fans_Pie_YA), 4);
        }

        [Fact]
        public void NoFans_KeepsTheGamesPlaceholder()
        {
            Profile_Fans_Pies pies = Pies(0.33f, 0.33f, 0.66f);

            Profile_Fans_Pies_Render_Pies.Postfix(pies, TestGame.Idol());

            Assert.Equal(0.33f, Seams.Fill(pies.Fans_Pie_Teen));
            Assert.Equal(0.66f, Seams.Fill(pies.Fans_Pie_Adult));
        }
    }

    /// <summary>
    /// The new tour popup's expected revenue is green when it beats the production cost after savings.
    /// </summary>
    public class TourRevenueColorTests
    {
        public TourRevenueColorTests() => TestGame.Reset();

        private static Color32 RevenueColor(int expectedRevenue, int productionCost, int saving)
        {
            Tour_New_Popup popup = TestGame.Component<Tour_New_Popup>();
            popup.Tour = new SEvent_Tour.tour { ExpectedRevenue = expectedRevenue, ProductionCost = productionCost, Saving = saving };
            popup.ExpectedRevenue = TestGame.Component<GameObject>();

            Tour_New_Popup_Render.Postfix(popup);

            Assert.Single(Seams.ColorsSet);
            Assert.Same(popup.ExpectedRevenue, Seams.ColorsSet[0].obj);
            return Seams.ColorsSet[0].color;
        }

        [Theory]
        [InlineData(100_000, 90_000, 0)]
        [InlineData(100_000, 120_000, 30_000)] // the game showed this red: it ignored the savings
        public void Profitable_IsGreen(int revenue, int cost, int saving)
        {
            Assert.Equal(mainScript.green32, RevenueColor(revenue, cost, saving));
        }

        [Theory]
        [InlineData(100_000, 150_000, 30_000)]
        [InlineData(100_000, 150_000, 0)]
        [InlineData(100_000, 100_000, 0)] // breaking even
        public void NotProfitable_IsRed(int revenue, int cost, int saving)
        {
            Assert.Equal(mainScript.red32, RevenueColor(revenue, cost, saving));
        }
    }

    /// <summary>
    /// The show and concert editors' idol pickers keep cast idols greyed out. The postfix's hierarchy lookups are
    /// Unity native code and are checked in game; these tests cover which idols count as the cast.
    /// </summary>
    public class PickerCastTests
    {
        public PickerCastTests() => TestGame.Reset();

        private static Show_Popup ShowEditor(params data_girls.girls[] slots)
        {
            Show_Popup popup = TestGame.Component<Show_Popup>();
            AccessTools.Field(typeof(Show_Popup), "girls").SetValue(popup, slots);
            return popup;
        }

        private static Concert_New_Popup ConcertEditor(SEvent_Concerts._concert.ISetlistItem item)
        {
            Concert_New_Popup popup = TestGame.Component<Concert_New_Popup>();
            AccessTools.Field(typeof(Concert_New_Popup), "girl_setListItem").SetValue(popup, item);
            return popup;
        }

        [Fact]
        public void ShowEditor_CastIdolsAreGreyed()
        {
            data_girls.girls cast = TestGame.Idol(name: "Cast");
            data_girls.girls free = TestGame.Idol(name: "Free");
            Show_Popup popup = ShowEditor(cast, null, null);

            Assert.True(GirlButtonSmall_UpdateData.InCast(cast, GirlButtonSmall_UpdateData.ShowCast(popup)));
            Assert.False(GirlButtonSmall_UpdateData.InCast(free, GirlButtonSmall_UpdateData.ShowCast(popup)));
        }

        [Fact]
        public void ConcertEditor_CastIdolsOfTheItemBeingCastAreGreyed()
        {
            data_girls.girls cast = TestGame.Idol(name: "Cast");
            data_girls.girls free = TestGame.Idol(name: "Free");
            SEvent_Concerts._concert._mc mc = new SEvent_Concerts._concert._mc();
            mc.Girls[1] = cast;
            Concert_New_Popup popup = ConcertEditor(mc);

            Assert.True(GirlButtonSmall_UpdateData.InCast(cast, GirlButtonSmall_UpdateData.ConcertCast(popup)));
            Assert.False(GirlButtonSmall_UpdateData.InCast(free, GirlButtonSmall_UpdateData.ConcertCast(popup)));
        }

        [Fact]
        public void ConcertEditor_BeforeThePickerFirstOpens_NothingIsGreyed()
        {
            Concert_New_Popup popup = ConcertEditor(null);

            Assert.Null(GirlButtonSmall_UpdateData.ConcertCast(popup));
            Assert.False(GirlButtonSmall_UpdateData.InCast(TestGame.Idol(), GirlButtonSmall_UpdateData.ConcertCast(popup)));
        }

        [Fact]
        public void EmptySlots_DontGreyTheClearButton()
        {
            // The picker's "nobody" button has no idol; empty slots must not match it
            Assert.False(GirlButtonSmall_UpdateData.InCast(null, GirlButtonSmall_UpdateData.ShowCast(ShowEditor(null, null, null))));
        }
    }
}
