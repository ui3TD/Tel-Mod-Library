using System.Globalization;
using UnityEngine.UI;
using Xunit;

namespace ModMenus.Tests
{
    /// <summary>
    /// The menu shows each setting's saved value, Apply saves what the player set, and Cancel throws it away.
    /// The popup is built once and reused, so Cancel works by reopening it at the saved values.
    /// </summary>
    public class ApplyCancelTests
    {
        public ApplyCancelTests() => TestGame.Reset();

        private const string Volume = "A_Volume";
        private const string Loud = "A_Loud";
        private const string Pick = "B_Pick";

        /// <summary>
        /// Opens two mods' settings: a 1 to 20 slider at 5 and an unticked checkbox, and a three-item dropdown on its first item.
        /// </summary>
        private static Menu OpenMenu()
        {
            TestGame.AddMod("Mod A", """
                [
                    { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 5 },
                    { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" }
                ]
                """);
            TestGame.AddMod("Mod B", """
                [ { "type": "dropdown", "varID": "B_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND", "TEST__THIRD"] } ]
                """);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        private static void ChangeEverything(Menu menu)
        {
            menu.Drag(Volume, 1f);
            menu.Click(Loud);
            menu.Select(Pick, 2);
        }

        /// <summary>
        /// Checks what the menu shows, and the values Apply would save.
        /// </summary>
        private static void AssertShows(Menu menu, float volume, bool loud, int pick)
        {
            Assert.Equal((volume - 1) / 19.0, menu.SliderPosition(Volume), 5);
            Assert.Equal("Volume: " + volume, menu.SliderText(Volume));
            Assert.Equal(loud, menu.IsTicked(Loud));
            Assert.Equal(pick, menu.Selected(Pick));
            Assert.Equal(
                (volume, loud ? 1f : 0f, (float)pick),
                (menu.Item(Volume).tempValue, menu.Item(Loud).tempValue, menu.Item(Pick).tempValue));
        }

        private static (string, string, string) Saved() =>
            (TestGame.Saved(Volume), TestGame.Saved(Loud), TestGame.Saved(Pick));

        [Fact]
        public void OpensAtTheDefaultsUntilSaved()
        {
            AssertShows(OpenMenu(), volume: 5, loud: false, pick: 0);
            Assert.Equal((null, null, null), Saved());
        }

        [Fact]
        public void OpensAtTheSavedSettings()
        {
            TestGame.Save(Volume, "12");
            TestGame.Save(Loud, "1");
            TestGame.Save(Pick, "2");

            AssertShows(OpenMenu(), volume: 12, loud: true, pick: 2);
        }

        [Fact]
        public void UnreadableSavedSettingsShowTheDefaults()
        {
            TestGame.Save(Volume, "loud");
            TestGame.Save(Loud, "");
            TestGame.Save(Pick, "second");

            AssertShows(OpenMenu(), volume: 5, loud: false, pick: 0);
        }

        [Fact]
        public void ApplySavesTheChangesAndCloses()
        {
            Menu menu = OpenMenu();
            ChangeEverything(menu);

            menu.Apply();

            Assert.Equal(("20", "1", "2"), Saved());
            Assert.Equal(1, Seams.PopupsClosed);
        }

        /// <summary>
        /// Apply saves every setting, so mods read the values the menu showed even if the player changed nothing.
        /// </summary>
        /// <summary>
        /// Fixed in 1.3.0: a saved choice past the end of a dropdown's list (the mod's list got shorter)
        /// made the menu throw every time it opened. It now shows and keeps the last item.
        /// </summary>
        [Fact]
        public void DropdownSavedPastTheEndShowsTheLastItem()
        {
            TestGame.Save(Pick, "5");
            Menu menu = OpenMenu();

            Assert.Equal(2, menu.Selected(Pick));
            menu.Apply();
            Assert.Equal("2", TestGame.Saved(Pick));
        }

        /// <summary>
        /// Settings read and write the same in every language, including ones that write 1.5 as 1,5.
        /// </summary>
        [Fact]
        public void SettingsReadAndSaveTheSameInCommaDecimalLanguages()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                TestGame.Save(Volume, "12.0");
                Menu menu = OpenMenu();
                Assert.Equal(12f, menu.Item(Volume).tempValue);

                menu.Apply();
                Assert.Equal("12", TestGame.Saved(Volume));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [Fact]
        public void ApplySavesUnchangedSettingsToo()
        {
            OpenMenu().Apply();

            Assert.Equal(("5", "0", "0"), Saved());
        }

        [Fact]
        public void CancelSavesNothingAndCloses()
        {
            TestGame.Save(Volume, "12");
            Menu menu = OpenMenu();
            ChangeEverything(menu);

            menu.Cancel();

            Assert.Equal(("12", null, null), Saved());
            Assert.Equal(1, Seams.PopupsClosed);
        }

        [Fact]
        public void ReopeningAfterCancelShowsTheSavedSettings()
        {
            TestGame.Save(Volume, "12");
            TestGame.Save(Loud, "1");
            TestGame.Save(Pick, "1");
            Menu menu = OpenMenu();
            menu.Drag(Volume, 0f);
            menu.Click(Loud);
            menu.Select(Pick, 2);
            menu.Cancel();

            menu.Open();

            AssertShows(menu, volume: 12, loud: true, pick: 1);
            menu.Apply();
            Assert.Equal(("12", "1", "1"), Saved());
        }

        [Fact]
        public void ReopeningAfterCancelShowsTheDefaultsIfNothingWasSaved()
        {
            Menu menu = OpenMenu();
            ChangeEverything(menu);
            menu.Cancel();

            menu.Open();

            AssertShows(menu, volume: 5, loud: false, pick: 0);
        }

        [Fact]
        public void ReopeningAfterApplyShowsTheNewSettings()
        {
            Menu menu = OpenMenu();
            ChangeEverything(menu);
            menu.Apply();

            menu.Open();

            AssertShows(menu, volume: 20, loud: true, pick: 2);
        }

        /// <summary>
        /// The slider's track runs from 0 to 1; its value is the nearest whole number in its range.
        /// </summary>
        [Theory]
        [InlineData(0f, "1")]
        [InlineData(1f, "20")]
        [InlineData(0.25f, "6")]
        [InlineData(0.6f, "12")]
        public void SliderSavesWholeNumbersInItsRange(float position, string expected)
        {
            Menu menu = OpenMenu();

            menu.Drag(Volume, position);

            Assert.Equal("Volume: " + expected, menu.SliderText(Volume));
            menu.Apply();
            Assert.Equal(expected, TestGame.Saved(Volume));
        }

        /// <summary>
        /// Reopening puts the slider back at its saved value whatever its range, and saving again keeps it.
        /// </summary>
        [Theory]
        [InlineData(0, 100, 37)]
        [InlineData(1, 20, 12)]
        [InlineData(5, 200, 64)]
        [InlineData(-10, 10, -3)]
        [InlineData(-20, -10, -13)]
        [InlineData(-10, 0, -4)]
        [InlineData(-10, 0, 0)]
        public void SliderReopensAtItsSavedValue(int min, int max, int saved)
        {
            TestGame.Save("A_Range", saved.ToString());
            TestGame.AddMod("Mod A", $$"""
                [ { "type": "slider", "varID": "A_Range", "labelID": "TEST__VOLUME", "minValue": {{min}}, "maxValue": {{max}}, "defaultValue": {{min}} } ]
                """);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            menu.Drag("A_Range", 1f);
            menu.Cancel();

            menu.Open();

            Assert.Equal((double)(saved - min) / (max - min), menu.SliderPosition("A_Range"), 5);
            Assert.Equal("Volume: " + saved, menu.SliderText("A_Range"));
            menu.Apply();
            Assert.Equal(saved.ToString(), TestGame.Saved("A_Range"));
        }

        [Fact]
        public void SliderWithoutRangeOrDefaultOpensHalfway()
        {
            TestGame.AddMod("Mod A", """[ { "type": "slider", "varID": "A_Range", "labelID": "TEST__VOLUME" } ]""");
            Menu menu = TestGame.BuildMenu();

            menu.Open();

            Assert.Equal(0.5f, menu.SliderPosition("A_Range"));
            Assert.Equal("Volume: 50", menu.SliderText("A_Range"));
            menu.Apply();
            Assert.Equal("50", TestGame.Saved("A_Range"));
        }

        /// <summary>
        /// The default is halfway, 10.5, but sliders save whole numbers, so it shows the 10 it saves.
        /// </summary>
        [Fact]
        public void SliderWithoutDefaultShowsTheWholeNumberItSaves()
        {
            TestGame.AddMod("Mod A", """[ { "type": "slider", "varID": "A_Range", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20 } ]""");
            Menu menu = TestGame.BuildMenu();

            menu.Open();

            Assert.Equal("Volume: 10", menu.SliderText("A_Range"));
            menu.Apply();
            Assert.Equal("10", TestGame.Saved("A_Range"));
        }

        /// <summary>
        /// A label that isn't in constants.json is shown as written, as for the other items.
        /// </summary>
        [Fact]
        public void SliderShowsALabelThatIsNotAConstant()
        {
            TestGame.AddMod("Mod A", """[ { "type": "slider", "varID": "A_Range", "labelID": "Raw volume", "defaultValue": 50 } ]""");
            Menu menu = TestGame.BuildMenu();

            menu.Open();
            Assert.Equal("Raw volume: 50", menu.SliderText("A_Range"));

            menu.Drag("A_Range", 0.2f);
            Assert.Equal("Raw volume: 20", menu.SliderText("A_Range"));
            menu.Apply();
            Assert.Equal("20", TestGame.Saved("A_Range"));
        }

        [Theory]
        [InlineData(1, true, "1")]
        [InlineData(2, false, "0")]
        [InlineData(3, true, "1")]
        public void CheckboxTogglesOnEachClick(int clicks, bool ticked, string saved)
        {
            Menu menu = OpenMenu();

            for (int i = 0; i < clicks; i++)
                menu.Click(Loud);

            Assert.Equal(ticked, menu.IsTicked(Loud));
            menu.Apply();
            Assert.Equal(saved, TestGame.Saved(Loud));
        }

        private const string Motto = "C_Motto";

        /// <summary>
        /// Opens a mod's text field, whose default is "Hello".
        /// </summary>
        private static Menu OpenTextMenu()
        {
            TestGame.AddMod("Mod C", """[ { "type": "input", "varID": "C_Motto", "labelID": "TEST__MOTTO", "defaultValue": "Hello" } ]""");
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        [Fact]
        public void TextFieldOpensAtItsDefaultUntilSaved()
        {
            Menu menu = OpenTextMenu();

            Assert.Equal(("Hello", "Hello"), (menu.FieldText(Motto), menu.Item(Motto).tempText));
            Assert.Null(TestGame.Saved(Motto));
        }

        [Fact]
        public void TextFieldOpensAtItsSavedText()
        {
            TestGame.Save(Motto, "Saved");

            Assert.Equal("Saved", OpenTextMenu().FieldText(Motto));
        }

        /// <summary>
        /// Whatever the player types is saved as typed, including nothing at all, and the game's number format doesn't touch it.
        /// </summary>
        [Theory]
        [InlineData("Bye")]
        [InlineData("  spaces kept  ")]
        [InlineData("スター☆")]
        [InlineData("1,5")]
        [InlineData("")]
        public void ApplySavesTheTextAsTyped(string typed)
        {
            Menu menu = OpenTextMenu();
            menu.Type(Motto, typed);
            Assert.Null(TestGame.Saved(Motto));

            menu.Apply();

            Assert.Equal(typed, TestGame.Saved(Motto));
            Assert.Equal(1, Seams.PopupsClosed);
        }

        [Fact]
        public void ApplySavesAnUnchangedTextFieldToo()
        {
            OpenTextMenu().Apply();

            Assert.Equal("Hello", TestGame.Saved(Motto));
        }

        [Fact]
        public void CancelThrowsTheTypingAwayAndReopensAtTheSavedText()
        {
            TestGame.Save(Motto, "Saved");
            Menu menu = OpenTextMenu();
            menu.Type(Motto, "Typed");

            menu.Cancel();
            Assert.Equal("Saved", TestGame.Saved(Motto));

            menu.Open();
            Assert.Equal(("Saved", "Saved"), (menu.FieldText(Motto), menu.Item(Motto).tempText));
        }

        [Fact]
        public void ReopeningAfterApplyShowsTheNewText()
        {
            Menu menu = OpenTextMenu();
            menu.Type(Motto, "Typed");
            menu.Apply();

            menu.Open();

            Assert.Equal("Typed", menu.FieldText(Motto));
        }

        [Fact]
        public void ReopeningScrollsBackToTheTop()
        {
            Menu menu = OpenMenu();
            ScrollRect scroll = Seams.GetComponentInChildren<ScrollRect>(menu.Panel);
            Seams.ScrollPositions[scroll] = 0.2f;
            menu.Cancel();

            menu.Open();

            Assert.Equal(1f, Seams.ScrollPositions[scroll]);
        }
    }
}
