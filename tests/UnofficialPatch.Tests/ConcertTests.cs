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

        /// <summary>
        /// The whole percent in one of the popup's coloured percent strings.
        /// </summary>
        private static int ShownPercent(string text) =>
            int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"(\d+)%").Groups[1].Value);

        [Theory]
        [InlineData(1f, 100)]
        [InlineData(0.999f, 99)]   // the game showed 100%: sold out, though it isn't
        [InlineData(0.995f, 99)]
        [InlineData(0.994f, 99)]
        [InlineData(0.29f, 29)]    // float error must not floor an exact percent one point low
        [InlineData(0.8f, 80)]
        [InlineData(0.456f, 45)]
        [InlineData(0f, 0)]
        public void Attendance_IsRoundedDown(float attendance, int shown)
        {
            SEvent_Concerts._concert._projectedValues values = new() { Attendance = attendance };
            Assert.Equal(shown, ShownPercent(values.GetAttendanceString()));
        }

        [Fact]
        public void Attendance_ColourFollowsTheShownPercent()
        {
            // The game colours below 30% red; 29.9% now shows as 29, so it must be red too
            SEvent_Concerts._concert._projectedValues low = new() { Attendance = 0.299f };
            SEvent_Concerts._concert._projectedValues ok = new() { Attendance = 0.3f };
            Assert.Contains(mainScript.red, low.GetAttendanceString());
            Assert.DoesNotContain(mainScript.red, ok.GetAttendanceString());
        }

        [Theory]
        [InlineData(200f, 200)]
        [InlineData(199.5f, 199)]
        [InlineData(150f, 150)]
        public void Hype_IsRoundedDown(float hype, int shown)
        {
            SEvent_Concerts._concert concert = new();
            concert.ProjectedValues.Parent = concert;
            concert.SetListItems.Add(new FixedHypeItem(hype));
            Assert.Equal(shown, ShownPercent(concert.ProjectedValues.GetHypeString()));
        }

        [Fact]
        public void Display_IsCappedAt200()
        {
            float val = 2.5f;
            Assert.True(SEvent_Concerts__concert__projectedValues_GetString.Prefix(ref val));
            Assert.Equal(2f, val);
        }
    }
}
