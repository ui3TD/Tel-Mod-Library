using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static NationalTour.Prefectures;
using Country = SEvent_Tour._country;
using Status = SpecialEvents_Manager._specialEvent._status;
using TourStatus = SEvent_Tour.tour._status;

namespace NationalTour.Tests
{
    /// <summary>
    /// National tours are saved in a file that holds only base-game values, so it loads with or without the mod.
    /// The game's own SaveFunction and LoadFunction run here, with the mod's postfixes after them.
    /// </summary>
    public class SaveTests
    {
        private readonly Scene scene;
        private readonly SEvent_Tour national;
        private static readonly DateTime FirstFinish = new(2026, 2, 10);
        private static readonly DateTime SecondFinish = new(2026, 5, 1);

        public SaveTests()
        {
            TestGame.Reset();
            TestGame.LoadCountries();
            scene = new Scene();
            Seams.AddHandler(scene.Handler);
            national = NationalHandler.Instance;
            Seams.SaveData = new SaveManager.SavedData { variables__variables = variables.variable };
        }

        private static SEvent_Tour.country Prefecture(Prefectures.Prefecture prefecture) => Get(TypeOf(prefecture));

        /// <summary>
        /// A tour as the game keeps it, with each stop's venue level and attendance.
        /// </summary>
        private static SEvent_Tour.tour Tour(TourStatus status, DateTime finish, params (SEvent_Tour.country Country, int Level, int Attendance)[] stops)
        {
            SEvent_Tour.tour tour = new() { Status = status, FinishDate = finish };
            tour.Initiate();
            foreach (var stop in stops)
                tour.SelectedCountries.Add(new SEvent_Tour.tour.selectedCountry { Country = stop.Country, Level = stop.Level, Attendance = stop.Attendance });
            SEvent_Tour.Tours.Add(tour);
            return tour;
        }

        // The game during play: two finished national tours, a finished world tour, and one of each in production
        private void Play(bool worldInProduction = true)
        {
            Tour(TourStatus.finished, FirstFinish, (Prefecture(Prefectures.Prefecture.osaka), 1, 90), (Prefecture(Prefectures.Prefecture.kyoto), 1, 50));
            Tour(TourStatus.finished, FirstFinish.AddDays(20), (TestGame.Country(Country.france), 1, 90));
            Tour(TourStatus.finished, SecondFinish, (Prefecture(Prefectures.Prefecture.osaka), 2, 85), (Prefecture(Prefectures.Prefecture.hokkaido), 1, 95));
            national.Tour = Tour(TourStatus.normal, SecondFinish.AddDays(3), (Prefecture(Prefectures.Prefecture.saitama), 1, 0));
            if (worldInProduction)
                scene.Handler.Tour = Tour(TourStatus.normal, SecondFinish.AddDays(4), (TestGame.Country(Country.china), 1, 0));
            NationalRecord.Record.Cooldown_Start = SecondFinish;
            NationalRecord.Record.Cooldown_Stop = SecondFinish.AddMonths(3);
            NationalRecord.Record.Status = Status.production;
        }

        private void Save()
        {
            Seams.GameSaveFunction(scene.Handler);
            SaveState.OnSave(Seams.SaveData);
        }

        /// <summary>
        /// Quitting and loading the save again: everything the game keeps in memory starts over.
        /// </summary>
        private void NewSession()
        {
            SEvent_Tour.Tours.Clear();
            scene.Handler.Tour = null;
            national.Tour = new SEvent_Tour.tour();
            NationalRecord.Reset();
            Build();
            foreach (SEvent_Tour.country country in SEvent_Tour.Countries)
                country.Level = 1;
            variables.variable = new List<variables._variable>();
        }

        private void Load(bool withTheMod = true)
        {
            // The game's variables load first or later: either way the mod reads the save's own list
            variables.variable = Seams.SaveData.variables__variables;
            Seams.GameLoadFunction(scene.Handler);
            if (withTheMod)
                SaveState.OnLoad(scene.Handler, Seams.SaveData);
        }

        private SEvent_Tour.TourData SavedTour(int id) => Seams.SaveData.SEvent_Tour__Tours.Single(t => t.ID == id);

        private static string Entry() => variables.variable.SingleOrDefault(v => v.name == SaveState.VARIABLE)?.value;

        // Saving

        [Fact]
        public void SavedStops_AreTheirPairedCountries()
        {
            Play();

            Save();

            Assert.Equal(new[] { Country.france, Country.russia }, SavedTour(1).SelectedCountries.Select(s => s.Country));
            Assert.Equal(new[] { Country.france, Country.canada }, SavedTour(3).SelectedCountries.Select(s => s.Country));
            Assert.Equal(new[] { Country.china }, SavedTour(4).SelectedCountries.Select(s => s.Country));
            Assert.All(Seams.SaveData.SEvent_Tour__Tours.SelectMany(t => t.SelectedCountries), s => Assert.True(Enum.IsDefined(typeof(Country), s.Country)));
        }

