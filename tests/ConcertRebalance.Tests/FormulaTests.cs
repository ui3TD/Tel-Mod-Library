using Xunit;
using static ConcertRebalance.ConcertRebalance;

namespace ConcertRebalance.Tests
{
    public class AttendanceTests
    {
        private const float Vanilla = 0.123f;

        // The game's straight line, which it uses for prices from 3,000 up to where it leaves it
        private static float GameLine(int priceFactor) => (-0.0007142857f * priceFactor + 12.142858f) / 100f;

        [Theory]
        [InlineData(1000, false)]
        [InlineData(3000, false)]
        [InlineData(9000, false)]
        [InlineData(1000, true)]
        [InlineData(2000, true)]   // ×3 = 6000, where the game leaves its straight line on Unfair
        public void WhereTheGameIsOnItsLine_KeepsVanilla(int price, bool hard)
        {
            Assert.Equal(Vanilla, AdjustAttendance(Vanilla, price, hard));
        }

        [Theory]
        [InlineData(2100)]
        [InlineData(2500)]
        [InlineData(3000)]         // ×3 = 9000, the threshold
        public void OnUnfair_ContinuesTheGamesLineUpToTheThreshold(int price)
        {
            Assert.Equal(GameLine(price * 3), AdjustAttendance(Vanilla, price, true), 5);
        }

        [Theory]
        [InlineData(9001, false)]
        [InlineData(3001, true)]   // ×3 = 9003
        public void JustAboveThreshold_StepsDownTo543Percent(int price, bool hard)
        {
            float adjusted = AdjustAttendance(Vanilla, price, hard);
            Assert.InRange(adjusted, 0.0541f, 0.0543f);
            Assert.True(adjusted < GameLine(PRICE_THRESHOLD), "a higher price must never sell more tickets");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AboveThreshold_FallsAsPriceRises(bool hard)
        {
            float previous = float.MaxValue;
            for (int price = 9100; price <= 100000; price += 900)
            {
                float current = AdjustAttendance(Vanilla, price, hard);
                Assert.True(current < previous, $"price {price}: {current} >= {previous}");
                Assert.True(current > 0f);
                previous = current;
            }
        }

        [Fact]
        public void AboveThreshold_HalvesRoughlyEvery8700Yen()
        {
            // 1.00008^-8665 ≈ 0.5
            float at = AdjustAttendance(Vanilla, PRICE_THRESHOLD + 1, false);
            float later = AdjustAttendance(Vanilla, PRICE_THRESHOLD + 1 + 8665, false);
            Assert.InRange(later / at, 0.49f, 0.51f);
        }

        [Fact]
        public void AtTheHighestPrice_AlmostNobodyBuys()
        {
            // The game's own high-price curve never falls below 0.86%, which made ¥100,000 tickets the best price;
            // the mod's ends below a tenth of that
            Assert.True(AdjustAttendance(Vanilla, 100000, true) < 0.00086f);
            Assert.True(AdjustAttendance(Vanilla, 100000, false) < 0.00086f);
        }
    }

    public class VenueUnlockTests
    {
        [Theory]
        [InlineData(1f, 0L, true)]
        [InlineData(1f, 1000L, true)]
        [InlineData(0.99f, 1000L, false)]
        [InlineData(1f, -1L, false)]
        public void RequiresSellOutWithoutLoss(float attendance, long profit, bool expected)
        {
            Assert.Equal(expected, QualifiesForVenueUnlock(attendance, profit));
        }
    }
}
