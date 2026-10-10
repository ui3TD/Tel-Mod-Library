using HarmonyLib;
using System;
using Xunit;
using _venue = SEvent_Concerts._venue;

namespace ConcertRebalance.Tests
{
    /// <summary>
    /// Clubs keep the game's own hype payout (linear, ×2.0 at 200%), which the game's estimate already matches.
    /// </summary>
    public class ClubForecastTests
    {
        [Theory]
        [InlineData(50f, false)]
        [InlineData(100f, false)]
        [InlineData(120f, false)]
        [InlineData(150f, false)]
        [InlineData(200f, false)]
        public void ClubForecast_MatchesPayout(float hype, bool fuji)
        {
            // Without the FUJI ticket deal: the game's estimate leaves out its +5% at every venue, which Unofficial
            // Patch fixes (WithUnofficialPatchTests)
            ConcertGame.Reset(fuji);
            ConcertGame.AssertForecastMatchesPayout(ConcertGame.Concert(_venue.club, hype));
        }

        [Fact]
        public void ClubForecast_HasTheFullHypeBonus()
        {
            // 500 tickets × ¥5,000 at 200% hype: the game's linear ×2.0
            ConcertGame.Reset(false);
            Assert.Equal(5_000_000L, ConcertGame.Concert(_venue.club, 200f).ProjectedValues.GetRevenue());
        }

        [Fact]
        public void ClubPayout_HasTheFullHypeBonus()
        {
            ConcertGame.Reset(false);
            Assert.Equal(5_000_000L, ConcertGame.Concert(_venue.club, 200f).ProjectedValues.Actual_Revenue);
        }

        [Theory]
        [InlineData(_venue.concertHall)]
        [InlineData(_venue.tokyoColiseum)]
        public void OtherVenuesPayout_KeepsTheGamesCurve(_venue venue)
        {
            // The game's curve at 200% hype: ×1.25
            ConcertGame.Reset(false);
            SEvent_Concerts._concert concert = ConcertGame.Concert(venue, 200f);
            Assert.Equal((long)UnityEngine.Mathf.Round(concert.ProjectedValues.Actual_Audience * 5000f * 1.25f), concert.ProjectedValues.Actual_Revenue);
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
