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

        private static data_girls.girls IdolWithFans(long people)
        {
            data_girls.girls girl = TestGame.Idol();
            girl.Fans.Add(new resources._fan { age = resources.fanType.teen, people = people });
            return girl;
        }

        /// <summary>
        /// Renders the pies as the game leaves them: Teen and Young Adult at their ratios, Adult at adult + teen.
        /// </summary>
        private static Profile_Fans_Pies Render(float teen, float youngAdult, float adult, long fans = 100)
        {
            Profile_Fans_Pies pies = Pies(teen, youngAdult, adult + teen);
            Profile_Fans_Pies_Render_Pies.Postfix(pies, IdolWithFans(fans));
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
        public void RoundingOvershoot_IsScaledBackToAFullCircle()
        {
            Profile_Fans_Pies pies = Render(0.34f, 0.34f, 0.34f);

            Assert.Equal(1f, Seams.Fill(pies.Fans_Pie_Teen), 4);
            Assert.Equal(2f / 3f, Seams.Fill(pies.Fans_Pie_Adult), 4);
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

            Tour_New_Popup_Render.Postfix(ref popup);

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
}