        /// <summary>
        /// The live tours keep their prefectures: only the saved copies change.
        /// </summary>
        [Fact]
        public void Saving_LeavesTheToursInPlay()
        {
            Play();

            Save();

            Assert.Same(Prefecture(Prefectures.Prefecture.osaka), SEvent_Tour.GetTourByID(1).SelectedCountries[0].Country);
        }

        [Fact]
        public void Entry_ListsTheNationalTours_TheCurrentOne_AndTheCooldown()
        {
            Play();

            Save();

            Assert.Equal("tours=1,3,4;current=4;cooldown=2026-05-01 00:00:00|2026-08-01 00:00:00", Entry());
        }

        [Fact]
        public void CurrentTourField_KeepsTheWorldTour_WhenBothAreInProduction()
        {
            Play();

            Save();

            Assert.Equal(5, Seams.SaveData.SEvent_Tour__Tour);
        }

        /// <summary>
        /// Without the mod, the national tour in production is then the world tour in production.
        /// </summary>
        [Fact]
        public void CurrentTourField_GetsTheNationalTour_WhenNoWorldTourIsInProduction()
        {
            Play(worldInProduction: false);

            Save();

            Assert.Equal(4, Seams.SaveData.SEvent_Tour__Tour);
        }

        [Fact]
        public void SaveWithoutNationalTours_HasNoEntry()
        {
            Tour(TourStatus.finished, FirstFinish, (TestGame.Country(Country.france), 1, 90));

            Save();

            Assert.Null(Entry());
        }

        /// <summary>
        /// When the last national tour is gone (cancelled before it was launched) and the cooldown has never run,
        /// the entry is deleted.
        /// </summary>
        [Fact]
        public void EntryFromAnEarlierSave_IsDeletedWhenEmpty()
        {
            variables.variable.Add(new variables._variable { name = SaveState.VARIABLE, value = "tours=9" });

            Save();

            Assert.Null(Entry());
        }

        [Fact]
        public void NationalCooldownAlone_IsSaved()
        {
            NationalRecord.Record.Cooldown_Start = SecondFinish;
            NationalRecord.Record.Cooldown_Stop = SecondFinish.AddMonths(3);

            Save();

            Assert.Equal("tours=;cooldown=2026-05-01 00:00:00|2026-08-01 00:00:00", Entry());
        }

        // Loading

        [Fact]
        public void RoundTrip_PutsTheStopsBackOnThePrefectures()
        {
            Play();
            Save();
            NewSession();

            Load();

            Assert.Equal(new[] { Prefecture(Prefectures.Prefecture.osaka), Prefecture(Prefectures.Prefecture.kyoto) }, SEvent_Tour.GetTourByID(1).SelectedCountries.Select(s => s.Country));
            Assert.Equal(new[] { TestGame.Country(Country.france) }, SEvent_Tour.GetTourByID(2).SelectedCountries.Select(s => s.Country));
            Assert.Equal(new[] { Prefecture(Prefectures.Prefecture.saitama) }, SEvent_Tour.GetTourByID(4).SelectedCountries.Select(s => s.Country));
        }

        [Fact]
        public void RoundTrip_PutsEachTourInItsOwnSlot()
        {
            Play();
            Save();
            NewSession();

            Load();

            Assert.Equal(4, national.Tour.ID);
            Assert.Equal(5, scene.Handler.Tour.ID);
        }

        [Fact]
        public void RoundTrip_NationalTourAlone_MovesOutOfTheWorldSlot()
        {
            Play(worldInProduction: false);
            Save();
            NewSession();

            Load();

            Assert.Equal(4, national.Tour.ID);
            Assert.Null(scene.Handler.Tour);
        }

        [Fact]
        public void RoundTrip_RestoresTheNationalCooldown()
        {
            Play();
            Save();
            NewSession();
            staticVars.dateTime = SecondFinish.AddDays(10);

            Load();

            Assert.Equal(SecondFinish, NationalRecord.Record.Cooldown_Start);
            Assert.Equal(SecondFinish.AddMonths(3), NationalRecord.Record.Cooldown_Stop);
            Assert.True(NationalRecord.Record.IsCooldown());
            Assert.Equal(Status.production, NationalRecord.Record.Status);
        }

