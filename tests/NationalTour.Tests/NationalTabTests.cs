using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Xunit;

namespace NationalTour.Tests
{
    /// <summary>
    /// The Special Events popup gets a fourth tab, National Tour: a copy of the World Tour tab and its panel.
    /// </summary>
    public class NationalTabTests
    {
        private readonly Scene scene;

        public NationalTabTests()
        {
            TestGame.Reset();
            TestGame.LoadImages();
            TestGame.LoadCountries();
            Language.Data[NationalTab.TITLE_KEY] = "National Tour (language file)";
            Language.Data[NationalTab.DESC_KEY] = "Select the prefectures (language file)";
            scene = new Scene();
        }

        private GameObject NationalTabObject() => Seams.Child(scene.Types, NationalTab.NAME);

        private GameObject NationalPanel() => Seams.Child(scene.Container, NationalTab.NAME);

        [Fact]
        public void Tab_IsAddedAfterTheWorldTourTab()
        {
            Seams.BuildTab(scene.SpecialEvents);

            Assert.Equal(new[] { "Concert", "Election", "World Tour", NationalTab.NAME }, Seams.ChildrenOf(scene.Types).Select(Seams.NameOf));
            Assert.Same(NationalTab.Tab, NationalTabObject());
        }

        /// <summary>
        /// The panel sits with the other panels, before the tab bar, which is drawn on top of them.
        /// </summary>
        [Fact]
        public void Panel_IsAddedAfterTheWorldTourPanel()
        {
            Seams.BuildTab(scene.SpecialEvents);

            Assert.Equal(new[] { "Concert", "Election", "World Tour", NationalTab.NAME, "Types" }, Seams.ChildrenOf(scene.Container).Select(Seams.NameOf));
            Assert.Same(NationalTab.Panel, Seams.GetComponent<TabButton>(NationalTabObject()).TabToOpen);
        }

        [Fact]
        public void Tab_IsBuiltOnce()
        {
            Seams.BuildTab(scene.SpecialEvents);
            Seams.BuildTab(scene.SpecialEvents);

            Assert.Equal(4, Seams.ChildrenOf(scene.Types).Length);
            Assert.Equal(5, Seams.ChildrenOf(scene.Container).Length);
        }

        [Fact]
        public void TabAndPanel_ShowTheNationalTourText()
        {
            Seams.BuildTab(scene.SpecialEvents);

            Lang_Button title = Seams.GetComponent<Lang_Button>(Seams.Child(NationalTabObject(), "Text"));
            Lang_Button description = Seams.GetComponent<Lang_Button>(Seams.Child(Seams.Child(NationalPanel(), "Panel"), "Text"));
            // The game reads the text again from these when the language changes
            Assert.Equal(NationalTab.TITLE_KEY, title.Constant);
            Assert.Equal(NationalTab.DESC_KEY, description.Constant);
            Assert.Equal("National Tour (language file)", Seams.TextOf(title));
            Assert.Equal("Select the prefectures (language file)", Seams.TextOf(description));
        }

        [Fact]
        public void WithoutTheModsText_TheTabSaysNationalTourInEnglish()
        {
            Language.Data.Remove(NationalTab.TITLE_KEY);
            Language.Data.Remove(NationalTab.DESC_KEY);

            Seams.BuildTab(scene.SpecialEvents);

            Assert.Equal(NationalTab.TITLE_FALLBACK, Seams.TextOf(Seams.Child(NationalTabObject(), "Text")));
            Assert.Equal(NationalTab.DESC_FALLBACK, Seams.TextOf(Seams.Child(Seams.Child(NationalPanel(), "Panel"), "Text")));
        }

        [Fact]
        public void NationalPanel_ShowsTheNationalTourPicture_WorldTourKeepsTheGames()
        {
            Seams.BuildTab(scene.SpecialEvents);

            Assert.Same(Pictures.World_tour_def, Seams.GetComponent<Image>(Seams.Child(NationalPanel(), "BG")).sprite);
            Assert.Same(scene.GameTourPicture, scene.WorldTourPicture.sprite);
            Assert.Equal("SEVENT__WORLD_TOUR_DESC", Seams.GetComponent<Lang_Button>(Seams.Child(Seams.Child(scene.WorldTourPanel, "Panel"), "Text")).Constant);
        }

