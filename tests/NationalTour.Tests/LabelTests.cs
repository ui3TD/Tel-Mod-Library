using TMPro;
using UnityEngine;
using Xunit;

namespace NationalTour.Tests
{
    /// <summary>
    /// A national tour's event cards and staff status line say "National Tour" where the game says "World Tour".
    /// </summary>
    public class LabelTests
    {
        private readonly SEvent_Tour.tour national;
        private readonly SEvent_Tour.tour world;

        public LabelTests()
        {
            TestGame.Reset();
            TestGame.LoadCountries();
            new Scene();
            Language.Data["SEVENT__WORLD_TOUR"] = "World Tour";
            Language.Data["TOUR__COUNTRIES"] = "Countries";
            Language.Data[NationalTab.TITLE_KEY] = "National Tour (language file)";
            Language.Data[Labels.PREFECTURES_KEY] = "Prefectures (language file)";
            national = TestGame.Tour(Prefectures.All[0]);
            world = TestGame.Tour(SEvent_Tour.Countries[0]);
        }

        private static GameObject Label(string name, string constant, GameObject parent = null)
        {
            GameObject label = Seams.NewObject(name, parent);
            Seams.Add<Lang_Button>(label).Constant = constant;
            Seams.Add<TextMeshProUGUI>(label);
            return label;
        }

        private static SEvent_Button_Tour CardInProduction()
        {
            GameObject card = Seams.NewObject("Tour In Development");
            SEvent_Button_Tour button = Seams.Add<SEvent_Button_Tour>(card);
            button.Title = Label("Title", "SEVENT__WORLD_TOUR", Seams.NewObject("Container", card));
            return button;
        }

        // The finished card: Container/Title, and Container/Results/Countries with its Title label and Value
        private static SEvent_Button_Tour_Finished CardFinished()
        {
            GameObject card = Seams.NewObject("Tour Finished");
            SEvent_Button_Tour_Finished button = Seams.Add<SEvent_Button_Tour_Finished>(card);
            GameObject container = Seams.NewObject("Container", card);
            button.Title = Label("Title", "SEVENT__WORLD_TOUR", container);
            GameObject countries = Seams.NewObject("Countries", Seams.NewObject("Results", container));
            Label("Title", "TOUR__COUNTRIES", countries);
            button.Countries = Seams.NewObject("Value", countries);
            Seams.Add<TextMeshProUGUI>(button.Countries);
            return button;
        }

        private static GameObject CountriesLabel(SEvent_Button_Tour_Finished card) => Seams.Child(Seams.ParentOf(card.Countries), "Title");

        [Fact]
        public void NationalCardInProduction_SaysNationalTour()
        {
            SEvent_Button_Tour card = CardInProduction();

            Seams.CardInProduction(card, national);

            // The card's label reads its text again from this key when it starts and when the language changes
            Assert.Equal(NationalTab.TITLE_KEY, Seams.GetComponent<Lang_Button>(card.Title).Constant);
            Assert.Equal("National Tour (language file)", Seams.TextOf(card.Title));
        }

        [Fact]
        public void WorldCardInProduction_IsTheGames()
        {
            SEvent_Button_Tour card = CardInProduction();

            Seams.CardInProduction(card, world);

            Assert.Equal("SEVENT__WORLD_TOUR", Seams.GetComponent<Lang_Button>(card.Title).Constant);
            Assert.Null(Seams.TextOf(card.Title));
        }

        [Fact]
        public void NationalCardFinished_SaysNationalTourAndPrefectures()
        {
            SEvent_Button_Tour_Finished card = CardFinished();

            Seams.CardFinished(card, national);

            Assert.Equal("National Tour (language file)", Seams.TextOf(card.Title));
            Assert.Equal(Labels.PREFECTURES_KEY, Seams.GetComponent<Lang_Button>(CountriesLabel(card)).Constant);
            Assert.Equal("Prefectures (language file)", Seams.TextOf(CountriesLabel(card)));
        }

        [Fact]
        public void WorldCardFinished_IsTheGames()
        {
            SEvent_Button_Tour_Finished card = CardFinished();

            Seams.CardFinished(card, world);

            Assert.Equal("SEVENT__WORLD_TOUR", Seams.GetComponent<Lang_Button>(card.Title).Constant);
            Assert.Equal("TOUR__COUNTRIES", Seams.GetComponent<Lang_Button>(CountriesLabel(card)).Constant);
        }

        [Fact]
        public void WithoutTheModsText_CardsSayItInEnglish()
        {
            Language.Data.Remove(NationalTab.TITLE_KEY);
            Language.Data.Remove(Labels.PREFECTURES_KEY);
            SEvent_Button_Tour_Finished card = CardFinished();

            Seams.CardFinished(card, national);

            Assert.Equal("National Tour", Seams.TextOf(card.Title));
            Assert.Equal("Prefectures", Seams.TextOf(CountriesLabel(card)));
        }

        // The staff status line

        private static staff._staff StafferWorkingOn(SEvent_Tour.tour tour, agency._room._status status = agency._room._status.tourProduction)
        {
            agency._room room = Seams.Fake<agency._room>();
            room.tour = tour;
            room.status = status;
            staff._staff staffer = Seams.Fake<staff._staff>();
            staffer.room = room;
            return staffer;
        }

        private const string GameStatus = "Production for the <color=#6fd08c>World Tour</color>";

        [Fact]
        public void StaffOnANationalTour_SayNationalTour()
        {
            string status = GameStatus;
            staff_staff_GetStatusText.Postfix(StafferWorkingOn(national), ref status);
            Assert.Equal("Production for the <color=#6fd08c>National Tour (language file)</color>", status);
        }

        [Fact]
        public void StaffOnAWorldTour_AreTheGames()
        {
            string status = GameStatus;
            staff_staff_GetStatusText.Postfix(StafferWorkingOn(world), ref status);
            Assert.Equal(GameStatus, status);
        }

        [Fact]
        public void StaffDoingSomethingElse_AreTheGames()
        {
            string status = "Idle (World Tour)";
            staff_staff_GetStatusText.Postfix(StafferWorkingOn(national, agency._room._status.concertProduction), ref status);
            Assert.Equal("Idle (World Tour)", status);
        }

        [Fact]
        public void StaffWithoutARoom_AreTheGames()
        {
            staff._staff staffer = Seams.Fake<staff._staff>();
            string status = "Not assigned";
            staff_staff_GetStatusText.Postfix(staffer, ref status);
            Assert.Equal("Not assigned", status);
        }
    }
}
