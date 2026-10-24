using HarmonyLib;
using System.Reflection;
using Xunit;
using _difficulty = staticVars._playerData._difficulty;
using _status = SEvent_Tour.tour._status;
using _venue = SEvent_Concerts._venue;

namespace ConcertRebalance.Tests
{
    /// <summary>
    /// The game unlocks the next venue as soon as a concert starts at the newest one. With the mod, the concert
    /// must finish sold out and without a loss.
    /// </summary>
    public class VenueUnlockingTests
    {
        public VenueUnlockingTests() => ConcertGame.Reset();

        private const string OtherModId = "tests.ConcertRebalance.OtherMod";

        [Fact]
        public void StartingAConcert_DoesNotUnlock()
        {
            // The game's StartConcert calls UpdateVenueUnlocked with the concert it starts
            SEvent_Concerts._concert concert = ConcertGame.Concert(_venue.club, 100f);
            SEvent_Concerts_StartConcert.Prefix();
            SEvent_Concerts.UpdateVenueUnlocked(concert);
            SEvent_Concerts_StartConcert.Postfix();
            Assert.Equal(_venue.club, SEvent_Concerts.UnlockedVenue);
        }

        [Theory]
        [InlineData(_status.normal)]
        [InlineData(_status.working)]
        public void AnotherModsUnlock_OutsideTheStart_GoesThrough(_status status)
        {
            SEvent_Concerts._concert concert = ConcertGame.Concert(_venue.club, 100f);
            concert.Status = status;
            SEvent_Concerts.UpdateVenueUnlocked(concert);
            Assert.Equal(_venue.concertHall, SEvent_Concerts.UnlockedVenue);
        }

        [Fact]
        public void AStartThatThrows_StillEndsTheWindow()
        {
            SEvent_Concerts._concert concert = ConcertGame.Concert(_venue.club, 100f);
            SEvent_Concerts_StartConcert.Prefix();
            SEvent_Concerts_StartConcert.Finalizer();
            SEvent_Concerts.UpdateVenueUnlocked(concert);
            Assert.Equal(_venue.concertHall, SEvent_Concerts.UnlockedVenue);
        }

        private static bool SkipOriginal() => false;

        [Fact]
        public void FinishSkippedByAnotherMod_DoesNotUnlock()
        {
            // The concert would qualify: sold out with a profit
            Harmony otherMod = new(OtherModId);
            MethodInfo finish = AccessTools.Method(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish));
            try
            {
                otherMod.Patch(finish, prefix: new HarmonyMethod(typeof(VenueUnlockingTests), nameof(SkipOriginal)));
                SEvent_Concerts._concert concert = ConcertGame.Concert(_venue.club, 100f);
                concert.Finish();
                Assert.Equal(_venue.club, SEvent_Concerts.UnlockedVenue);
            }
            finally
            {
                otherMod.UnpatchSelf();
            }
        }

        [Theory]
        [InlineData(_venue.club, _venue.concertHall)]
        [InlineData(_venue.concertHall, _venue.openAirStage)]
        [InlineData(_venue.openAirStage, _venue.stadium)]
        [InlineData(_venue.stadium, _venue.tokyoColiseum)]
        public void SoldOutWithAProfit_UnlocksTheNextVenue(_venue venue, _venue next)
        {
            SEvent_Concerts.UnlockedVenue = venue;
            SEvent_Concerts._concert concert = ConcertGame.Concert(venue, 100f);
            concert.Finish();

            Assert.Equal(_status.finished, concert.Status);
            Assert.Equal(1f, concert.ProjectedValues.Actual_Attendance);
            Assert.True(concert.ProjectedValues.GetActualProfit() > 0L);
            Assert.Equal(next, SEvent_Concerts.UnlockedVenue);
        }

