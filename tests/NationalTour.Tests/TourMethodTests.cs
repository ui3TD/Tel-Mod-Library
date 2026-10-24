using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Xunit;
using SpecialEvent = SpecialEvents_Manager._specialEvent;
using Status = SpecialEvents_Manager._specialEvent._status;

namespace NationalTour.Tests
{
    /// <summary>
    /// The game's SetTour, StartTour, FinishTour and CancelTour, run with the mod's transpiler: on the national
    /// handler they use the national record and skip the World Tour's achievement and story; on the game's
    /// handler they are the game's.
    /// </summary>
    public class TourMethodTests
    {
        private readonly Scene scene;
        private readonly SEvent_Tour national;
        private readonly SpecialEvent worldRecord = new() { Type = SpecialEvents_Manager._type.WorldTour };

        public TourMethodTests()
        {
            TestGame.Reset();
            TestGame.LoadCountries();
            scene = new Scene();
            Seams.AddHandler(scene.Handler);
            national = NationalHandler.Instance;
            SpecialEvents_Manager.SpecialEvents.Add(worldRecord);
            staticVars.dateTime = new DateTime(2026, 5, 1);
        }

        private SEvent_Tour.tour NationalTour() => TestGame.Tour(Prefectures.All[0], Prefectures.All[13]);

        private SEvent_Tour.tour WorldTour() => TestGame.Tour(SEvent_Tour.Countries[0]);

        // SetTour

        [Fact]
        public void SetTour_National_GoesInTheNationalSlotAndRecord()
        {
            SEvent_Tour.tour tour = NationalTour();

            Seams.GameSetTour(national, tour);

            Assert.Same(tour, national.Tour);
            Assert.Null(scene.Handler.Tour);
            Assert.Contains(tour, SEvent_Tour.Tours);
            Assert.Equal(Status.production, NationalRecord.Record.Status);
            Assert.Equal(Status.normal, worldRecord.Status);
            Assert.Single(Seams.CallsTo(nameof(SpecialEvents_Manager.RenderTab)));
        }

        [Fact]
        public void SetTour_World_IsTheGames()
        {
            SEvent_Tour.tour tour = WorldTour();

            Seams.GameSetTour(scene.Handler, tour);

            Assert.Same(tour, scene.Handler.Tour);
            Assert.Equal(Status.production, worldRecord.Status);
            Assert.Equal(Status.normal, NationalRecord.Record.Status);
        }

        /// <summary>
        /// Both kinds share the game's tour list and ID counter, so rooms find either tour by ID.
        /// </summary>
        [Fact]
        public void BothTours_InProductionAtOnce_WithTheirOwnIDs()
        {
            SEvent_Tour.tour world = WorldTour();
            SEvent_Tour.tour nationalTour = NationalTour();

            Seams.GameSetTour(scene.Handler, world);
            Seams.GameSetTour(national, nationalTour);

            Assert.Same(world, scene.Handler.Tour);
            Assert.Same(nationalTour, national.Tour);
            Assert.Equal(new[] { 1, 2 }, SEvent_Tour.Tours.Select(t => t.ID));
            Assert.Equal(Status.production, worldRecord.Status);
            Assert.Equal(Status.production, NationalRecord.Record.Status);
        }

        // StartTour

        [Fact]
        public void StartTour_National_StartsTheNationalCooldownOnly_AndNoAchievement()
        {
            national.Tour = NationalTour();

            Seams.GameStartTour(national);

            Assert.Equal(new DateTime(2026, 8, 1), NationalRecord.Record.Cooldown_Stop);
            Assert.False(worldRecord.IsCooldown());
            Assert.Empty(Seams.CallsTo(nameof(Achievements.Unlock)));
            Assert.Equal(PopupManager._type.sevent_tour, Assert.Single(Seams.CallsTo(nameof(PopupManager.Open))));
            Assert.Same(national.Tour, Assert.Single(Seams.CallsTo("ResultsSetTour")));
        }

