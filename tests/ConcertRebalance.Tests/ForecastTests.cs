using HarmonyLib;
using System;
using System.Collections.Generic;
using Xunit;
using _venue = SEvent_Concerts._venue;

// The forecast tests share the game's static state (fans, variables, difficulty) and patch the game.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ConcertRebalance.Tests
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
    /// Builds a concert and compares the popup's projected revenue with what the game pays.
    /// </summary>
    public static class ConcertGame
    {
        private static readonly Lazy<bool> Patched = new(() =>
        {
            // Not "tests.ConcertRebalance": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.ConcertRebalance.Behaviour");
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(ConcertRebalance).Assembly, "ConcertRebalance"))
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        /// <summary>
        /// Resets the game to Normal with no scandals or awards, and enough hardcore fans to sell out any venue.
        /// </summary>
        public static void Reset(bool fujiTickets)
        {
            _ = Patched.Value;
            staticVars.PlayerData.Difficulty = staticVars._playerData._difficulty.normal;
            staticVars.dateTime = new DateTime(2023, 6, 5);
            data_girls.girl = new List<data_girls.girls>();
            resources.resource = new List<long>(new long[32]);
            resources.Fans = new List<resources._fan>
            {
                new() { hardcoreness = resources.fanType.hardcore, people = 10_000_000 },
            };
            Awards._Awards = new List<Awards._award>();
            variables.variable = new List<variables._variable>();
            if (fujiTickets)
                variables.variable.Add(new variables._variable { name = "FUJI_3_TICKETS", value = "true" });
        }

        /// <summary>
        /// A concert whose set list adds up to this hype, which it also reaches on the day.
        /// </summary>
        public static SEvent_Concerts._concert Concert(_venue venue, float hype, int ticketPrice = 5000)
        {
            SEvent_Concerts._concert concert = new() { Venue = venue, Hype = hype };
            concert.ProjectedValues.Parent = concert;
            concert.SetListItems.Add(new FixedHypeItem(hype));
            concert.ProjectedValues.TicketPrice = ticketPrice;
            concert.RecalcProjectedValues();
            return concert;
        }

        /// <summary>
        /// Asserts the popup's estimate is what the game pays, allowing for float rounding.
        /// </summary>
        public static void AssertForecastMatchesPayout(SEvent_Concerts._concert concert)
        {
            long forecast = concert.ProjectedValues.GetRevenue();
            long payout = concert.ProjectedValues.Actual_Revenue;
            Assert.True(payout > 0L);
            Assert.True(Math.Abs(forecast - payout) <= Math.Max(1L, payout / 1_000_000L),
                $"{concert.Venue} at {concert.Hype}% hype: forecast {forecast}, payout {payout}");
        }
    }

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
