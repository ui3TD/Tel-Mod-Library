using SimpleJSON;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// How AddMenuItems turns each enabled mod's modmenu.json into rows of the menu.
    /// </summary>
    public class MenuBuildTests
    {
        public MenuBuildTests() => TestGame.Reset();

        private static Menu Build(string menuJson)
        {
            TestGame.AddMod("Mod A", menuJson);
            return TestGame.BuildMenu();
        }

        private static TextMeshProUGUI TextOf(GameObject row) => Seams.GetComponent<TextMeshProUGUI>(row);

        [Fact]
        public void RowsFollowTheFileUnderTheModTitle()
        {
            Menu menu = Build("""
                [
                    { "type": "text", "labelID": "TEST__INTRO" },
                    { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME" },
                    { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST"] },
                    { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" },
                    { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO" },
                    { "type": "text", "labelID": "TEST__OUTRO" }
                ]
                """);

            Assert.Equal(new[]
            {
                "ModMenuText_Mod A",
                "ModMenuText_TEST__INTRO",
                "ModMenuSlider_A_Volume",
                "ModMenuDropdown_A_Pick",
                "ModMenuCheckbox_A_Loud",
                "ModMenuInput_A_Motto",
                "ModMenuText_TEST__OUTRO",
            }, menu.RowNames);
        }

        /// <summary>
        /// Fixed in 1.3.0: a file that couldn't be read stopped the whole menu from opening, for every mod.
        /// Now that mod's settings are left out with a warning, and the other mods' settings still show.
        /// </summary>
        [Theory]
        [InlineData("""[ { "type": "text", "labelID": """)]
        [InlineData("""{ "type": "text", "labelID": "TEST__INTRO" }""")]
        [InlineData("")]
        public void BrokenFileIsLeftOutWithAWarning(string brokenJson)
        {
            TestGame.AddMod("Mod A", brokenJson);
            TestGame.AddMod("Mod B", """[ { "type": "checkbox", "varID": "B_Loud", "labelID": "TEST__LOUD" } ]""");

            Assert.Equal(new[] { "ModMenuText_Mod B", "ModMenuCheckbox_B_Loud" }, TestGame.BuildMenu().RowNames);
            Assert.Contains(Log.Messages, m => m.StartsWith("[ModMenus] Mod A's modmenu.json"));
        }

        [Fact]
        public void ModsAreListedInLoadOrder()
        {
            TestGame.AddMod("Mod A", """[ { "type": "text", "labelID": "TEST__INTRO" } ]""");
            TestGame.AddMod("Mod B", """[ { "type": "checkbox", "varID": "B_Loud", "labelID": "TEST__LOUD" } ]""");

            Assert.Equal(
                new[] { "ModMenuText_Mod A", "ModMenuText_TEST__INTRO", "ModMenuText_Mod B", "ModMenuCheckbox_B_Loud" },
                TestGame.BuildMenu().RowNames);
        }

        [Fact]
        public void DisabledModsAndModsWithoutAMenuAreLeftOut()
        {
            TestGame.AddMod("Mod A", """[ { "type": "text", "labelID": "TEST__INTRO" } ]""", enabled: false);
            TestGame.AddMod("Mod B", menuJson: null);
            TestGame.AddMod("Mod C", """[ { "type": "checkbox", "varID": "C_Loud", "labelID": "TEST__LOUD" } ]""");

            Assert.Equal(new[] { "ModMenuText_Mod C", "ModMenuCheckbox_C_Loud" }, TestGame.BuildMenu().RowNames);
        }

        [Fact]
        public void IgnoredItemsAreLeftOut()
        {
            Menu menu = Build("""
                [
                    { "type": "text", "labelID": "TEST__INTRO", "ignore": true },
                    { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD", "ignore": false },
                    { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "ignore": true }
                ]
                """);

            Assert.Equal(new[] { "ModMenuText_Mod A", "ModMenuCheckbox_A_Loud" }, menu.RowNames);
        }

        /// <summary>
        /// Items missing a field they need are skipped, but the mod's title is still shown.
        /// </summary>
        [Theory]
        [InlineData("""{ "labelID": "TEST__INTRO" }""")]
        [InlineData("""{ "type": "button", "labelID": "TEST__INTRO" }""")]
        [InlineData("""{ "type": "slider", "labelID": "TEST__VOLUME" }""")]
        [InlineData("""{ "type": "slider", "varID": "A_Volume", "labelID": "" }""")]
        [InlineData("""{ "type": "checkbox", "labelID": "TEST__LOUD" }""")]
        [InlineData("""{ "type": "dropdown", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST"] }""")]
        [InlineData("""{ "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK" }""")]
        [InlineData("""{ "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": "TEST__FIRST" }""")]
        [InlineData("""{ "type": "input", "labelID": "TEST__MOTTO" }""")]
        [InlineData("""{ "type": "input", "varID": "A_Motto" }""")]
        [InlineData("""{ "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 30 }""")]
        [InlineData("""{ "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 20, "maxValue": 1, "defaultValue": 5 }""")]
        public void IncompleteItemsAreLeftOut(string item)
        {
            Assert.Equal(new[] { "ModMenuText_Mod A" }, Build("[" + item + "]").RowNames);
        }

        /// <summary>
        /// The Steam description's tip for vertical spacing: a text item with an empty label. The game's JSON
        /// parser drops empty strings, so a text item with no label at all does the same.
        /// </summary>
        [Theory]
        [InlineData(", \"labelID\": \"\"")]
        [InlineData("")]
        public void TextWithoutALabelAddsABlankRow(string labelField)
        {
            Menu menu = Build($$"""
                [
                    { "type": "text", "labelID": "TEST__INTRO" },
                    { "type": "text"{{labelField}} },
                    { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" }
                ]
                """);

            Assert.Equal(new[] { "ModMenuText_Mod A", "ModMenuText_TEST__INTRO", "ModMenuText_", "ModMenuCheckbox_A_Loud" }, menu.RowNames);
            Assert.Equal("", TextOf(menu.Rows[2]).text);
            Assert.Null(Seams.GetComponent<UnityEngine.UI.ContentSizeFitter>(menu.Rows[2]));
        }

        /// <summary>
        /// A text row is as tall as its lines, so long text doesn't run into the next row. Mod titles keep their height.
        /// </summary>
        [Fact]
        public void TextRowsFitTheirLines()
        {
            Menu menu = Build("""[ { "type": "text", "labelID": "TEST__INTRO" } ]""");

            UnityEngine.UI.ContentSizeFitter fitter = Seams.GetComponent<UnityEngine.UI.ContentSizeFitter>(menu.Rows[1]);
            Assert.NotNull(fitter);
            Assert.Equal(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize, fitter.verticalFit);
            Assert.Null(Seams.GetComponent<UnityEngine.UI.ContentSizeFitter>(menu.Rows[0]));
        }

        [Fact]
        public void TextShowsItsLabelInBlueOnTheLeft()
        {
            Menu menu = Build("""[ { "type": "text", "labelID": "TEST__INTRO" }, { "type": "text", "labelID": "Not a constant" } ]""");

            TextMeshProUGUI text = TextOf(menu.Rows[1]);
            Assert.Equal("Some settings", text.text);
            Assert.Equal(TEXT_SIZE, text.fontSize);
            Assert.Equal((Color)mainScript.blue32, text.color);
            Assert.Equal(TextAlignmentOptions.Left, Seams.Alignments[text]);

            // A label that isn't in constants.json is shown as written
            Assert.Equal("Not a constant", TextOf(menu.Rows[2]).text);
        }

        [Fact]
        public void TitleShowsTheModNameCentred()
        {
            TextMeshProUGUI title = TextOf(Build("""[ { "type": "text", "labelID": "TEST__INTRO" } ]""").Rows[0]);

            Assert.Equal("Mod A", title.text);
            Assert.Equal(TITLE_SIZE, title.fontSize);
            Assert.Equal((Color)mainScript.black32, title.color);
            Assert.Equal(TextAlignmentOptions.Center, Seams.Alignments[title]);
        }

        [Fact]
        public void SliderTakesItsRangeLabelAndDefault()
        {
            Menu menu = Build("""[ { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 5 } ]""");

            Settings_Slider slider = menu.SliderSettings("A_Volume");
            Assert.Equal((1f, 20f, "TEST__VOLUME"), (slider.Min_Value, slider.Max_Value, slider.Title_Text));
            Assert.Equal((5f, 5f), (menu.Item("A_Volume").defValue, menu.Item("A_Volume").tempValue));
        }

        [Fact]
        public void SliderWithoutRangeOrDefaultRunsFrom0To100StartingAt50()
        {
            Menu menu = Build("""[ { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME" } ]""");

            Settings_Slider slider = menu.SliderSettings("A_Volume");
            Assert.Equal((0f, 100f), (slider.Min_Value, slider.Max_Value));
            Assert.Equal(50f, menu.Item("A_Volume").defValue);
        }

        /// <summary>
        /// A slider whose default is outside its range is left out, and the log says which and why.
        /// </summary>
        [Fact]
        public void SliderWithDefaultOutsideItsRangeIsLeftOutWithAWarning()
        {
            Menu menu = Build("""[ { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 30 } ]""");

            Assert.Equal(new[] { "ModMenuText_Mod A" }, menu.RowNames);
            Assert.Contains("[ModMenus] The slider A_Volume is left out: its range is 1 to 20 and its default is 30.", Log.Messages);
        }

        [Fact]
        public void CheckboxShowsItsLabel()
        {
            Menu menu = Build("""[ { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" } ]""");

            GameObject title = menu.Checkbox("A_Loud").Title;
            Assert.Equal("TEST__LOUD", Seams.GetComponent<Lang_Button>(title).Constant);
            Assert.Equal("Loud mode", Seams.GetComponent<TextMeshProUGUI>(title).text);
        }

        [Theory]
        [InlineData("", 0f)]
        [InlineData(""", "defaultValue": false""", 0f)]
        [InlineData(""", "defaultValue": true""", 1f)]
        public void CheckboxDefaultsToUnticked(string defaultField, float expected)
        {
            Menu menu = Build($$"""[ { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD"{{defaultField}} } ]""");

            Assert.Equal((expected, expected), (menu.Item("A_Loud").defValue, menu.Item("A_Loud").tempValue));
        }

        [Fact]
        public void DropdownListsItsItemsInsteadOfVanillas()
        {
            Menu menu = Build("""
                [ { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND", "Not a constant"], "defaultValue": 2 } ]
                """);

            Assert.Equal(new[] { "First", "Second", "Not a constant" }, Seams.DropdownSetups[menu.Dropdown("A_Pick")]);
            Assert.Equal("TEST__PICK", Seams.GetComponentInChildren<Lang_Button>(menu.Row("A_Pick")).Constant);
            Assert.Equal("Pick one", Seams.GetComponentInChildren<TextMeshProUGUI>(menu.Row("A_Pick")).text);
            Assert.Equal((2f, 2f), (menu.Item("A_Pick").defValue, menu.Item("A_Pick").tempValue));
        }

        [Fact]
        public void DropdownDefaultsToTheFirstItem()
        {
            Menu menu = Build("""[ { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND"] } ]""");

            Assert.Equal(0f, menu.Item("A_Pick").defValue);
        }

        [Fact]
        public void SavedSettingsReplaceTheDefaults()
        {
            TestGame.Save("A_Volume", "12");
            TestGame.Save("A_Loud", "1");
            TestGame.Save("A_Quiet", "0");
            TestGame.Save("A_Pick", "1");
            TestGame.Save("A_Motto", "Saved motto");
            Menu menu = Build("""
                [
                    { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 5 },
                    { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD", "defaultValue": false },
                    { "type": "checkbox", "varID": "A_Quiet", "labelID": "TEST__LOUD", "defaultValue": true },
                    { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND"] },
                    { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO", "defaultValue": "Default motto" }
                ]
                """);

            Assert.Equal(new[] { 12f, 1f, 0f, 1f }, menu.Items.Take(4).Select(i => i.defValue));
            Assert.Equal(new[] { 12f, 1f, 0f, 1f }, menu.Items.Take(4).Select(i => i.tempValue));
            Assert.Equal(("Default motto", "Saved motto"), (menu.Item("A_Motto").defText, menu.Item("A_Motto").tempText));
        }

        [Fact]
        public void TextFieldShowsItsLabelAboveTheField()
        {
            Menu menu = Build("""[ { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO" }, { "type": "input", "varID": "A_Raw", "labelID": "Not a constant" } ]""");

            GameObject title = Assert.Single(Seams.ChildrenOf(menu.Row("A_Motto")), c => Seams.NameOf(c) == "Title");
            Assert.Equal("TEST__MOTTO", Seams.GetComponent<Lang_Button>(title).Constant);
            Assert.Equal("Agency motto", Seams.GetComponent<TextMeshProUGUI>(title).text);
            Assert.Null(Seams.GetComponentInChildren<Lang_Button>(Seams.OwnerOf(menu.Field("A_Motto"))));

            GameObject rawTitle = Assert.Single(Seams.ChildrenOf(menu.Row("A_Raw")), c => Seams.NameOf(c) == "Title");
            Assert.Equal("Not a constant", Seams.GetComponent<TextMeshProUGUI>(rawTitle).text);
        }

        /// <summary>
        /// What's typed is dark in a white box, so it doesn't read as a second blue label.
        /// </summary>
        [Fact]
        public void TextFieldIsADarkTextInAWhiteBox()
        {
            Menu menu = Build("""[ { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO" } ]""");

            TMP_InputField field = menu.Field("A_Motto");
            Assert.Equal(Color.white, Seams.GetComponent<UnityEngine.UI.Image>(Seams.OwnerOf(field)).color);
            TextMeshProUGUI typed = Seams.GetComponent<TextMeshProUGUI>(Seams.ChildrenOf(Seams.ChildrenOf(Seams.OwnerOf(field)).Single(c => Seams.NameOf(c) == "Container")).Single(c => Seams.NameOf(c) == "Text"));
            Assert.Equal((Color)mainScript.black32, typed.color);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData(""", "defaultValue": "Hello world" """, "Hello world")]
        [InlineData(""", "defaultValue": "スター☆" """, "スター☆")]
        [InlineData(""", "defaultValue": 42 """, "42")]
        public void TextFieldTakesItsDefaultOrIsEmpty(string defaultField, string expected)
        {
            Menu menu = Build($$"""[ { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO"{{defaultField}} } ]""");

            Assert.Equal((expected, expected), (menu.Item("A_Motto").defText, menu.Item("A_Motto").tempText));
        }

        /// <summary>
        /// The field is a copy of the rival name field, so its ties to the rival popup are cut: Tab would move to the
        /// rival's last name and typing would rename the rival. Typing changes only the setting.
        /// </summary>
        [Fact]
        public void TextFieldIsCutLooseFromTheRivalPopup()
        {
            Menu menu = Build("""[ { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO" } ]""");

            TMP_InputField field = menu.Field("A_Motto");
            Assert.Null(Seams.GetComponent<InputField_Tab>(Seams.OwnerOf(field)));
            Assert.Single(Seams.Listeners(field.onValueChanged));
            menu.Type("A_Motto", "Typed");
            Assert.Equal("Typed", menu.Item("A_Motto").tempText);
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData(""", "maxLength": 12""", 12)]
        public void TextFieldTakesItsMaxLengthOrHasNoLimit(string maxLengthField, int expected)
        {
            Menu menu = Build($$"""[ { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO"{{maxLengthField}} } ]""");

            Assert.Equal(expected, menu.Field("A_Motto").characterLimit);
        }

        /// <summary>
        /// The other items keep no text, which is how Apply tells them from text fields.
        /// </summary>
        [Fact]
        public void OnlyTextFieldsHaveText()
        {
            Menu menu = Build("""
                [
                    { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME" },
                    { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" },
                    { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST"] }
                ]
                """);
            menu.Open();

            Assert.All(menu.Items, item => Assert.Null(item.tempText));
        }

        /// <summary>
        /// An open dropdown's list moves to the layer above every row, so it is never covered by the rows
        /// below or cut off at the bottom of the menu, and needs no blank rows under it.
        /// </summary>
        [Fact]
        public void DropdownsOpenAboveEveryRow()
        {
            TestGame.AddMod("Mod A", """[ { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST"] } ]""");
            TestGame.AddMod("Mod B", """[ { "type": "dropdown", "varID": "B_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST"] } ]""");

            Menu menu = TestGame.BuildMenu();

            Assert.Equal(new[] { "ModMenuText_Mod A", "ModMenuDropdown_A_Pick", "ModMenuText_Mod B", "ModMenuDropdown_B_Pick" }, menu.RowNames);
            foreach (string varID in new[] { "A_Pick", "B_Pick" })
            {
                Assert.True(menu.Dropdown(varID).isListItem);
                Assert.Same(Seams.TransformOf(menu.DropdownLayer), menu.Dropdown(varID).listParent);
            }
        }

        public static IEnumerable<object[]> ShippedMods() =>
            ShippedMenuTests.MenuFiles().Select(f => new object[] { ((string)f[0]).Split(Path.DirectorySeparatorChar)[0] });

        /// <summary>
        /// Every item our mods show gets a row, and each slider opens at its default with its label from constants.json.
        /// </summary>
        [Theory]
        [MemberData(nameof(ShippedMods))]
        public void ShippedMenusBuildAndOpenAtTheirDefaults(string mod)
        {
            TestGame.AddShippedMod(mod);
            string menuFile = Path.Combine(TestGame.RepoRoot(), "mods", mod, "assets", "JSON", JSON_DIR, JSON_FILE);
            JSONArray items = JSON.Parse(File.ReadAllText(menuFile)).AsArray;
            List<JSONNode> shown = Enumerable.Range(0, items.Count).Select(i => items[i]).Where(i => !i[JSON_FIELD_IGNORE].AsBool).ToList();

            Menu menu = TestGame.BuildMenu();
            menu.Open();

            List<string> expected = new() { MENU_TEXT_OBJ_NAME + "_" + mod };
            expected.AddRange(shown.Select(item => (string)item[JSON_FIELD_TYPE] switch
            {
                JSON_TYPE_TEXT => MENU_TEXT_OBJ_NAME + "_" + item[JSON_FIELD_LABELID],
                JSON_TYPE_SLIDER => MENU_SLIDER_OBJ_NAME + "_" + item[JSON_FIELD_VARID],
                JSON_TYPE_CHECKBOX => MENU_CHECKBOX_OBJ_NAME + "_" + item[JSON_FIELD_VARID],
                JSON_TYPE_DROPDOWN => MENU_DROPDOWN_OBJ_NAME + "_" + item[JSON_FIELD_VARID],
                JSON_TYPE_INPUT => MENU_INPUT_OBJ_NAME + "_" + item[JSON_FIELD_VARID],
                string type => "unknown type " + type,
            }));
            Assert.Equal(expected, menu.RowNames.Where(r => r != MENU_TEXT_OBJ_NAME + "_"));

            foreach (JSONNode slider in shown.Where(i => i[JSON_FIELD_TYPE].Value == JSON_TYPE_SLIDER))
            {
                string varID = slider[JSON_FIELD_VARID];
                GetSliderRange(slider, out _, out _, out float def);
                Assert.Equal(def, menu.Item(varID).tempValue);
                Assert.Equal(Language.Data[slider[JSON_FIELD_LABELID]] + ": " + def, menu.SliderText(varID));
            }
        }
    }
}
