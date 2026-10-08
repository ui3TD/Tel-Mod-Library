using HarmonyLib;
using System;
using Xunit;
using _venue = SEvent_Concerts._venue;

namespace ConcertRebalance.Tests
{
    /// <summary>
    /// Concert Rebalance changes what clubs pay above 100% hype, so it must change the estimate the same way.
    /// </summary>
    public class ClubForecastTests
    {
        [Theory]
        [InlineData(50f, false)]
        [InlineData(100f, false)]
        [InlineData(120f, false)]
        [InlineData(150f, false)]
        [InlineData(200f, false)]
        [InlineData(150f, true)]
        [InlineData(200f, true)]
        public void ClubForecast_MatchesPayout(float hype, bool fuji)
        {
            ConcertGame.Reset(fuji);
            ConcertGame.AssertForecastMatchesPayout(ConcertGame.Concert(_venue.club, hype));
        }

        [Fact]
        public void ClubForecast_UsesTheReducedMultiplier()
        {
            // 500 tickets × ¥5,000 at 200% hype: ×1.25 (¥3,125,000), not the game's linear ×2.0 (¥5,000,000)
            ConcertGame.Reset(false);
            Assert.Equal(3_125_000L, ConcertGame.Concert(_venue.club, 200f).ProjectedValues.GetRevenue());
        }

        [Theory]
        [InlineData(_venue.concertHall)]
        [InlineData(_venue.openAirStage)]
        [InlineData(_venue.stadium)]
        [InlineData(_venue.tokyoColiseum)]
        public void OtherVenues_KeepTheGamesForecast(_venue venue)
        {
            // The game's own estimate: tickets × price × hype, linear for every venue
            ConcertGame.Reset(false);
            SEvent_Concerts._concert concert = ConcertGame.Concert(venue, 150f);
            long expected = (long)UnityEngine.Mathf.Round(concert.ProjectedValues.GetNumberOfSoldTickets() * 5000f * 1.5f);
            Assert.Equal(expected, concert.ProjectedValues.GetRevenue());
        }
    }

    /// <summary>
    /// With Unofficial Patch, which makes the estimate follow the game's payout for every venue,
    /// the estimate matches what is paid everywhere, whichever mod's patch runs first.
    /// </summary>
    public class WithUnofficialPatchTests : IDisposable
    {
        private readonly Harmony unofficialPatch = new("tests.ConcertRebalance.UnofficialPatch");

        public WithUnofficialPatchTests()
        {
            ConcertGame.Reset(false);
            unofficialPatch.CreateClassProcessor(typeof(UnofficialPatch.SEvent_Concerts__concert__projectedValues_GetRevenue)).Patch();
        }

        public void Dispose() => unofficialPatch.UnpatchSelf();

        [Theory]
        [InlineData(_venue.club, 150f, false)]
        [InlineData(_venue.club, 200f, true)]
        [InlineData(_venue.club, 80f, true)]
        [InlineData(_venue.concertHall, 150f, false)]
        [InlineData(_venue.openAirStage, 200f, true)]
        [InlineData(_venue.stadium, 120f, false)]
        [InlineData(_venue.tokyoColiseum, 200f, false)]
        [InlineData(_venue.tokyoColiseum, 90f, true)]
        public void EveryVenueForecast_MatchesPayout(_venue venue, float hype, bool fuji)
        {
            ConcertGame.Reset(fuji);
            ConcertGame.AssertForecastMatchesPayout(ConcertGame.Concert(venue, hype));
        }
    }
}
