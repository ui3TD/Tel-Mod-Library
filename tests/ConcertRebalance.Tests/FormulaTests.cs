using HarmonyLib;
using System;
using System.Reflection;
using Xunit;
using static ConcertRebalance.ConcertRebalance;
using _difficulty = staticVars._playerData._difficulty;
using _venue = SEvent_Concerts._venue;

namespace ConcertRebalance.Tests
{
    public class AttendanceTests
    {
        // The game's straight line, which it uses for prices from 3,000 up to where it leaves it
        private static float GameLine(int priceFactor) => (-0.0007142857f * priceFactor + 12.142858f) / 100f;

        // The mod's rate when the game's own rate comes in, as with no other mod
        private static float Adjusted(int price, bool hard) => AdjustAttendance(GameAttendance(price, hard), price, hard);

        /// <summary>
        /// The 1.3.0 formula, which replaced the game's rate above its line: with no other mod, 1.3.1 must match it exactly.
        /// </summary>
        private static float Formula130(float vanillaAttendance, int ticketPrice, bool isHard)
        {
            int ticketPriceFactor = isHard ? ticketPrice * 3 : ticketPrice;
            if (ticketPriceFactor <= PRICE_THRESHOLD)
            {
                if (ticketPriceFactor <= (isHard ? GAME_LINE_END_HARD : GAME_LINE_END))
                    return vanillaAttendance;
                return (-0.0007142857f * ticketPriceFactor + 12.142858f) / 100f;
            }

            return ATTENDANCE_AT_THRESHOLD * (float)Math.Pow(PRICE_SCALING_BASE, PRICE_THRESHOLD - ticketPriceFactor) / 100f;
        }

        private static readonly Lazy<Func<SEvent_Concerts._concert._projectedValues, float>> Unpatched = new(() =>
        {
            Harmony.ReversePatch(AccessTools.Method(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetAttendanceOfDemo)),
                new HarmonyMethod(typeof(AttendanceTests), nameof(GameGetAttendanceOfDemo)));
            return GameGetAttendanceOfDemo;
        });

        // Replaced by a copy of the game's GetAttendanceOfDemo without any patches
        private static float GameGetAttendanceOfDemo(SEvent_Concerts._concert._projectedValues instance) => throw new NotImplementedException();

        private static SEvent_Concerts._concert._projectedValues PricedAt(int price) =>
            ConcertGame.Concert(_venue.club, 100f, price).ProjectedValues;

        [Theory]
        [InlineData(_difficulty.normal)]
        [InlineData(_difficulty.hard)]
        public void GameAttendance_IsTheGamesFormula(_difficulty difficulty)
        {
            ConcertGame.Reset(difficulty: difficulty);
            bool hard = difficulty == _difficulty.hard;
            for (int price = 0; price <= 100000; price += 50)
                Assert.Equal(Unpatched.Value(PricedAt(price)), GameAttendance(price, hard));
        }

        [Theory]
        [InlineData(_difficulty.normal)]
        [InlineData(_difficulty.hard)]
        public void WithNoOtherMod_TheRateIs130sExactly(_difficulty difficulty)
        {
            ConcertGame.Reset(difficulty: difficulty);
            bool hard = difficulty == _difficulty.hard;
            for (int price = 0; price <= 100000; price += 50)
            {
                float game = Unpatched.Value(PricedAt(price));
                Assert.Equal(Formula130(game, price, hard), PricedAt(price).GetAttendanceOfDemo());
            }
        }

        [Theory]
        [InlineData(5000, false)]
        [InlineData(20000, false)]
        [InlineData(2500, true)]
        [InlineData(20000, true)]
        public void AnotherModsMultiplierComingIn_IsKept(int price, bool hard)
        {
            float adjusted = AdjustAttendance(1.1f * GameAttendance(price, hard), price, hard);
            Assert.Equal(1.1f * ModAttendance(price, hard), adjusted, 6);
        }

        private const string OtherModId = "tests.ConcertRebalance.OtherMod";

        private static void OtherModBonus(ref float __result) => __result *= 1.1f;

        /// <summary>
        /// Another mod that raises attendance by 10% keeps its bonus, whether its patch runs before or after the mod's.
        /// </summary>
        [Theory]
        [InlineData(Priority.High)]
        [InlineData(Priority.Low)]
        public void AnotherModsAttendanceBonus_IsKeptInEitherOrder(int priority)
        {
            ConcertGame.Reset();
            Harmony otherMod = new(OtherModId);
            MethodInfo method = AccessTools.Method(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetAttendanceOfDemo));
            try
            {
                otherMod.Patch(method, postfix: new HarmonyMethod(typeof(AttendanceTests), nameof(OtherModBonus)) { priority = priority });
                foreach (int price in new[] { 5000, 20000, 50000 })
                    Assert.Equal(1.1f * ModAttendance(price, false), PricedAt(price).GetAttendanceOfDemo(), 6);
            }
            finally
            {
                otherMod.UnpatchSelf();
            }
        }

        [Theory]
        [InlineData(1000, false)]
        [InlineData(3000, false)]
        [InlineData(9000, false)]
        [InlineData(1000, true)]
        [InlineData(2000, true)]   // ×3 = 6000, where the game leaves its straight line on Unfair
        public void WhereTheGameIsOnItsLine_KeepsVanilla(int price, bool hard)
        {
            Assert.Equal(GameAttendance(price, hard), Adjusted(price, hard));
        }

        [Theory]
        [InlineData(2100)]
        [InlineData(2500)]
        [InlineData(3000)]         // ×3 = 9000, the threshold
        public void OnUnfair_ContinuesTheGamesLineUpToTheThreshold(int price)
        {
            Assert.Equal(GameLine(price * 3), Adjusted(price, true), 5);
        }

        [Theory]
        [InlineData(9001, false)]
        [InlineData(3001, true)]   // ×3 = 9003
        public void JustAboveThreshold_StepsDownTo543Percent(int price, bool hard)
        {
            float adjusted = Adjusted(price, hard);
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
                float current = Adjusted(price, hard);
                Assert.True(current < previous, $"price {price}: {current} >= {previous}");
                Assert.True(current > 0f);
                previous = current;
            }
        }

        [Fact]
        public void AboveThreshold_HalvesRoughlyEvery8700Yen()
        {
            // 1.00008^-8665 ≈ 0.5
            float at = Adjusted(PRICE_THRESHOLD + 1, false);
            float later = Adjusted(PRICE_THRESHOLD + 1 + 8665, false);
            Assert.InRange(later / at, 0.49f, 0.51f);
        }

        [Fact]
        public void AtTheHighestPrice_AlmostNobodyBuys()
        {
            // The game's own high-price curve never falls below 0.86%, which made ¥100,000 tickets the best price;
            // the mod's ends below a tenth of that
            Assert.True(Adjusted(100000, true) < 0.00086f);
            Assert.True(Adjusted(100000, false) < 0.00086f);
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