        [Fact]
        public void NewTab_StartsHidden_AndEveryTabRedrawsItsEnds()
        {
            Seams.BuildTab(scene.SpecialEvents);

            TabButton national = Seams.GetComponent<TabButton>(NationalTabObject());
            Assert.Equal(1, Seams.CountCalls(nameof(TabButton.HideTab), national));
            Assert.All(Seams.ChildrenOf(scene.Types).Select(Seams.GetComponent<TabButton>),
                tab => Assert.Equal(1, Seams.CountCalls(nameof(NationalTab.CallRenderTab), tab)));
        }

        /// <summary>
        /// The copy's own listeners would open the World Tour tab; its only listener is the national one.
        /// </summary>
        [Fact]
        public void TabClick_HasOnlyTheNationalListener()
        {
            Seams.BuildTab(scene.SpecialEvents);

            UnityAction listener = Assert.Single(Seams.OnClick(Seams.GetComponent<Button>(NationalTabObject())));
            Assert.Equal(nameof(NationalTab.OnTabClicked), listener.Method.Name);
            Assert.Single(Seams.OnClick(Seams.GetComponent<Button>(scene.WorldTourTab)));
        }

        [Fact]
        public void TabClick_ShowsThePanel_ThenTheNationalContinueButton()
        {
            scene.SpecialEvents.OpenTab = SpecialEvents_Manager._type.WorldTour;
            Seams.BuildTab(scene.SpecialEvents);
            Seams.Calls.Clear();

            Seams.ClickTab();

            Assert.Equal(NationalRecord.Tab, scene.SpecialEvents.OpenTab);
            Assert.True(Seams.IsActive(NationalPanel()));
            Assert.False(Seams.IsActive(scene.WorldTourPanel));
            Assert.Equal(new[] { nameof(TabButton.OnClick), nameof(NationalTab.CallRenderContinueButton) }, Seams.Calls.Select(c => c.Method));
            Assert.Same(scene.SpecialEvents, Seams.Calls[1].Target);
        }

        [Fact]
        public void WithoutTheMapPicture_NoTab()
        {
            Pictures.TOUR_map_2 = null;

            Seams.BuildTab(scene.SpecialEvents);

            Assert.Equal(3, Seams.ChildrenOf(scene.Types).Length);
        }

        [Fact]
        public void WithoutPrefectures_NoTab()
        {
            Prefectures.All.Clear();

            Seams.BuildTab(scene.SpecialEvents);

            Assert.Equal(3, Seams.ChildrenOf(scene.Types).Length);
        }

        /// <summary>
        /// If another mod has changed the popup so the World Tour tab isn't there, the tab is skipped and logged.
        /// </summary>
        [Fact]
        public void WithoutAWorldTourTabToCopy_NoTab_AndLogged()
        {
            Seams.Rename(scene.WorldTourTab, "Something else");
            using LogRecorder log = new();

            Seams.BuildTab(scene.SpecialEvents);
            Seams.BuildTab(scene.SpecialEvents);

            Assert.Equal(3, Seams.ChildrenOf(scene.Types).Length);
            Assert.False(Readiness.IsReady(Readiness.Module.NationalTab));
            Assert.Single(log.Errors);
        }

        // Continue on the tab

        [Fact]
        public void Continue_OnTheNationalTab_ResetsTheMapInNationalMode()
        {
            scene.SpecialEvents.OpenTab = NationalRecord.Tab;

            Seams.SpecialEventsContinue(scene.SpecialEvents);

            Assert.Equal(1, Seams.CountCalls(nameof(Tour_New_Popup.Reset), scene.NewTour));
            Assert.True(NewTourMap.NationalMode);
        }

        [Theory]
        [InlineData(SpecialEvents_Manager._type.WorldTour)]
        [InlineData(SpecialEvents_Manager._type.Concert)]
        public void Continue_OnAnotherTab_LeavesItToTheGame(SpecialEvents_Manager._type tab)
        {
            scene.SpecialEvents.OpenTab = tab;

            Seams.SpecialEventsContinue(scene.SpecialEvents);

            Assert.Equal(0, Seams.CountCalls(nameof(Tour_New_Popup.Reset), scene.NewTour));
        }
    }
}
