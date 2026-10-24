using HarmonyLib;
using System;
using System.Linq;
using Xunit;

namespace NationalTour.Tests
{
    /// <summary>
    /// The national tour lives in a second, disabled SEvent_Tour next to the game's.
    /// </summary>
    public class HandlerTests
    {
        private readonly Scene scene;

        public HandlerTests()
        {
            TestGame.Reset();
            scene = new Scene();
        }

        private SEvent_Tour[] Handlers() => Seams.ComponentsOf<SEvent_Tour>(scene.Data);

        private static Delegate[] Subscribers(string saveEvent) =>
            ((Delegate)AccessTools.Field(typeof(SaveManager), saveEvent).GetValue(null))?.GetInvocationList() ?? new Delegate[0];

        [Fact]
        public void GameAwake_AddsADisabledNationalHandler_AfterTheGames()
        {
            Seams.AddHandler(scene.Handler);

            Assert.Equal(2, Handlers().Length);
            Assert.Same(scene.Handler, Handlers()[0]);
            Assert.Same(NationalHandler.Instance, Handlers()[1]);
            // Disabled, so its Start (which would load the countries again) never runs
            Assert.False(Seams.Enabled[NationalHandler.Instance]);
        }

        /// <summary>
        /// The national handler's own Awake runs the same postfix as it's added; it adds nothing more.
        /// </summary>
        [Fact]
        public void GameAwakeAgain_AddsNoMoreHandlers()
        {
            Seams.AddHandler(scene.Handler);
            SEvent_Tour national = NationalHandler.Instance;

            Seams.AddHandler(scene.Handler);

            Assert.Equal(2, Handlers().Length);
            Assert.Same(national, NationalHandler.Instance);
        }

        /// <summary>
        /// Another mod that already added a second handler keeps it; the mod doesn't add a third.
        /// </summary>
        [Fact]
        public void SceneWithASecondHandlerAlready_GetsNoNationalHandler()
        {
            Seams.Add<SEvent_Tour>(scene.Data);

            Seams.AddHandler(scene.Handler);

            Assert.Equal(2, Handlers().Length);
            Assert.Null(NationalHandler.Instance);
        }

        /// <summary>
        /// Only the game's handler saves and loads; the national handler's Awake subscribed it too.
        /// </summary>
        [Fact]
        public void NationalHandler_DoesntSaveOrLoad()
        {
            AccessTools.Method(typeof(SEvent_Tour), "Awake").Invoke(scene.Handler, null);

            Seams.AddHandler(scene.Handler);

            foreach (string saveEvent in new[] { "SaveEvent", "LoadEvent" })
            {
                Delegate subscriber = Assert.Single(Subscribers(saveEvent));
                Assert.Same(scene.Handler, subscriber.Target);
            }
        }

        [Fact]
        public void NationalHandler_OpensTheGamesPopups()
        {
            Seams.AddHandler(scene.Handler);
            AccessTools.FieldRef<SEvent_Tour, PopupManager> popups = AccessTools.FieldRefAccess<SEvent_Tour, PopupManager>("popupManager");
            popups(scene.Handler) = scene.Popups;
            scene.Handler.TourPopup = Seams.Fake<Tour_Popup>();

            Seams.CopyHandlerPopups(scene.Handler);

            Assert.Same(scene.Popups, popups(NationalHandler.Instance));
            Assert.Same(scene.Handler.TourPopup, NationalHandler.Instance.TourPopup);
        }

        [Fact]
        public void TourToPrefectures_BelongsToTheNationalHandler()
        {
            TestGame.LoadCountries();
            Seams.AddHandler(scene.Handler);

            Assert.Same(NationalHandler.Instance, NationalHandler.For(scene.Handler, TestGame.Tour(Prefectures.All[0])));
            Assert.Same(scene.Handler, NationalHandler.For(scene.Handler, TestGame.Tour(SEvent_Tour.Countries[0])));
            Assert.Same(scene.Handler, NationalHandler.For(scene.Handler, null));
        }

        [Fact]
        public void WithoutANationalHandler_EveryTourBelongsToTheGames()
        {
            TestGame.LoadCountries();

            Assert.Same(scene.Handler, NationalHandler.For(scene.Handler, TestGame.Tour(Prefectures.All[0])));
            Assert.False(Readiness.CanCreateNationalTours());
        }
    }
}
