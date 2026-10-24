using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Xunit;

namespace NationalTour.Tests
{
    /// <summary>
    /// The game's buttons reach the tour's own handler: the new tour popup's Continue, dropping a card on a room,
    /// the card's Launch and Cancel, and the results' Finish. The game's methods run with the mod's transpilers.
    /// </summary>
    public class RoutingTests
    {
        private readonly Scene scene;
        private readonly SEvent_Tour.tour national;
        private readonly SEvent_Tour.tour world;

        public RoutingTests()
        {
            TestGame.Reset();
            TestGame.LoadCountries();
            scene = new Scene();
            Seams.AddHandler(scene.Handler);
            national = TestGame.Tour(Prefectures.All[0], Prefectures.All[1]);
            world = TestGame.Tour(SEvent_Tour.Countries[0]);
        }

        private SEvent_Tour HandlerFor(bool isNational) => isNational ? NationalHandler.Instance : scene.Handler;

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Continue_SetsTheTourOnItsOwnHandler(bool isNational)
        {
            scene.NewTour.Tour = isNational ? national : world;

            Seams.GameOnContinue(scene.NewTour);

            Assert.Same(HandlerFor(isNational), Assert.Single(Seams.CallsTo(nameof(SEvent_Tour.SetTour))));
            Assert.Single(Seams.CallsTo(nameof(PopupManager.Close)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DroppingACardOnARoom_AssignsItOnItsOwnHandler(bool isNational)
        {
            DragAndDropManager drag = Seams.Fake<DragAndDropManager>();
            AccessTools.FieldRefAccess<DragAndDropManager, agency._room>("hovering_room")(drag) = Seams.Fake<agency._room>();

            Seams.GameDropTour(drag, isNational ? national : world);

            Assert.Same(HandlerFor(isNational), Assert.Single(Seams.CallsTo(nameof(SEvent_Tour.Assign))));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Finish_FinishesOnTheHandlerThatLaunched(bool isNational)
        {
            Tour_Popup results = Seams.Fake<Tour_Popup>();
            results.BG_Camera = Seams.NewObject("BG_Camera");
            results.BG_Camera_Container = Seams.NewObject("BG_Camera_Container");
            SEvent_Tour_StartTour_Routing.Prefix(HandlerFor(isNational));

            Seams.GameOnFinish(results);

            Assert.Same(HandlerFor(isNational), Assert.Single(Seams.CallsTo(nameof(SEvent_Tour.FinishTour))));
        }

        [Fact]
        public void Finish_WithNothingLaunched_IsTheGames()
        {
            Tour_Popup results = Seams.Fake<Tour_Popup>();
            results.BG_Camera = Seams.NewObject("BG_Camera");
            results.BG_Camera_Container = Seams.NewObject("BG_Camera_Container");

            Seams.GameOnFinish(results);

            Assert.Same(scene.Handler, Assert.Single(Seams.CallsTo(nameof(SEvent_Tour.FinishTour))));
        }

        private static readonly AccessTools.FieldRef<SEvent_Button_Tour, SEvent_Tour> CardHandler =
            AccessTools.FieldRefAccess<SEvent_Button_Tour, SEvent_Tour>("_Sevent_Tour");

        /// <summary>
        /// A card's Launch and Cancel call its handler's StartTour and CancelTour, which act on that handler's tour.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Card_GetsItsTourHandler(bool isNational)
        {
            SEvent_Button_Tour card = Seams.Fake<SEvent_Button_Tour>();
            card.Tour = isNational ? national : world;
            CardHandler(card) = scene.Handler;

            SEvent_Button_Tour_Start.Postfix(card);

            Assert.Same(HandlerFor(isNational), CardHandler(card));
        }

        [Fact]
        public void ConcertCard_KeepsTheGamesHandler()
        {
            SEvent_Button_Tour card = Seams.Fake<SEvent_Button_Tour>();
            CardHandler(card) = scene.Handler;

            SEvent_Button_Tour_Start.Postfix(card);

            Assert.Same(scene.Handler, CardHandler(card));
        }

        /// <summary>
        /// If another mod's transpiler or a different game version moved the call the mod anchors on, the method
        /// stays as it is, and national tours can't be created (the map's Continue stays greyed).
        /// </summary>
        [Fact]
        public void AnchorNotFound_LeavesTheMethodAlone_AndTurnsNationalToursOff()
        {
            List<CodeInstruction> method = new() { new CodeInstruction(OpCodes.Nop), new CodeInstruction(OpCodes.Ret) };
            using LogRecorder log = new();

            List<CodeInstruction> result = Tour_New_Popup_OnContinue.Transpiler(method).ToList();

            Assert.Equal(method, result);
            Assert.False(Readiness.IsReady(Readiness.Module.Routing));
            Assert.False(Readiness.CanCreateNationalTours());
            Assert.Single(log.Errors);
        }

        [Fact]
        public void AnchorFoundTwice_LeavesTheMethodAlone()
        {
            CodeInstruction call = new(OpCodes.Callvirt, AccessTools.Method(typeof(UnityEngine.GameObject), nameof(UnityEngine.GameObject.GetComponent), new System.Type[0], new[] { typeof(SEvent_Tour) }));
            List<CodeInstruction> method = new() { call, new CodeInstruction(OpCodes.Pop), call.Clone(), new CodeInstruction(OpCodes.Ret) };
            using LogRecorder log = new();

            Assert.Equal(method, Tour_Popup_OnFinish_Routing.Transpiler(method).ToList());
            Assert.False(Readiness.IsReady(Readiness.Module.Routing));
        }
    }
}
