using System;
using Xunit;
using EventType = SpecialEvents_Manager._type;
using SpecialEvent = SpecialEvents_Manager._specialEvent;
using Status = SpecialEvents_Manager._specialEvent._status;
using TourStatus = SEvent_Tour.tour._status;

namespace NationalTour.Tests
{
    /// <summary>
    /// The national tour has its own cooldown and "in production" record, apart from the World Tour's.
    /// </summary>
    public class NationalRecordTests
    {
        private readonly SpecialEvent world = new() { Type = EventType.WorldTour };

        public NationalRecordTests()
        {
            TestGame.Reset();
            TestGame.LoadCountries();
        }

        private static SEvent_Tour.tour AddTour(bool national, TourStatus status)
        {
            SEvent_Tour.country stop = national ? Prefectures.All[0] : SEvent_Tour.Countries[0];
            SEvent_Tour.tour tour = TestGame.Tour(stop);
            tour.Status = status;
            SEvent_Tour.Tours.Add(tour);
            return tour;
        }

        private static void Update(SpecialEvent record)
        {
            record.UpdateStatus();
            SpecialEvents_Manager_specialEvent_UpdateStatus.Postfix(record);
        }

        [Fact]
        public void TheNationalTab_FindsTheNationalRecord()
        {
            SpecialEvent result = null;
            SpecialEvents_Manager_GetEvent.Postfix(NationalRecord.Tab, ref result);
            Assert.Same(NationalRecord.Record, result);
        }

        [Theory]
        [InlineData(EventType.WorldTour)]
        [InlineData((EventType)999)]
        public void OtherTabs_KeepTheGamesResult(EventType tab)
        {
            SpecialEvent game = tab == EventType.WorldTour ? world : null;
            SpecialEvent result = game;

            SpecialEvents_Manager_GetEvent.Postfix(tab, ref result);

            Assert.Same(game, result);
        }

        [Fact]
        public void AnotherModsRecordForTheTab_IsKept()
        {
            SpecialEvent anotherMods = new();
            SpecialEvent result = anotherMods;
            SpecialEvents_Manager_GetEvent.Postfix(NationalRecord.Tab, ref result);
            Assert.Same(anotherMods, result);
        }

        [Fact]
        public void NationalTabNumber_IsNotTheGames()
        {
            Assert.False(Enum.IsDefined(typeof(EventType), NationalRecord.Tab));
        }

        [Theory]
        [InlineData(false, false, Status.normal, Status.normal)]
        [InlineData(true, false, Status.production, Status.normal)]
        [InlineData(false, true, Status.normal, Status.production)]
        [InlineData(true, true, Status.production, Status.production)]
        public void EachRecord_IsInProductionOnlyForItsOwnTours(bool worldUnfinished, bool nationalUnfinished, Status worldStatus, Status nationalStatus)
        {
            // A finished tour of each kind is never counted
            AddTour(national: false, TourStatus.finished);
            AddTour(national: true, TourStatus.finished);
            if (worldUnfinished)
                AddTour(national: false, TourStatus.working);
            if (nationalUnfinished)
                AddTour(national: true, TourStatus.normal);

            Update(world);
            Update(NationalRecord.Record);

            Assert.Equal(worldStatus, world.Status);
            Assert.Equal(nationalStatus, NationalRecord.Record.Status);
        }

        [Theory]
        [InlineData(EventType.Concert)]
        [InlineData(EventType.SSK)]
        public void ConcertsAndElections_KeepTheGamesStatus(EventType type)
        {
            SpecialEvent record = new() { Type = type, Status = Status.production };
            AddTour(national: false, TourStatus.normal);

            SpecialEvents_Manager_specialEvent_UpdateStatus.Postfix(record);

            Assert.Equal(Status.production, record.Status);
        }

        [Fact]
        public void NationalCooldown_IsThreeMonths_AndSeparate()
        {
            staticVars.dateTime = new DateTime(2026, 5, 1);

            NationalRecord.Record.SetCooldown();

            Assert.Equal(new DateTime(2026, 8, 1), NationalRecord.Record.Cooldown_Stop);
            Assert.True(NationalRecord.Record.IsCooldown());
            Assert.False(world.IsCooldown());
        }

        [Fact]
        public void NewGameOrSave_StartsWithoutANationalCooldown()
        {
            staticVars.dateTime = new DateTime(2026, 5, 1);
            NationalRecord.Record.SetCooldown();
            NationalRecord.Record.Status = Status.production;

            SpecialEvents_Manager_Start.Postfix();

            Assert.False(NationalRecord.Record.IsCooldown());
            Assert.Equal(Status.normal, NationalRecord.Record.Status);
        }
    }
}