        /// <summary>
        /// The results popup levels up a stop at its prefecture's top venue with at least 80% attendance, counts
        /// the concert and dates it. Osaka: 90% at level 1 (top) goes to 2, then 85% at level 2 goes to 3. Kyoto:
        /// 50% stays at 1. Hokkaido: 95% at level 1 goes to 2. The tour in production has played nowhere yet.
        /// </summary>
        [Fact]
        public void RoundTrip_ReplaysVenueLevelsConcertsAndDates()
        {
            Play();
            Save();
            NewSession();

            Load();

            void Expect(Prefectures.Prefecture prefecture, int level, int concerts, DateTime last)
            {
                SEvent_Tour.country country = Prefecture(prefecture);
                Assert.Equal((level, concerts, last), (country.Level, country.ConcertCount, country.LastConcertDate));
            }
            Expect(Prefectures.Prefecture.osaka, 3, 2, SecondFinish);
            Expect(Prefectures.Prefecture.kyoto, 1, 1, FirstFinish);
            Expect(Prefectures.Prefecture.hokkaido, 2, 1, SecondFinish);
            Expect(Prefectures.Prefecture.saitama, 1, 0, default);
        }

        /// <summary>
        /// The world tour's countries keep their own saved levels; national tours don't touch them.
        /// </summary>
        [Fact]
        public void RoundTrip_LeavesTheWorldTourCountriesAsTheGameSavedThem()
        {
            Play();
            TestGame.Country(Country.france).Level = 2;
            TestGame.Country(Country.france).ConcertCount = 1;
            Save();
            NewSession();

            Load();

            Assert.Equal(2, TestGame.Country(Country.france).Level);
            Assert.Equal(1, TestGame.Country(Country.france).ConcertCount);
        }

        /// <summary>
        /// A save made without national tours (or before the mod was installed) loads as the base game, even
        /// right after a save that had them.
        /// </summary>
        [Fact]
        public void SaveWithoutTheEntry_LoadsAsTheBaseGame()
        {
            Tour(TourStatus.normal, FirstFinish, (TestGame.Country(Country.china), 1, 0));
            scene.Handler.Tour = SEvent_Tour.Tours[0];
            Save();
            Prefecture(Prefectures.Prefecture.osaka).Level = 4;
            national.Tour = new SEvent_Tour.tour();
            NationalRecord.Record.Cooldown_Stop = SecondFinish;

            Load();

            Assert.Null(national.Tour);
            Assert.False(NationalRecord.Record.IsCooldown());
            Assert.Equal(1, Prefecture(Prefectures.Prefecture.osaka).Level);
            Assert.Same(SEvent_Tour.Tours[0], scene.Handler.Tour);
        }

        /// <summary>
        /// Without the mod, the game's own LoadFunction reads the file: every stop is a country it knows, and the
        /// national tour in production is the world tour in production.
        /// </summary>
        [Fact]
        public void WithoutTheMod_TheSaveLoadsAsWorldTours()
        {
            Play(worldInProduction: false);
            Save();
            NewSession();

            Load(withTheMod: false);

            Assert.All(SEvent_Tour.Tours.SelectMany(t => t.SelectedCountries), s => Assert.NotNull(s.Country));
            Assert.Equal(new[] { TestGame.Country(Country.france), TestGame.Country(Country.russia) }, SEvent_Tour.GetTourByID(1).SelectedCountries.Select(s => s.Country));
            Assert.Equal(4, scene.Handler.Tour.ID);
        }

        // The entry's format

        [Fact]
        public void Entry_ReadsBackWhatWasWritten()
        {
            SaveState.Entry entry = new() { Tours = { 3, 12 }, Current = 12, CooldownStart = FirstFinish, CooldownStop = SecondFinish };

            SaveState.Entry read = SaveState.Parse(SaveState.Format(entry));

            Assert.Equal(entry.Tours, read.Tours);
            Assert.Equal(12, read.Current);
            Assert.Equal(FirstFinish, read.CooldownStart);
            Assert.Equal(SecondFinish, read.CooldownStop);
        }

        [Theory]
        [InlineData("")]
        [InlineData("nonsense")]
        [InlineData("tours=a,,3;current=x;cooldown=2026|nope;other=1")]
        public void BrokenEntry_ReadsWhatItCan(string value)
        {
            SaveState.Entry read = SaveState.Parse(value);

            Assert.Equal(value.Contains("3") ? new[] { 3 } : new int[0], read.Tours);
            Assert.Equal(-1, read.Current);
            Assert.Equal(default, read.CooldownStop);
        }

        [Fact]
        public void EntryForAToursGone_IsSkipped()
        {
            variables.variable.Add(new variables._variable { name = SaveState.VARIABLE, value = "tours=40;current=40" });
            Seams.SaveData.variables__variables = variables.variable;
            Save();
            variables.variable.Add(new variables._variable { name = SaveState.VARIABLE, value = "tours=40;current=40" });

            Exception error = Record.Exception(() => Load());

            Assert.Null(error);
            Assert.Null(national.Tour);
        }
    }
}
