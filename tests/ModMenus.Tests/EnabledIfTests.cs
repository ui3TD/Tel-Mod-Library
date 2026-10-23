using HarmonyLib;
using System.Linq;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.1.0: a row with "enabledIf": "&lt;varID&gt;" is shown only while that setting is on, and hidden
    /// otherwise; the rows under it move up. Hidden rows keep their value, and Apply still saves it.
    /// </summary>
    public class EnabledIfTests
    {
        public EnabledIfTests() => TestGame.Reset();

        private const string On = "A_On";
        private const string Count = "A_Count";
        private const string Pick = "A_Pick";
        private const string Url = "A_Url";

        /// <summary>
        /// A checkbox that shows a slider, a text row and a dropdown; the dropdown shows a text field when it's
        /// on anything but its first item.
        /// </summary>
        private const string MenuJson = """
            [
                { "type": "checkbox", "varID": "A_On", "labelID": "TEST__LOUD" },
                { "type": "slider", "varID": "A_Count", "labelID": "TEST__VOLUME", "defaultValue": 40, "enabledIf": "A_On" },
                { "type": "text", "labelID": "TEST__INTRO", "enabledIf": "A_On" },
                { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND", "TEST__THIRD"], "enabledIf": "A_On" },
                { "type": "input", "varID": "A_Url", "labelID": "TEST__MOTTO", "enabledIf": "A_Pick" },
                { "type": "text", "labelID": "Always shown" }
            ]
            """;

        private static Menu OpenMenu(string menuJson = MenuJson)
        {
            TestGame.AddMod("Mod A", menuJson);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        private static string[] AllRows => new[]
        {
            MENU_TEXT_OBJ_NAME + "_Mod A",
            MENU_CHECKBOX_OBJ_NAME + "_A_On",
            MENU_SLIDER_OBJ_NAME + "_A_Count",
            MENU_TEXT_OBJ_NAME + "_TEST__INTRO",
            MENU_DROPDOWN_OBJ_NAME + "_A_Pick",
            MENU_INPUT_OBJ_NAME + "_A_Url",
            MENU_TEXT_OBJ_NAME + "_Always shown",
        };

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("0", false)]
        [InlineData("0.0", false)]
        [InlineData("-0", false)]
        [InlineData("1", true)]
        [InlineData("-2", true)]
        [InlineData("0.5", true)]
        [InlineData("abc", true)]
        [InlineData(" ", true)]
        public void ASettingIsOnUnlessEmptyOrZero(string value, bool on)
        {
            Assert.Equal(on, IsSettingOn(value));
        }

        [Fact]
        public void RowsAreHiddenWhileTheirSettingIsOff()
        {
            Menu menu = OpenMenu();

            Assert.Equal(new[] { AllRows[0], AllRows[1], AllRows[6] }, menu.VisibleRowNames);
            Assert.Equal(AllRows, menu.ShownRowNames);
        }

        [Fact]
        public void TickingTheCheckboxShowsThemAndUntickingHidesThem()
        {
            Menu menu = OpenMenu();

            menu.Click(On);
            Assert.Equal(new[] { AllRows[0], AllRows[1], AllRows[2], AllRows[3], AllRows[4], AllRows[6] }, menu.VisibleRowNames);

            menu.Click(On);
            Assert.Equal(new[] { AllRows[0], AllRows[1], AllRows[6] }, menu.VisibleRowNames);
        }

        /// <summary>
        /// A dropdown is on at any item but its first.
        /// </summary>
        [Fact]
        public void ADropdownShowsRowsUnlessOnItsFirstItem()
        {
            Menu menu = OpenMenu();
            menu.Click(On);

            menu.Select(Pick, 2);
            Assert.True(menu.IsShown(Url));

            menu.Select(Pick, 0);
            Assert.False(menu.IsShown(Url));
        }

        /// <summary>
        /// A row whose setting's own row is hidden is hidden too, whatever that setting's value.
        /// </summary>
        [Fact]
        public void HidingARowHidesTheRowsThatDependOnIt()
        {
            Menu menu = OpenMenu();
            menu.Click(On);
            menu.Select(Pick, 2);

            menu.Click(On);

            Assert.False(menu.IsShown(Pick));
            Assert.False(menu.IsShown(Url));
        }

        [Fact]
        public void ASliderShowsRowsAboveZeroAndATextFieldWithText()
        {
            Menu menu = OpenMenu("""
                [
                    { "type": "slider", "varID": "A_Level", "labelID": "TEST__VOLUME", "defaultValue": 0 },
                    { "type": "checkbox", "varID": "A_ByLevel", "labelID": "TEST__LOUD", "enabledIf": "A_Level" },
                    { "type": "input", "varID": "A_Name", "labelID": "TEST__MOTTO" },
                    { "type": "checkbox", "varID": "A_ByName", "labelID": "TEST__LOUD", "enabledIf": "A_Name" }
                ]
                """);
            Assert.False(menu.IsShown("A_ByLevel"));
            Assert.False(menu.IsShown("A_ByName"));

            menu.Drag("A_Level", 0.5f);
            menu.Type("A_Name", "Hikari");
            Assert.True(menu.IsShown("A_ByLevel"));
            Assert.True(menu.IsShown("A_ByName"));

            menu.Drag("A_Level", 0f);
            menu.Type("A_Name", "");
            Assert.False(menu.IsShown("A_ByLevel"));
            Assert.False(menu.IsShown("A_ByName"));
        }

        /// <summary>
        /// The menu opens with rows shown for the saved settings, not for changes thrown away by Cancel.
        /// </summary>
        [Fact]
        public void OpeningShowsTheRowsForTheSavedSettings()
        {
            TestGame.Save(On, "1");
            Menu menu = OpenMenu();
            Assert.True(menu.IsShown(Count));

            menu.Click(On);
            menu.Cancel();
            menu.Open();

            Assert.True(menu.IsShown(Count));
        }

        [Fact]
        public void HiddenRowsAreStillSaved()
        {
            Menu menu = OpenMenu();

            menu.Apply();

            Assert.Equal(("0", "40", "0", ""), (TestGame.Saved(On), TestGame.Saved(Count), TestGame.Saved(Pick), TestGame.Saved(Url)));
        }

        /// <summary>
        /// A row can depend on another mod's setting, which may not be applied yet.
        /// </summary>
        [Fact]
        public void ARowCanDependOnAnotherTabsSetting()
        {
            TestGame.AddMod("Mod A", """[ { "type": "checkbox", "varID": "A_On", "labelID": "TEST__LOUD" } ]""");
            TestGame.AddMod("Mod B", """[ { "type": "checkbox", "varID": "B_Extra", "labelID": "TEST__LOUD", "enabledIf": "A_On" } ]""");
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            Assert.False(menu.IsShown("B_Extra"));

            menu.Click(On);

            Assert.True(menu.IsShown("B_Extra"));
        }

        /// <summary>
        /// A setting no row has is read from the saved settings, so a mod can show a row from its own code.
        /// </summary>
        [Theory]
        [InlineData(null, false)]
        [InlineData("0", false)]
        [InlineData("1", true)]
        public void ASettingWithNoRowIsReadFromTheSave(string saved, bool shown)
        {
            if (saved != null)
                TestGame.Save("Other_Unlocked", saved);

            Menu menu = OpenMenu("""[ { "type": "checkbox", "varID": "A_Extra", "labelID": "TEST__LOUD", "enabledIf": "Other_Unlocked" } ]""");

            Assert.Equal(shown, menu.IsShown("A_Extra"));
        }

        /// <summary>
        /// Rows whose settings lead back to themselves could never be shown again once hidden, so they're always
        /// shown, with a warning for each, once.
        /// </summary>
        [Fact]
        public void RowsInALoopAreShownWithOneWarning()
        {
            Menu menu = OpenMenu("""
                [
                    { "type": "checkbox", "varID": "A_X", "labelID": "TEST__LOUD", "enabledIf": "A_Y" },
                    { "type": "checkbox", "varID": "A_Y", "labelID": "TEST__LOUD", "enabledIf": "A_X" },
                    { "type": "checkbox", "varID": "A_Self", "labelID": "TEST__LOUD", "enabledIf": "A_Self" }
                ]
                """);

            Assert.True(menu.IsShown("A_X"));
            Assert.True(menu.IsShown("A_Y"));
            Assert.True(menu.IsShown("A_Self"));

            menu.Click("A_X");
            menu.Click("A_X");
            Assert.Equal(3, Log.Messages.Count(m => m.Contains("lead back to themselves")));
        }

        /// <summary>
        /// An open list sits in the dropdown layer, outside its row, so hiding the row closes it first.
        /// </summary>
        [Fact]
        public void HidingADropdownClosesItsList()
        {
            Menu menu = OpenMenu();
            menu.Click(On);
            menu.OpenList(Pick);

            menu.Click(On);

            Assert.False(menu.Dropdown(Pick).isOn);
            Assert.Empty(Seams.ChildrenOf(menu.DropdownLayer));
        }

        /// <summary>
        /// A file without enabledIf shows every row, as before.
        /// </summary>
        [Fact]
        public void WithoutEnabledIfEveryRowIsShown()
        {
            Menu menu = OpenMenu(MenuJson.Replace("\"enabledIf\"", "\"notUsed\""));

            Assert.Equal(AllRows, menu.VisibleRowNames);
        }

        /// <summary>
        /// Added in 4.2.0: MenuValue gives what the open menu shows, as Apply would save it, before Apply.
        /// </summary>
        [Fact]
        public void MenuValueGivesWhatTheOpenMenuShows()
        {
            TestGame.Save(Pick, "1");
            Menu menu = OpenMenu();
            Assert.Equal("1", Seams.MenuValue(Pick));

            menu.Click(On);
            menu.Select(Pick, 2);
            menu.Type(Url, "openrouter.ai/api/v1");

            Assert.Equal(("1", "2", "openrouter.ai/api/v1"), (Seams.MenuValue(On), Seams.MenuValue(Pick), Seams.MenuValue(Url)));
            Assert.Equal("1", TestGame.Saved(Pick));
        }

        [Fact]
        public void MenuValueReadsEveryTab()
        {
            TestGame.AddMod("Mod A", """[ { "type": "checkbox", "varID": "A_On", "labelID": "TEST__LOUD" } ]""");
            TestGame.AddMod("Mod B", """[ { "type": "checkbox", "varID": "B_On", "labelID": "TEST__LOUD" } ]""");
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            menu.Click(On);

            menu.ClickTab("Mod B");

            Assert.Equal("1", Seams.MenuValue(On));
        }

        /// <summary>
        /// Null while no menu is open, so a mod reads the saved value then, and for a variable no row has.
        /// </summary>
        [Fact]
        public void MenuValueIsNullWhileClosedAndWithoutARow()
        {
            TestGame.Save(On, "1");
            TestGame.Save("Other_Unlocked", "1");
            Assert.Null(Seams.MenuValue(On));

            Menu menu = OpenMenu();
            Assert.Null(Seams.MenuValue("Other_Unlocked"));
            Assert.Null(Seams.MenuValue(null));

            menu.Cancel();
            Assert.Null(Seams.MenuValue(On));

            menu.Open();
            menu.Apply();
            Assert.Null(Seams.MenuValue(On));
        }

        /// <summary>
        /// A mod can show a row for one dropdown item: the row's enabledIf names a variable no row has, which
        /// the mod answers in a variables.Get postfix from MenuValue. The row follows the dropdown before Apply,
        /// and the variable is never saved.
        /// </summary>
        [Fact]
        public void AModCanAnswerAnEnabledIfFromMenuValue()
        {
            Harmony otherMod = new("tests.modmenus.othermod");
            otherMod.Patch(AccessTools.Method(typeof(variables), nameof(variables.Get)), postfix: new HarmonyMethod(typeof(EnabledIfTests), nameof(AnswerOnThird)));
            try
            {
                Menu menu = OpenMenu("""
                    [
                        { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND", "TEST__THIRD"] },
                        { "type": "input", "varID": "A_Url", "labelID": "TEST__MOTTO", "enabledIf": "A_OnThird" }
                    ]
                    """);
                Assert.False(menu.IsShown(Url));

                menu.Select(Pick, 1);
                Assert.False(menu.IsShown(Url));

                menu.Select(Pick, 2);
                Assert.True(menu.IsShown(Url));
                Assert.DoesNotContain(variables.variable, v => v.name == Pick);

                menu.Apply();
                Assert.Equal("2", variables.variable.Single(v => v.name == Pick).value);
                Assert.DoesNotContain(variables.variable, v => v.name == "A_OnThird");
            }
            finally
            {
                otherMod.UnpatchSelf();
            }
        }

        /// <summary>The other mod's answer: on while the dropdown shows its third item.</summary>
        private static void AnswerOnThird(string name, ref string __result)
        {
            if (name == "A_OnThird")
                __result = (Seams.MenuValue(Pick) ?? variables.Get(Pick)) == "2" ? "1" : "0";
        }
    }
}
