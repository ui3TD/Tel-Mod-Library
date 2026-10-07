using System;
using System.Collections.Generic;
using Xunit;
using _venue = SEvent_Concerts._venue;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A set list item worth a fixed amount of hype. An MC, so the production cost doesn't look for its single.
    /// </summary>
    public class FixedHypeItem : SEvent_Concerts._concert.ISetlistItem
    {
        private readonly float hype;

        public FixedHypeItem(float hype) => this.hype = hype;

        public float GetHype(SEvent_Concerts._concert Parent) => hype;
        public bool isMC() => true;
        public void SetTitle(string val) { }
        public string GetTitle() => "";
        public void SetGirl(data_girls.girls girl, int id = 0) { }
        public bool ReplaceGirl(data_girls.girls girl) => false;
        public List<data_girls.girls> GetGirls(bool skipNull = false) => new();
        public int GetSkillValue() => 0;
        public float GetStaminaCost() => 0f;
    }

    /// <summary>
    /// The concert popup's revenue estimate uses the same hype multiplier as the payout: diminishing above
    /// 100% for every venue but clubs, plus the FUJI ticket bonus.
    /// </summary>
    public class ConcertForecastTests
    {
        public ConcertForecastTests() => TestGame.Reset();

        /// <summary>
        /// A concert whose set list adds up to this hype, which it also reaches on the day, with enough fans
        /// to sell out. RecalcProjectedValues is the game's own payout calculation.
        /// </summary>
        private static SEvent_Concerts._concert Concert(_venue venue, float hype, bool fuji)
        {
            resources.Fans.Add(new resources._fan { hardcoreness = resources.fanType.hardcore, people = 10_000_000 });
            if (fuji)
                variables.variable.Add(new variables._variable { name = "FUJI_3_TICKETS", value = "true" });

            SEvent_Concerts._concert concert = new() { Venue = venue, Hype = hype };
            concert.ProjectedValues.Parent = concert;
            concert.SetListItems.Add(new FixedHypeItem(hype));
            concert.ProjectedValues.TicketPrice = 5000;
            concert.RecalcProjectedValues();
            return concert;
        }

        [Theory]
        [InlineData(_venue.club, 150f, false)]
        [InlineData(_venue.club, 200f, true)]
        [InlineData(_venue.concertHall, 50f, false)]
        [InlineData(_venue.concertHall, 100f, true)]
        [InlineData(_venue.concertHall, 150f, false)]
        [InlineData(_venue.openAirStage, 200f, false)]
        [InlineData(_venue.openAirStage, 180f, true)]
        [InlineData(_venue.stadium, 120f, false)]
        [InlineData(_venue.tokyoColiseum, 200f, true)]
        public void Forecast_MatchesPayout(_venue venue, float hype, bool fuji)
        {
            SEvent_Concerts._concert concert = Concert(venue, hype, fuji);

            long forecast = concert.ProjectedValues.GetRevenue();
            long payout = concert.ProjectedValues.Actual_Revenue;
            Assert.True(payout > 0L);
            Assert.True(Math.Abs(forecast - payout) <= Math.Max(1L, payout / 1_000_000L), $"forecast {forecast}, payout {payout}");
        }

        [Fact]
        public void Forecast_AtFullHype_PaysOneAndAQuarter()
        {
            // 1,500 seats × ¥5,000 at 200% hype: ×1.25 (¥9,375,000), not the game's old ×2.0 estimate
            Assert.Equal(9_375_000L, Concert(_venue.concertHall, 200f, false).ProjectedValues.GetRevenue());
        }

        [Fact]
        public void Forecast_ClubsStayLinear()
        {
            // 500 seats × ¥5,000 at 200% hype: the game pays clubs ×2.0
            Assert.Equal(5_000_000L, Concert(_venue.club, 200f, false).ProjectedValues.GetRevenue());
        }

        [Theory]
        [InlineData(2.5f, 2f)]
        [InlineData(2f, 2f)]
        [InlineData(1.5f, 1.5f)]
        [InlineData(0.3f, 0.3f)]
        public void PercentDisplay_IsCappedAt200(float value, float shown)
        {
            float val = value;
            Assert.True(SEvent_Concerts__concert__projectedValues_GetString.Prefix(ref val));
            Assert.Equal(shown, val);
        }
    }
}
