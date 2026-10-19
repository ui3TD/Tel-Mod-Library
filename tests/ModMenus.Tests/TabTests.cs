using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.0.0: each mod's settings are a page, shown by clicking the mod's tab. Tabs are in order of
    /// the names they show. Apply and Cancel cover every page, shown or not, and switching tabs keeps changes.
    /// </summary>
    public class TabTests
    {
        public TabTests() => TestGame.Reset();

        private const string ModA = """
            [
                { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 5 },
                { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" },
                { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND", "TEST__THIRD"] },
                { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO", "defaultValue": "Shine" }
            ]
            """;

        private const string ModB = """[ { "type": "dropdown", "varID": "B_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND", "TEST__THIRD"] } ]""";

        /// <summary>
        /// Opens Mod A's and Mod B's settings, on Mod A's tab.
        /// </summary>
        private static Menu OpenMenu()
        {
            TestGame.AddMod("Mod A", ModA);
            TestGame.AddMod("Mod B", ModB);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        private static void ChangeModA(Menu menu)
        {
            menu.Drag("A_Volume", 1f);
            menu.Click("A_Loud");
            menu.Select("A_Pick", 2);
            menu.Type("A_Motto", "Typed");
        }

        private static void AssertModAChanged(Menu menu)
        {
            Assert.Equal("Volume: 20", menu.SliderText("A_Volume"));
            Assert.True(menu.IsTicked("A_Loud"));
            Assert.Equal(2, menu.Selected("A_Pick"));
            Assert.Equal("Typed", menu.FieldText("A_Motto"));
            Assert.Equal((20f, 1f, 2f, "Typed"), (menu.Item("A_Volume").tempValue, menu.Item("A_Loud").tempValue, menu.Item("A_Pick").tempValue, menu.Item("A_Motto").tempText));
        }

        [Fact]
        public void EachModGetsATabAndAPage()
        {
            Menu menu = OpenMenu();

            Assert.Equal(new[] { "Mod A", "Mod B" }, menu.TabTexts);
            Assert.Equal(new[] { "ModMenuPage_com.test.ModA", "ModMenuPage_com.test.ModB" }, menu.Pages.Select(Seams.NameOf));
            Assert.Equal(new[] { "ModMenuTab_com.test.ModA", "ModMenuTab_com.test.ModB" }, menu.Tabs.Select(tab => Seams.NameOf(Seams.OwnerOf(tab))));
            for (int i = 0; i < menu.Tabs.Count; i++)
            {
                Assert.Same(menu.Pages[i], menu.Tabs[i].page);
                Assert.Equal("ModMenuText_" + menu.TabTexts[i], Seams.NameOf(Seams.ChildrenOf(menu.Pages[i])[0]));
            }
            Assert.Equal((MENUITEM_SPACING, false), Seams.Columns[Seams.GetComponent<VerticalLayoutGroup>(menu.Pages[0])]);
        }

        [Fact]
        public void TabsAreSortedByNameIgnoringCase()
        {
            TestGame.AddMod("zeta", """[ { "type": "checkbox", "varID": "Z_Loud", "labelID": "TEST__LOUD" } ]""");
            TestGame.AddMod("Alpha", """[ { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" } ]""");
            TestGame.AddMod("beta", """[ { "type": "checkbox", "varID": "B_Loud", "labelID": "TEST__LOUD" } ]""");

            Menu menu = TestGame.BuildMenu();

            Assert.Equal(new[] { "Alpha", "beta", "zeta" }, menu.TabTexts);
            Assert.Equal(new[] { "ModMenuText_Alpha", "ModMenuCheckbox_A_Loud", "ModMenuText_beta", "ModMenuCheckbox_B_Loud", "ModMenuText_zeta", "ModMenuCheckbox_Z_Loud" }, menu.RowNames);
        }

        /// <summary>
        /// Two mods with the same name (a copy from the Workshop and a local one, say) keep their load order.
        /// </summary>
        [Fact]
        public void ModsWithTheSameNameKeepLoadOrder()
        {
            TestGame.AddMod("Same", """[ { "type": "checkbox", "varID": "First_Loud", "labelID": "TEST__LOUD" } ]""");
            Mods._Mods.Last().ModName = "com.test.First";
            TestGame.AddMod("Same", """[ { "type": "checkbox", "varID": "Second_Loud", "labelID": "TEST__LOUD" } ]""");
            Mods._Mods.Last().ModName = "com.test.Second";

            Menu menu = TestGame.BuildMenu();

            Assert.Equal(new[] { "com.test.First", "com.test.Second" }, menu.Tabs.Select(tab => tab.modName));
        }

        /// <summary>
        /// A mod named with a constant sorts by the name its tab shows, not by the constant's ID.
        /// </summary>
        [Fact]
        public void TabsAreSortedByTheNameTheyShow()
        {
            TestGame.AddMod("Mod A", """[ { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" } ]""");
            TestGame.AddMod("TEST__LOUD", """[ { "type": "checkbox", "varID": "L_Loud", "labelID": "TEST__LOUD" } ]""");

            Assert.Equal(new[] { "Loud mode", "Mod A" }, TestGame.BuildMenu().TabTexts);
        }

        [Fact]
        public void OnlyTheFirstPageIsShownAtFirst()
        {
            Menu menu = OpenMenu();

            Assert.Same(menu.Pages[0], menu.ShownPage);
            Assert.Same(menu.Tabs[0], menu.SelectedTab);
            Assert.Equal(new[] { "ModMenuText_Mod A", "ModMenuSlider_A_Volume", "ModMenuCheckbox_A_Loud", "ModMenuDropdown_A_Pick", "ModMenuInput_A_Motto" }, menu.ShownRowNames);
        }

        [Fact]
        public void ClickingATabShowsOnlyItsPage()
        {
            Menu menu = OpenMenu();

            menu.ClickTab("Mod B");

            Assert.Same(menu.Pages[1], menu.ShownPage);
            Assert.Same(menu.Tab("Mod B"), menu.SelectedTab);
            Assert.Equal(new[] { "ModMenuText_Mod B", "ModMenuDropdown_B_Pick" }, menu.ShownRowNames);

            menu.ClickTab("Mod A");

            Assert.Same(menu.Pages[0], menu.ShownPage);
        }

        [Fact]
        public void ClickingATabScrollsToTheTop()
        {
            Menu menu = OpenMenu();
            Seams.ScrollPositions[menu.Scroll] = 0.3f;
            int stopped = Seams.MovementsStopped;

            menu.ClickTab("Mod B");

            Assert.Equal(1f, Seams.ScrollPositions[menu.Scroll]);
            Assert.Equal(stopped + 1, Seams.MovementsStopped);
        }

        [Fact]
        public void ClickingTheShownTabChangesNothing()
        {
            Menu menu = OpenMenu();
            Seams.ScrollPositions[menu.Scroll] = 0.3f;
            int stopped = Seams.MovementsStopped;

            menu.ClickTab("Mod A");

            Assert.Equal(0.3f, Seams.ScrollPositions[menu.Scroll]);
            Assert.Equal(stopped, Seams.MovementsStopped);
        }

        /// <summary>
        /// An open list sits above every row, outside its page, so it would stay over the next page.
        /// </summary>
        [Fact]
        public void ClickingATabClosesOpenLists()
        {
            Menu menu = OpenMenu();
            menu.OpenList("A_Pick");
            Assert.Single(Seams.ChildrenOf(menu.DropdownLayer));

            menu.ClickTab("Mod B");

            Assert.False(menu.Dropdown("A_Pick").isOn);
            Assert.Empty(Seams.ChildrenOf(menu.DropdownLayer));
            Assert.Contains(Seams.OwnerOf(menu.Dropdown("A_Pick")), Seams.ChildrenOf(menu.Row("A_Pick")), new SameObject());
        }

        [Fact]
        public void ChangesSurviveSwitchingTabs()
        {
            Menu menu = OpenMenu();
            ChangeModA(menu);

            menu.ClickTab("Mod B");
            menu.ClickTab("Mod A");

            AssertModAChanged(menu);
            Assert.Null(TestGame.Saved("A_Volume"));
        }

        /// <summary>
        /// Unity calls a row's OnEnable each time its page shows; if it reloaded the saved setting, switching
        /// tabs would throw away what the player changed. The rows are set up when they're built instead.
        /// </summary>
        [Fact]
        public void RowsDoNothingWhenShown()
        {
            BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (string message in new[] { "Awake", "OnEnable", "Start" })
            {
                Assert.Null(typeof(ModMenuItem).GetMethod(message, declared));
            }
        }

        [Fact]
        public void ApplySavesPagesNeverShown()
        {
            Menu menu = OpenMenu();

            menu.Apply();

            Assert.Equal(("5", "0", "0", "Shine", "0"), (TestGame.Saved("A_Volume"), TestGame.Saved("A_Loud"), TestGame.Saved("A_Pick"), TestGame.Saved("A_Motto"), TestGame.Saved("B_Pick")));
        }

        /// <summary>
        /// Apply saves in tab order, then each page from the top, so a mod's settings are always saved in its
        /// file's order (AI Fan Chatter relies on that).
        /// </summary>
        [Fact]
        public void ApplySavesInTabOrderThenFileOrder()
        {
            TestGame.AddMod("Zeta", """[ { "type": "checkbox", "varID": "Z_1", "labelID": "TEST__LOUD" }, { "type": "checkbox", "varID": "Z_2", "labelID": "TEST__LOUD" } ]""");
            TestGame.AddMod("Alpha", """[ { "type": "checkbox", "varID": "A_1", "labelID": "TEST__LOUD" } ]""");
            Menu menu = TestGame.BuildMenu();
            menu.Open();

            menu.Apply();

            Assert.Equal(new[] { "A_1", "Z_1", "Z_2" }, variables.variable.Select(v => v.name));
        }

        [Fact]
        public void ApplySavesChangesOnEveryPage()
        {
            Menu menu = OpenMenu();
            ChangeModA(menu);
            menu.ClickTab("Mod B");
            menu.Select("B_Pick", 1);

            menu.Apply();

            Assert.Equal(("20", "1", "2", "Typed", "1"), (TestGame.Saved("A_Volume"), TestGame.Saved("A_Loud"), TestGame.Saved("A_Pick"), TestGame.Saved("A_Motto"), TestGame.Saved("B_Pick")));
        }

        [Fact]
        public void CancelDropsChangesOnEveryPage()
        {
            Menu menu = OpenMenu();
            ChangeModA(menu);
            menu.ClickTab("Mod B");
            menu.Select("B_Pick", 1);

            menu.Cancel();
            menu.Open();

            Assert.Null(TestGame.Saved("A_Volume"));
            Assert.Equal("Volume: 5", menu.SliderText("A_Volume"));
            Assert.False(menu.IsTicked("A_Loud"));
            Assert.Equal(0, menu.Selected("A_Pick"));
            Assert.Equal("Shine", menu.FieldText("A_Motto"));
            Assert.Equal(0, menu.Selected("B_Pick"));
            Assert.Equal(0f, menu.Item("B_Pick").tempValue);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyAndCancelCloseOpenLists(bool apply)
        {
            Menu menu = OpenMenu();
            menu.OpenList("A_Pick");

            if (apply)
                menu.Apply();
            else
                menu.Cancel();

            Assert.False(menu.Dropdown("A_Pick").isOn);
            Assert.Empty(Seams.ChildrenOf(menu.DropdownLayer));
            Assert.Equal(1, Seams.PopupsClosed);
        }

        /// <summary>
        /// A setting saved while the menu was closed (by the mod itself, say) shows on a hidden page too.
        /// </summary>
        [Fact]
        public void ReopeningShowsSavedValuesOnHiddenPages()
        {
            Menu menu = OpenMenu();
            menu.Cancel();
            TestGame.Save("B_Pick", "2");

            menu.Open();

            Assert.Same(menu.Pages[0], menu.ShownPage);
            Assert.Equal(2, menu.Selected("B_Pick"));
            Assert.Equal(2f, menu.Item("B_Pick").tempValue);
        }

        [Fact]
        public void ReopeningShowsTheLastTab()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod B");
            Seams.ScrollPositions[menu.Scroll] = 0.3f;
            menu.Cancel();

            menu.Open();

            Assert.Same(menu.Pages[1], menu.ShownPage);
            Assert.Same(menu.Tab("Mod B"), menu.SelectedTab);
            Assert.Equal(1f, Seams.ScrollPositions[menu.Scroll]);
        }

        /// <summary>
        /// The menu is built again for each game the player loads; it opens on the tab they last used.
        /// </summary>
        [Fact]
        public void ARebuiltMenuOpensOnTheLastTab()
        {
            OpenMenu().ClickTab("Mod B");

            Menu rebuilt = TestGame.BuildMenu();

            Assert.Same(rebuilt.Tab("Mod B"), rebuilt.SelectedTab);
            Assert.Same(rebuilt.Pages[1], rebuilt.ShownPage);
        }

        [Fact]
        public void ALastTabNoLongerInstalledFallsBackToTheFirst()
        {
            ModMenuManager.lastModName = "com.test.Removed";

            Menu menu = OpenMenu();

            Assert.Same(menu.Tab("Mod A"), menu.SelectedTab);
            Assert.Same(menu.Pages[0], menu.ShownPage);
        }

        [Fact]
        public void NoModWithAMenuShowsAMessage()
        {
            TestGame.AddMod("Mod A", menuJson: null);
            Menu menu = TestGame.BuildMenu();
            menu.Open();

            Assert.Empty(menu.Tabs);
            Assert.Equal(new[] { "ModMenuText_" + NO_SETTINGS_LABEL }, menu.RowNames);

            menu.Apply();

            Assert.Empty(variables.variable);
            Assert.Equal(1, Seams.PopupsClosed);
        }

        /// <summary>
        /// The shown tab is green with white text, as the game's own tabs are. The others are blue text with no fill.
        /// </summary>
        [Fact]
        public void TheShownTabIsGreen()
        {
            Menu menu = OpenMenu();
            Color32 blue = mainScript.blue32;

            Assert.Equal((Color)mainScript.green32, menu.Tab("Mod A").image.color);
            Assert.Equal(Color.white, menu.Tab("Mod A").label.color);
            Assert.Equal((Color)new Color32(blue.r, blue.g, blue.b, 0), menu.Tab("Mod B").image.color);
            Assert.Equal((Color)blue, menu.Tab("Mod B").label.color);

            menu.ClickTab("Mod B");

            Assert.Equal((Color)mainScript.green32, menu.Tab("Mod B").image.color);
            Assert.Equal((Color)blue, menu.Tab("Mod A").label.color);
        }

        [Fact]
        public void PointingAtATabTintsItUntilThePointerLeaves()
        {
            Menu menu = OpenMenu();
            Color32 blue = mainScript.blue32;
            ModMenuTab other = menu.Tab("Mod B");

            Seams.TabOnPointerEnter(other, null);
            Assert.Equal((Color)new Color32(blue.r, blue.g, blue.b, 38), other.image.color);

            Seams.TabOnPointerExit(other, null);
            Assert.Equal((Color)new Color32(blue.r, blue.g, blue.b, 0), other.image.color);

            Seams.TabOnPointerEnter(menu.Tab("Mod A"), null);
            Assert.Equal((Color)mainScript.green32, menu.Tab("Mod A").image.color);
        }

        /// <summary>
        /// A tab's name wraps onto two lines, then ends in "…"; the whole tab takes clicks, not its name.
        /// The wrapping itself needs the game's text layout, so the in-game test checks it.
        /// </summary>
        [Fact]
        public void TabNamesWrapToTwoLinesThenEndInAnEllipsis()
        {
            Menu menu = OpenMenu();

            foreach (ModMenuTab tab in menu.Tabs)
            {
                LabelWrap wrap = Seams.GetComponent<LabelWrap>(Seams.OwnerOf(tab));
                Assert.Equal(TAB_MAX_LINES, wrap.maxLines);
                Assert.Same(tab.label, wrap.label);
                Assert.Empty(wrap.below);
                Assert.Null((object)wrap.box);
                Assert.Equal(TEXT_SIZE, tab.label.fontSize);
                Assert.Contains(tab.button, Seams.StyledTabs);
            }
            Assert.All(menu.Rows.Select(Seams.GetComponent<LabelWrap>).Where(wrap => wrap is not null), wrap => Assert.Equal(0, wrap.maxLines));
        }

        [Fact]
        public void PageTitleShowsTheWholeName()
        {
            string longName = string.Concat(Enumerable.Repeat("A very long mod name ", 8)).Trim();
            TestGame.AddMod(longName, ModB);

            Menu menu = TestGame.BuildMenu();

            TextMeshProUGUI title = Seams.GetComponent<TextMeshProUGUI>(menu.Rows[0]);
            Assert.Equal(longName, title.text);
            Assert.Equal(ContentSizeFitter.FitMode.PreferredSize, Seams.GetComponent<ContentSizeFitter>(menu.Rows[0]).verticalFit);
            Assert.Equal(longName, Assert.Single(menu.TabTexts));
        }

        [Theory]
        [InlineData(20, 20, 2, 20)]
        [InlineData(40, 20, 2, 40)]
        [InlineData(100, 20, 2, 50)]
        [InlineData(100, 20, 0, 100)]
        public void LabelHeightKeepsUpToMaxLines(float wrapped, float oneLine, int maxLines, float expected)
        {
            Assert.Equal(expected, CappedLabelHeight(wrapped, oneLine, maxLines));
        }

        /// <summary>
        /// When the menu opens, the tab column scrolls only as far as it must to show the tab shown whole.
        /// </summary>
        [Theory]
        [InlineData(0, 30, 0f, 1f)]         // the first tab, scrolled to the bottom: back to the top
        [InlineData(470, 500, 1f, 0f)]      // the last tab, at the top: to the bottom
        [InlineData(100, 130, 1f, 1f)]      // in view already: no change
        [InlineData(250, 290, 1f, 0.7f)]    // just below the view: its bottom lines up with the view's
        public void TabColumnScrollsToShowATabWhole(float top, float bottom, float position, float expected)
        {
            Assert.Equal(expected, ScrollPositionShowing(500, 200, top, bottom, position), 4);
        }

        /// <summary>
        /// The menu now reaches near the bottom of the screen, so a list opened from its last row would run off it.
        /// Box 100 to 130; list 40 tall, 2 below the box (58 to 98).
        /// </summary>
        [Theory]
        [InlineData(0, 400, 0)]       // fits below: stays
        [InlineData(70, 400, 74)]     // runs off the bottom: goes above the box, 2 above it (132 to 172)
        [InlineData(70, 160, 12)]     // no room above: moves up until its bottom is on screen (70 to 110)
        public void ListsThatWouldRunOffTheScreenMoveUp(float screenBottom, float screenTop, float expected)
        {
            Assert.Equal(expected, ListShift(58, 98, 100, 130, screenBottom, screenTop), 3);
        }

        [Fact]
        public void TabColumnDoesntScrollWhenTabsFit()
        {
            Assert.Equal(1f, ScrollPositionShowing(150, 200, 100, 130, 0.5f));
        }
    }
}
