using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using _difficulty = staticVars._playerData._difficulty;
using _venue = SEvent_Concerts._venue;

namespace ConcertRebalance.Tests
{
    /// <summary>
    /// The balance the attendance curve and Unfair Open Air Stage cost were tuned for, checked against the game's own venue seats and
    /// costs and the patched attendance: 200% hype, 8 songs, half the fans hardcore, the most profitable ticket price.
    /// </summary>
    public class BalanceTests
    {
        private const int Songs = 8;
        private const float Hype = 200f;
        // Hardcore fans buy at the attendance rate, casual fans at a fifth of it: half and half is 0.6 per fan
        private const float FanWeight = 0.5f + 0.5f / 5f;
        private static readonly _venue[] Venues = (_venue[])Enum.GetValues(typeof(_venue));

        private static float Rate(int price) => new SEvent_Concerts._concert._projectedValues { TicketPrice = price }.GetAttendanceOfDemo();

        // The game's payout at 200% hype: linear for clubs, its curve for every other venue
        private static float HypeMultiplier(_venue venue) => venue == _venue.club ? Hype / 100f : 1.25f;

        private static long Cost(_venue venue) => SEvent_Concerts.GetVenueBaseCost(venue) + Songs * (long)SEvent_Concerts.GetVenueSongCost(venue);

        private static double BestProfit(_venue venue, double fans)
        {
            int capacity = SEvent_Concerts.GetVenueCapacity(venue);
            double best = double.MinValue;
            for (int price = 500; price <= 100000; price += 500)
            {
                double audience = Math.Round(Math.Min(capacity, fans * FanWeight * Rate(price)));
                best = Math.Max(best, audience * price * HypeMultiplier(venue) - Cost(venue));
            }
            return best;
        }

        /// <summary>
        /// Fewest fans that can sell the venue out at the price that just covers its cost.
        /// </summary>
        private static double SellOutFans(_venue venue)
        {
            int capacity = SEvent_Concerts.GetVenueCapacity(venue);
            int price = (int)Math.Ceiling(Cost(venue) / (capacity * HypeMultiplier(venue)) / 100.0) * 100;
            return capacity / Rate(price) / FanWeight;
        }

        [Theory]
        [InlineData(_difficulty.normal)]
        [InlineData(_difficulty.hard)]
        public void EveryVenue_IsTheMostProfitableAtSomeFanCount(_difficulty difficulty)
        {
            ConcertGame.Reset(false, difficulty);
            HashSet<_venue> best = new();
            for (double fans = 5_000; fans <= 20_000_000; fans *= 1.1)
                best.Add(Venues.OrderByDescending(v => BestProfit(v, fans)).First());

            Assert.Equal(Venues.OrderBy(v => v), best.OrderBy(v => v));
        }

        [Fact]
        public void OnUnfair_TheColiseumSellsOutAtAbout22MillionFans()
        {
            ConcertGame.Reset(false, _difficulty.hard);
            Assert.InRange(SellOutFans(_venue.tokyoColiseum), 2_100_000, 2_300_000);
        }

        [Theory]
        [InlineData(_difficulty.normal)]
        [InlineData(_difficulty.hard)]
        public void EachVenue_SellsOutLaterThanTheOneBefore(_difficulty difficulty)
        {
            ConcertGame.Reset(false, difficulty);
            double[] fans = Venues.Select(SellOutFans).ToArray();
            for (int i = 1; i < fans.Length; i++)
                Assert.True(fans[i] > fans[i - 1], $"{Venues[i]} sells out from {fans[i]:N0} fans, {Venues[i - 1]} from {fans[i - 1]:N0}");
        }

        [Fact]
        public void OnUnfair_TheClubGivesWayToTheHallAtAbout250000Fans()
        {
            ConcertGame.Reset(false, _difficulty.hard);
            Assert.True(BestProfit(_venue.club, 220_000) > BestProfit(_venue.concertHall, 220_000));
            Assert.True(BestProfit(_venue.concertHall, 280_000) > BestProfit(_venue.club, 280_000));
        }

        [Fact]
        public void OnUnfair_TheOpenAirStageIsBestBetween700000And1100000Fans()
        {
            ConcertGame.Reset(false, _difficulty.hard);
            foreach (double fans in new[] { 750_000.0, 1_050_000.0 })
                Assert.Equal(_venue.openAirStage, Venues.OrderByDescending(v => BestProfit(v, fans)).First());
        }
    }
}
