using TMPro;
using Xunit;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.1.0: Reset to Defaults sets the shown tab's settings back to their defaults in the menu, except
    /// rows with "reset": false. Apply saves them; Cancel throws them away.
    /// </summary>
    public class ResetTests
    {
        public ResetTests() => TestGame.Reset();

        /// <summary>
        /// Mod A has one setting of each kind, with defaults; Mod B has a slider.
        /// </summary>
        private static Menu OpenMenu()
        {
            TestGame.AddMod("Mod A", """
                [
                    { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 5 },
                    { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD", "defaultValue": true },
                    { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND", "TEST__THIRD"], "defaultValue": 1 },
                    { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO", "defaultValue": "Hello" },
                    { "type": "input", "varID": "A_Key", "labelID": "TEST__MOTTO", "password": true },
                    { "type": "input", "varID": "A_Name", "labelID": "TEST__MOTTO", "defaultValue": "Hikari", "reset": false }
                ]
                """);
            TestGame.AddMod("Mod B", """[ { "type": "slider", "varID": "B_Level", "labelID": "TEST__VOLUME", "defaultValue": 7 } ]""");
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        private static void ChangeModA(Menu menu)
        {
            menu.Drag("A_Volume", 1f);
            menu.Click("A_Loud");
            menu.Select("A_Pick", 2);
            menu.Type("A_Motto", "Changed");
            menu.Type("A_Key", "secret");
            menu.Type("A_Name", "Sora");
        }

        [Fact]
        public void ShowsTheShownTabsDefaults()
        {
            Menu menu = OpenMenu();
            ChangeModA(menu);

            menu.Reset();

            Assert.Equal((5f, 1f, 1f), (menu.Item("A_Volume").tempValue, menu.Item("A_Loud").tempValue, menu.Item("A_Pick").tempValue));
            Assert.Equal("Volume: 5", menu.SliderText("A_Volume"));
            Assert.True(menu.IsTicked("A_Loud"));
            Assert.Equal(1, menu.Selected("A_Pick"));
            Assert.Equal(("Hello", "Hello"), (menu.FieldText("A_Motto"), menu.Item("A_Motto").tempText));
        }

        /// <summary>
        /// Password fields are reset like any other; only "reset": false keeps a row as it is.
        /// </summary>
        [Fact]
        public void ResetsPasswordFieldsButNotRowsThatOptOut()
        {
            Menu menu = OpenMenu();
            ChangeModA(menu);

            menu.Reset();

            Assert.Equal("", menu.FieldText("A_Key"));
            Assert.Equal(("Sora", "Sora"), (menu.FieldText("A_Name"), menu.Item("A_Name").tempText));
        }

        [Fact]
        public void ApplySavesTheDefaultsEvenOverSavedSettings()
        {
            TestGame.Save("A_Volume", "12");
            TestGame.Save("A_Loud", "0");
            Menu menu = OpenMenu();

            menu.Reset();
            menu.Apply();

            Assert.Equal(("5", "1", "1", "Hello", "Hikari"), (TestGame.Saved("A_Volume"), TestGame.Saved("A_Loud"), TestGame.Saved("A_Pick"), TestGame.Saved("A_Motto"), TestGame.Saved("A_Name")));
        }

        [Fact]
        public void OtherTabsKeepTheirChanges()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod B");
            menu.Drag("B_Level", 1f);
            menu.ClickTab("Mod A");

            menu.Reset();
            menu.Apply();

            Assert.Equal("100", TestGame.Saved("B_Level"));
        }

        [Fact]
        public void CancelThrowsTheDefaultsAway()
        {
            TestGame.Save("A_Volume", "12");
            Menu menu = OpenMenu();

            menu.Reset();
            menu.Cancel();
            menu.Open();

            Assert.Equal(12, menu.Item("A_Volume").tempValue);
            Assert.Equal("12", TestGame.Saved("A_Volume"));
        }

        /// <summary>
        /// Defaults are shown as the menu shows them when nothing is saved: a dropdown's past its list shows its
        /// last item, a stepped slider's moves to its nearest step, and a number field's is checked.
        /// </summary>
        [Fact]
        public void DefaultsAreShownAsWhenNothingIsSaved()
        {
            TestGame.AddMod("Mod A", """
                [
                    { "type": "dropdown", "varID": "A_Past", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND"], "defaultValue": 7 },
                    { "type": "slider", "varID": "A_Stepped", "labelID": "TEST__VOLUME", "defaultValue": 12, "step": 5 },
                    { "type": "input", "varID": "A_Seats", "labelID": "TEST__MOTTO", "inputType": "integer", "maxValue": 99, "defaultValue": "250" }
                ]
                """);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            menu.Select("A_Past", 0);
            menu.Drag("A_Stepped", 1f);
            menu.TypeAndLeave("A_Seats", "3");

            menu.Reset();
            menu.Apply();

            Assert.Equal(("1", "10", "99"), (TestGame.Saved("A_Past"), TestGame.Saved("A_Stepped"), TestGame.Saved("A_Seats")));
        }

        /// <summary>
        /// Rows with enabledIf show or hide for the defaults.
        /// </summary>
        [Fact]
        public void RowsShowOrHideForTheDefaults()
        {
            TestGame.AddMod("Mod A", """
                [
                    { "type": "checkbox", "varID": "A_On", "labelID": "TEST__LOUD" },
                    { "type": "slider", "varID": "A_Count", "labelID": "TEST__VOLUME", "enabledIf": "A_On" }
                ]
                """);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            menu.Click("A_On");
            Assert.True(menu.IsShown("A_Count"));

            menu.Reset();

            Assert.False(menu.IsShown("A_Count"));
        }

        /// <summary>
        /// Reset closes an open list first: it isn't in its row.
        /// </summary>
        [Fact]
        public void ClosesAnOpenList()
        {
            Menu menu = OpenMenu();
            menu.OpenList("A_Pick");

            menu.Reset();

            Assert.False(menu.Dropdown("A_Pick").isOn);
        }

        [Fact]
        public void DoesNothingWithNoModSettings()
        {
            Menu menu = TestGame.BuildMenu();
            menu.Open();

            menu.Reset();

            Assert.Null(menu.Manager.selected);
        }

        /// <summary>
        /// AI Fan Chatter removes its saved key when its key field is applied empty after showing that a key is
        /// saved, so its key row opts out: Reset leaves what the field shows.
        /// </summary>
        [Fact]
        public void AIFanChattersKeyFieldIsNotReset()
        {
            TestGame.AddShippedMod("AI Fan Chatter");
            TestGame.Save("AIFanChatter_APIKey", "a saved key is kept in its own file outside your saves");
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            menu.Click("AIFanChatter_Enabled");

            menu.Reset();

            Assert.Equal(TMP_InputField.ContentType.Password, menu.Field("AIFanChatter_APIKey").contentType);
            Assert.Equal("a saved key is kept in its own file outside your saves", menu.Item("AIFanChatter_APIKey").tempText);
            Assert.Equal(0, menu.Item("AIFanChatter_Enabled").tempValue);
        }
    }
}
