using Xunit;
using static ConcertRebalance.ConcertRebalance;

namespace ConcertRebalance.Tests
{
    public class AttendanceTests
    {
        private const float Vanilla = 0.123f;

        [Theory]
        [InlineData(1000, false)]
        [InlineData(3000, false)]
        [InlineData(10000, false)]
        [InlineData(1000, true)]
        [InlineData(2000, true)]   // ×3 = 6000, at the hard threshold
        public void AtOrBelowThreshold_KeepsVanilla(int price, bool hard)
        {
            Assert.Equal(Vanilla, AdjustAttendance(Vanilla, price, hard));
        }

        [Theory]
        // Vanilla at the threshold is (-0.0007142857 * t + 12.142858) / 100:
        // 0.0500 at 10000 (normal), 0.0786 at 6000 (hard, price ×3).
        [InlineData(10001, false, 0.0500f)]
        [InlineData(2001, true, 0.0786f)]
        public void JustAboveThreshold_ContinuesFromVanilla(int price, bool hard, float vanillaAtThreshold)
        {
            float adjusted = AdjustAttendance(Vanilla, price, hard);
            Assert.InRange(adjusted, vanillaAtThreshold - 0.001f, vanillaAtThreshold + 0.001f);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AboveThreshold_FallsAsPriceRises(bool hard)
        {
            float previous = float.MaxValue;
            for (int price = 11000; price <= 50000; price += 1000)
            {
                float current = AdjustAttendance(Vanilla, price, hard);
                Assert.True(current < previous, $"price {price}: {current} >= {previous}");
                Assert.True(current > 0f);
                previous = current;
            }
        }

        [Fact]
        public void AboveThreshold_HalvesRoughlyEvery7000Yen()
        {
            // 1.0001^-6931 ≈ 0.5
            float at = AdjustAttendance(Vanilla, 10000 + 1, false);
            float later = AdjustAttendance(Vanilla, 10000 + 1 + 6931, false);
            Assert.InRange(later / at, 0.49f, 0.51f);
        }
    }

    public class ClubHypeTests
    {
        [Theory]
        [InlineData(100f, 1f)]
        [InlineData(150f, 1.1875f)]
        [InlineData(200f, 1.25f)]
        public void Multiplier_KnownPoints(float hype, float expected)
        {
            Assert.Equal(expected, HypeMultiplierAbove100(hype), 4);
        }

        [Fact]
        public void Multiplier_RisesUpToTheGameCapOf200()
        {
            float previous = HypeMultiplierAbove100(100f);
            for (float hype = 105f; hype <= 200f; hype += 5f)
            {
                float current = HypeMultiplierAbove100(hype);
                Assert.True(current > previous, $"hype {hype}: {current} <= {previous}");
                previous = current;
            }
        }

        [Fact]
        public void Multiplier_IsBelowVanillaClubLinearHype()
        {
            // Vanilla pays clubs Hype / 100; the mod's whole point is to pay less above 100.
            for (float hype = 105f; hype <= 200f; hype += 5f)
                Assert.True(HypeMultiplierAbove100(hype) < hype / 100f);
        }

        [Theory]
        [InlineData(500L, 5000, 200f, false, 3125000L)]
        [InlineData(500L, 5000, 200f, true, 3281250L)]
        [InlineData(500L, 5000, 150f, false, 2968750L)]
        [InlineData(0L, 5000, 200f, false, 0L)]
        public void Revenue_KnownValues(long audience, int price, float hype, bool fuji, long expected)
        {
            Assert.Equal(expected, ClubRevenue(audience, price, hype, fuji));
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