        [Fact]
        public void StartTour_World_IsTheGames()
        {
            scene.Handler.Tour = WorldTour();

            Seams.GameStartTour(scene.Handler);

            Assert.Equal(new DateTime(2026, 8, 1), worldRecord.Cooldown_Stop);
            Assert.False(NationalRecord.Record.IsCooldown());
            Assert.Equal(Achievements.ID.ACH_WORLD_TOUR, Assert.Single(Seams.CallsTo(nameof(Achievements.Unlock))));
        }

        // FinishTour

        [Fact]
        public void FinishTour_National_PaysOut_WithoutTheWorldTourStory()
        {
            SEvent_Tour.tour tour = NationalTour();
            tour.Revenue = 5_000_000;
            tour.ProductionCost = 2_000_000;
            national.Tour = tour;
            NationalRecord.Record.Status = Status.production;
            worldRecord.Status = Status.production;

            Seams.GameFinishTour(national);

            Assert.Equal(SEvent_Tour.tour._status.finished, tour.Status);
            Assert.Null(national.Tour);
            Assert.Equal(Status.normal, NationalRecord.Record.Status);
            Assert.Equal(Status.production, worldRecord.Status);
            Assert.Equal(3_000_000L, Assert.Single(Seams.CallsTo(nameof(resources.AddMoney))));
            Assert.Same(national, Assert.Single(Seams.CallsTo(nameof(SEvent_Tour.UseStamina))));
            Assert.Empty(Seams.CallsTo(nameof(tasks.OnWorldTour)));
        }

        [Fact]
        public void FinishTour_World_IsTheGames()
        {
            SEvent_Tour.tour tour = WorldTour();
            scene.Handler.Tour = tour;

            Seams.GameFinishTour(scene.Handler);

            Assert.Same(tour, Assert.Single(Seams.CallsTo(nameof(tasks.OnWorldTour))));
            Assert.Equal(Status.normal, worldRecord.Status);
        }

        // CancelTour

        [Fact]
        public void CancelTour_National_CancelsOnlyItsOwnJobAndRecord()
        {
            SEvent_Tour.tour world = WorldTour();
            SEvent_Tour.tour nationalTour = NationalTour();
            Seams.GameSetTour(scene.Handler, world);
            Seams.GameSetTour(national, nationalTour);
            agency._room worldRoom = Seams.Fake<agency._room>();
            worldRoom.tour = world;
            agency._room nationalRoom = Seams.Fake<agency._room>();
            nationalRoom.tour = nationalTour;
            Seams.Rooms.AddRange(new[] { worldRoom, nationalRoom });

            Seams.GameCancelTour(national);

            Assert.Same(nationalRoom, Assert.Single(Seams.CallsTo(nameof(agency._room.CancelJob))));
            Assert.Equal(new[] { world }, SEvent_Tour.Tours);
            Assert.Null(national.Tour);
            Assert.Same(world, scene.Handler.Tour);
            Assert.Equal(Status.normal, NationalRecord.Record.Status);
            Assert.Equal(Status.production, worldRecord.Status);
        }

        // Fail soft

        [Fact]
        public void AnchorNotFound_LeavesTheMethodAlone_AndTurnsNationalToursOff()
        {
            List<CodeInstruction> method = new() { new CodeInstruction(OpCodes.Ret) };
            using LogRecorder log = new();

            Assert.Equal(method, SEvent_Tour_FinishTour.Transpiler(method).ToList());
            Assert.False(Readiness.IsReady(Readiness.Module.TourMethods));
            Assert.False(Readiness.CanCreateNationalTours());
        }

        // The world tour count

        [Fact]
        public void WorldTourCount_CountsFinishedWorldToursOnly()
        {
            foreach (SEvent_Tour.tour tour in new[] { WorldTour(), WorldTour(), NationalTour(), NationalTour(), NationalTour() })
            {
                tour.Status = SEvent_Tour.tour._status.finished;
                SEvent_Tour.Tours.Add(tour);
            }
            SEvent_Tour.tour unfinished = WorldTour();
            SEvent_Tour.Tours.Add(unfinished);
            int count = SEvent_Tour.GetWorldTourCount();

            SEvent_Tour_GetWorldTourCount.Postfix(ref count);

            Assert.Equal(2, count);
        }
    }
}