        [Fact]
        public void SoldOutAtALoss_DoesNotUnlock()
        {
            // 500 seats × ¥5,000 at 10% hype pays ¥250,000; the club costs ¥500,000
            SEvent_Concerts._concert concert = ConcertGame.Concert(_venue.club, 10f);
            concert.Finish();

            Assert.Equal(1f, concert.ProjectedValues.Actual_Attendance);
            Assert.Equal(-250_000L, concert.ProjectedValues.GetActualProfit());
            Assert.Equal(_venue.club, SEvent_Concerts.UnlockedVenue);
        }

        [Fact]
        public void ProfitableButNotSoldOut_DoesNotUnlock()
        {
            // 5,000 hardcore fans buy 429 of the club's 500 tickets at ¥5,000
            resources.Fans[0].people = 5000;
            SEvent_Concerts._concert concert = ConcertGame.Concert(_venue.club, 100f);
            concert.Finish();

            Assert.Equal(429L, concert.ProjectedValues.Actual_Audience);
            Assert.True(concert.ProjectedValues.GetActualProfit() > 0L);
            Assert.Equal(_venue.club, SEvent_Concerts.UnlockedVenue);
        }

        [Fact]
        public void EarlierVenue_DoesNotUnlockAnother()
        {
            SEvent_Concerts.UnlockedVenue = _venue.openAirStage;
            ConcertGame.Concert(_venue.club, 100f).Finish();
            Assert.Equal(_venue.openAirStage, SEvent_Concerts.UnlockedVenue);
        }

        [Fact]
        public void Coliseum_IsTheLastVenue()
        {
            SEvent_Concerts.UnlockedVenue = _venue.tokyoColiseum;
            ConcertGame.Concert(_venue.tokyoColiseum, 100f).Finish();
            Assert.Equal(_venue.tokyoColiseum, SEvent_Concerts.UnlockedVenue);
        }
    }

    /// <summary>
    /// On hard, the Tokyo Coliseum seats 50,000 instead of the game's 30,000, and its base cost is ¥200,000,000
    /// instead of ¥100,000,000. Every other venue and difficulty keeps the game's figures.
    /// </summary>
    public class HardColiseumTests
    {
        [Fact]
        public void Capacity_Is50000()
        {
            ConcertGame.Reset(difficulty: _difficulty.hard);
            Assert.Equal(50_000, SEvent_Concerts.GetVenueCapacity(_venue.tokyoColiseum));
        }

        [Fact]
        public void BaseCost_Is200Million()
        {
            ConcertGame.Reset(difficulty: _difficulty.hard);
            Assert.Equal(200_000_000, SEvent_Concerts.GetVenueBaseCost(_venue.tokyoColiseum));
        }

        [Fact]
        public void OpenAirStageBaseCost_Is20Million()
        {
            // The game's Unfair ¥25,000,000 made it the dearest seat in the game
            ConcertGame.Reset(difficulty: _difficulty.hard);
            Assert.Equal(20_000_000, SEvent_Concerts.GetVenueBaseCost(_venue.openAirStage));
        }

        [Fact]
        public void SoldOutConcert_Seats50000AndCosts200Million()
        {
            // The popup and the payout both read the larger venue: 50,000 × ¥5,000 against ¥200,000,000
            ConcertGame.Reset(difficulty: _difficulty.hard);
            SEvent_Concerts._concert concert = ConcertGame.Concert(_venue.tokyoColiseum, 100f);

            Assert.Equal(50_000L, concert.ProjectedValues.GetNumberOfSoldTickets());
            Assert.Equal(50_000L, concert.ProjectedValues.Actual_Audience);
            Assert.Equal(200_000_000L, concert.ProjectedValues.GetProductionCost());
            Assert.Equal(50_000_000L, concert.ProjectedValues.GetActualProfit());
        }

        [Theory]
        [InlineData(_difficulty.hard, _venue.club, 500)]
        [InlineData(_difficulty.hard, _venue.concertHall, 1500)]
        [InlineData(_difficulty.hard, _venue.openAirStage, 5000)]
        [InlineData(_difficulty.hard, _venue.stadium, 15000)]
        [InlineData(_difficulty.normal, _venue.stadium, 25000)]
        [InlineData(_difficulty.normal, _venue.tokyoColiseum, 50000)]
        [InlineData(_difficulty.easy, _venue.tokyoColiseum, 50000)]
        public void OtherCapacities_AreTheGames(_difficulty difficulty, _venue venue, int capacity)
        {
            ConcertGame.Reset(difficulty: difficulty);
            Assert.Equal(capacity, SEvent_Concerts.GetVenueCapacity(venue));
        }

        [Theory]
        [InlineData(_difficulty.hard, _venue.club, 500_000)]
        [InlineData(_difficulty.hard, _venue.concertHall, 3_000_000)]
        [InlineData(_difficulty.hard, _venue.stadium, 50_000_000)]
        [InlineData(_difficulty.normal, _venue.openAirStage, 10_000_000)]
        [InlineData(_difficulty.normal, _venue.stadium, 25_000_000)]
        [InlineData(_difficulty.normal, _venue.tokyoColiseum, 50_000_000)]
        [InlineData(_difficulty.easy, _venue.tokyoColiseum, 50_000_000)]
        public void OtherBaseCosts_AreTheGames(_difficulty difficulty, _venue venue, int cost)
        {
            ConcertGame.Reset(difficulty: difficulty);
            Assert.Equal(cost, SEvent_Concerts.GetVenueBaseCost(venue));
        }

        private const string OtherModId = "tests.ConcertRebalance.OtherMod";

        private static void OtherModCapacity(SEvent_Concerts._venue val, ref int __result)
        {
            if (val == _venue.tokyoColiseum)
                __result = 40_000;
        }

        private static void OtherModCost(SEvent_Concerts._venue val, ref int __result)
        {
            if (val == _venue.tokyoColiseum)
                __result = 150_000_000;
            else if (val == _venue.openAirStage)
                __result = 15_000_000;
        }

        private static void OtherModDoubles(ref int __result) => __result *= 2;

        /// <summary>
        /// A mod that sets its own figure before the mod's patch runs keeps it; one that scales after it scales the mod's.
        /// </summary>
        [Theory]
        [InlineData(nameof(SEvent_Concerts.GetVenueCapacity), nameof(OtherModCapacity), Priority.High, _venue.tokyoColiseum, 40_000)]
        [InlineData(nameof(SEvent_Concerts.GetVenueCapacity), nameof(OtherModDoubles), Priority.Low, _venue.tokyoColiseum, 100_000)]
        [InlineData(nameof(SEvent_Concerts.GetVenueBaseCost), nameof(OtherModCost), Priority.High, _venue.tokyoColiseum, 150_000_000)]
        [InlineData(nameof(SEvent_Concerts.GetVenueBaseCost), nameof(OtherModCost), Priority.High, _venue.openAirStage, 15_000_000)]
        [InlineData(nameof(SEvent_Concerts.GetVenueBaseCost), nameof(OtherModDoubles), Priority.Low, _venue.tokyoColiseum, 400_000_000)]
        [InlineData(nameof(SEvent_Concerts.GetVenueBaseCost), nameof(OtherModDoubles), Priority.Low, _venue.openAirStage, 40_000_000)]
        public void AnotherModsFigures_AreRespected(string gameMethod, string otherPatch, int priority, _venue venue, int expected)
        {
            ConcertGame.Reset(difficulty: _difficulty.hard);
            Harmony otherMod = new(OtherModId);
            try
            {
                otherMod.Patch(AccessTools.Method(typeof(SEvent_Concerts), gameMethod),
                    postfix: new HarmonyMethod(typeof(HardColiseumTests), otherPatch) { priority = priority });
                int actual = gameMethod == nameof(SEvent_Concerts.GetVenueCapacity)
                    ? SEvent_Concerts.GetVenueCapacity(venue)
                    : SEvent_Concerts.GetVenueBaseCost(venue);
                Assert.Equal(expected, actual);
            }
            finally
            {
                otherMod.UnpatchSelf();
            }
        }
    }
}
