using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.3.0: key rows. Clicking the box waits for a key; the key pressed, with its modifiers, is shown and
    /// Apply saves it by name. Every mod's keys are on the Hotkeys tab, and a key two shown rows have is marked on both.
    /// </summary>
    public class KeybindRowTests
    {
        public KeybindRowTests()
        {
            TestGame.Reset();
            TestGame.Labels.ToList().ForEach(l => Language.Data[l.Key] = l.Value);
            Language.Data[HOTKEYS_LABEL] = "Hotkeys";
            Language.Data[PRESS_A_KEY_LABEL] = "Press a key…";
            Language.Data[KEY_NONE_LABEL] = "Not set";
            Language.Data[ALSO_USED_BY_LABEL] = "Also used by: {0}";
        }

        private const string ModA = """
            [
                { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" },
                { "type": "text", "labelID": "TEST__INTRO" },
                { "type": "keybind", "varID": "A_Pause", "labelID": "TEST__VOLUME", "defaultValue": "Space" },
                { "type": "keybind", "varID": "A_Save", "labelID": "TEST__PICK", "defaultValue": "F5" }
            ]
            """;

        private const string ModB = """[ { "type": "keybind", "varID": "B_Jump", "labelID": "TEST__MOTTO", "defaultValue": "J" } ]""";

        private static Menu OpenMenu(params (string Title, string Json)[] mods)
        {
            foreach ((string title, string json) in mods.Length > 0 ? mods : new[] { ("Mod A", ModA), ("Mod B", ModB) })
                TestGame.AddMod(title, json);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        private static void Click(Menu menu, string varID) => Seams.Fire(menu.Item(varID).keybindBox.onClick);

        private static void Clear(Menu menu, string varID) => Seams.Fire(menu.Item(varID).keybindClear.onClick);

        /// <summary>
        /// A frame with these keys held, of which these went down, with the row's LateUpdate.
        /// </summary>
        private static void Frame(Menu menu, string varID, KeyCode[] held, KeyCode[] down, KeyCode[] up = null)
        {
            Seams.Frame++;
            Seams.KeysHeld.Clear();
            Seams.KeysHeld.UnionWith(held);
            Seams.KeysDown.Clear();
            Seams.KeysDown.UnionWith(down);
            Seams.KeysUp.Clear();
            Seams.KeysUp.UnionWith(up ?? new KeyCode[0]);
            Seams.ItemPollKeybind(menu.Item(varID));
        }

        private static void Press(Menu menu, string varID, params KeyCode[] keys) => Frame(menu, varID, keys, keys.Skip(keys.Length - 1).ToArray());

        private static string Shown(Menu menu, string varID) => menu.Item(varID).keybindText.text;

        private static string Clash(Menu menu, string varID)
        {
            TextMeshProUGUI line = menu.Item(varID).keybindClash;
            return Seams.IsActive(Seams.OwnerOf(line)) ? line.text : null;
        }

        [Theory]
        [InlineData("F5", "F5")]
        [InlineData("f5", "F5")]
        [InlineData("ctrl+f", "Ctrl+F")]
        [InlineData("Alt + Shift + Ctrl + F", "Ctrl+Shift+Alt+F")]
        [InlineData("Control+Alpha4", "Ctrl+Alpha4")]
        [InlineData("Mouse2", "Mouse2")]
        [InlineData("None", "None")]
        [InlineData("", "None")]
        public void KeysAreSavedInOneForm(string written, string saved)
        {
            Assert.Equal(saved, NormalizeKeybind(written));
        }

        [Theory]
        [InlineData("Banana")]
        [InlineData("5")]
        [InlineData("-1")]
        [InlineData("Ctrl+")]
        [InlineData("Win+F")]
        [InlineData("Ctrl+None")]
        public void TextThatIsntAKeyIsntOne(string written)
        {
            Assert.Null(NormalizeKeybind(written));
        }

        [Fact]
        public void NothingSavedIsntAKey() => Assert.Null(NormalizeKeybind(null));

        [Theory]
        [InlineData("Alpha4", "4")]
        [InlineData("Keypad7", "Num 7")]
        [InlineData("Mouse1", "Right mouse")]
        [InlineData("Mouse2", "Middle mouse")]
        [InlineData("Mouse3", "Mouse 4")]
        [InlineData("PageDown", "Page Down")]
        [InlineData("BackQuote", "`")]
        [InlineData("Return", "Enter")]
        [InlineData("Ctrl+Shift+F", "Ctrl+Shift+F")]
        [InlineData("None", "Not set")]
        public void KeysShowAsTheKeyboardNamesThem(string saved, string shown)
        {
            Language.Data[KEY_NONE_LABEL] = "Not set";
            Assert.Equal(shown, KeybindName(saved));
        }

        [Fact]
        public void KeysAreOnTheHotkeysTabUnderEachModsHeading()
        {
            Menu menu = OpenMenu();

            Assert.Equal(new[] { "Hotkeys", "Mod A" }, menu.TabTexts);
            Assert.Equal(new[]
            {
                "ModMenuText_" + HOTKEYS_LABEL,
                SECTION_OBJ_NAME + "_com.test.ModA", "ModMenuText_TEST__INTRO", "ModMenuKeybind_A_Pause", "ModMenuKeybind_A_Save",
                SECTION_OBJ_NAME + "_com.test.ModB", "ModMenuKeybind_B_Jump",
            }, menu.ShownRowNames);

            // Mod B had only keys, so it has no tab of its own; Mod A keeps its checkbox there
            menu.ClickTab("Mod A");
            Assert.Equal(new[] { "ModMenuText_Mod A", "ModMenuCheckbox_A_Loud" }, menu.ShownRowNames);
        }

        /// <summary>
        /// ModMenus' own rows, the game's keys and the debug cheats switch, all go on the Hotkeys tab, first, under
        /// the game's name; ModMenus gets no tab of its own.
        /// </summary>
        [Fact]
        public void TheGamesKeysComeFirstUnderTheGamesName()
        {
            Language.Data[GAME_KEYS_LABEL] = "Idol Manager";
            TestGame.AddMod("Alpha", ModB.Replace("B_Jump", "Alpha_Jump"));
            OwnMenu("""
                [
                    { "type": "checkbox", "varID": "ModMenus_EnableDebugCheats", "labelID": "TEST__LOUD" },
                    { "type": "keybind", "varID": "ModMenus_Key_Pause", "labelID": "TEST__VOLUME", "defaultValue": "Space" }
                ]
                """);

            Menu menu = TestGame.BuildMenu();

            Assert.Equal(new[] { "Hotkeys" }, menu.TabTexts);
            Assert.Equal(new[]
            {
                "ModMenuText_" + HOTKEYS_LABEL,
                SECTION_OBJ_NAME + "_com.test.Folder3283314791", "ModMenuCheckbox_ModMenus_EnableDebugCheats", "ModMenuKeybind_ModMenus_Key_Pause",
                SECTION_OBJ_NAME + "_com.test.Alpha", "ModMenuKeybind_Alpha_Jump",
            }, menu.RowNames);
            GameObject heading = menu.Rows.Single(r => Seams.NameOf(r) == SECTION_OBJ_NAME + "_com.test.Folder3283314791");
            Assert.Equal("Idol Manager", Seams.GetComponent<TextMeshProUGUI>(heading).text);
        }

        /// <summary>
        /// ModMenus itself, with this menu file, which must have the debug cheats switch. Its folder's name is
        /// whatever the Workshop or the player gave it: here a test name.
        /// </summary>
        private static void OwnMenu(string json) => TestGame.AddMod("Folder 3283314791", json);

        [Fact]
        public void WithNoKeysThereIsNoHotkeysTab()
        {
            Menu menu = OpenMenu(("Mod A", """[ { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" } ]"""));

            Assert.Equal(new[] { "Mod A" }, menu.TabTexts);
        }

        [Fact]
        public void ARowShowsItsSavedKeyOrItsDefault()
        {
            TestGame.Save("A_Save", "ctrl+s");
            Menu menu = OpenMenu();

            Assert.Equal(("Space", "Space"), (Shown(menu, "A_Pause"), menu.Item("A_Pause").tempText));
            Assert.Equal(("Ctrl+S", "Ctrl+S"), (Shown(menu, "A_Save"), menu.Item("A_Save").tempText));
        }

        [Fact]
        public void ClickingTheBoxWaitsForAKeyAndTakesIt()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            Assert.Same(menu.Item("A_Pause"), Seams.Listening);
            Assert.Equal("Press a key…", Shown(menu, "A_Pause"));
            Assert.True(Seams.Deselected > 0);

            Press(menu, "A_Pause", KeyCode.P);

            Assert.Null(Seams.Listening);
            Assert.Equal(("P", "P"), (Shown(menu, "A_Pause"), menu.Item("A_Pause").tempText));
            Assert.Equal("P", Seams.MenuValue("A_Pause"));
        }

        [Fact]
        public void AKeyIsTakenWithTheModifiersHeld()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            Frame(menu, "A_Pause", new[] { KeyCode.LeftControl, KeyCode.RightShift }, new[] { KeyCode.LeftControl, KeyCode.RightShift });
            Assert.Same(menu.Item("A_Pause"), Seams.Listening);
            Frame(menu, "A_Pause", new[] { KeyCode.LeftControl, KeyCode.RightShift, KeyCode.F }, new[] { KeyCode.F });

            Assert.Equal("Ctrl+Shift+F", menu.Item("A_Pause").tempText);
        }

        [Fact]
        public void AModifierLetGoAloneIsTheKey()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            Frame(menu, "A_Pause", new[] { KeyCode.LeftShift }, new[] { KeyCode.LeftShift });
            Frame(menu, "A_Pause", new KeyCode[0], new KeyCode[0], new[] { KeyCode.LeftShift });

            Assert.Equal("LeftShift", menu.Item("A_Pause").tempText);
        }

        [Theory]
        [InlineData(KeyCode.Escape)]
        [InlineData(KeyCode.Mouse1)]
        [InlineData(KeyCode.Mouse2)]
        [InlineData(KeyCode.Return)]
        [InlineData(KeyCode.F9)]
        public void AnyKeyOrMouseButtonButTheLeftCanBeTaken(KeyCode key)
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            Press(menu, "A_Pause", key);

            Assert.Equal(key.ToString(), menu.Item("A_Pause").tempText);
        }

        [Fact]
        public void ALeftClickElsewhereStopsWaiting()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            // The click that started it is let go in the same frame
            Seams.KeysUp.Add(KeyCode.Mouse0);
            Seams.ItemPollKeybind(menu.Item("A_Pause"));
            Assert.Same(menu.Item("A_Pause"), Seams.Listening);

            Frame(menu, "A_Pause", new KeyCode[0], new KeyCode[0], new[] { KeyCode.Mouse0 });

            Assert.Null(Seams.Listening);
            Assert.Equal(("Space", "Space"), (Shown(menu, "A_Pause"), menu.Item("A_Pause").tempText));
        }

        [Fact]
        public void ClickingTheBoxAgainStopsWaiting()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            Click(menu, "A_Pause");

            Assert.Null(Seams.Listening);
            Assert.Equal("Space", Shown(menu, "A_Pause"));
        }

        [Fact]
        public void ClickingAnotherBoxMovesTheWait()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            Click(menu, "A_Save");

            Assert.Same(menu.Item("A_Save"), Seams.Listening);
            Assert.Equal("Space", Shown(menu, "A_Pause"));
            Assert.Equal("Press a key…", Shown(menu, "A_Save"));
        }

        [Fact]
        public void TheCrossClearsTheKey()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            Clear(menu, "A_Pause");

            Assert.Null(Seams.Listening);
            Assert.Equal(("Not set", KEY_NONE), (Shown(menu, "A_Pause"), menu.Item("A_Pause").tempText));
        }

        [Fact]
        public void ApplySavesTheKeysAndCancelDoesnt()
        {
            Menu menu = OpenMenu();
            Click(menu, "A_Pause");
            Press(menu, "A_Pause", KeyCode.P);
            Clear(menu, "B_Jump");

            menu.Cancel();
            Assert.Null(TestGame.Saved("A_Pause"));

            menu.Open();
            Assert.Equal("Space", Shown(menu, "A_Pause"));
            Click(menu, "A_Pause");
            Press(menu, "A_Pause", KeyCode.P);
            Clear(menu, "B_Jump");
            menu.Apply();

            Assert.Equal(("P", KEY_NONE, "F5"), (TestGame.Saved("A_Pause"), TestGame.Saved("B_Jump"), TestGame.Saved("A_Save")));
        }

        [Fact]
        public void ClosingTheMenuStopsWaiting()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Pause");
            menu.Cancel();

            Assert.Null(Seams.Listening);
        }

        [Fact]
        public void ResetOnTheHotkeysTabResetsEveryModsKeys()
        {
            Menu menu = OpenMenu();
            Click(menu, "A_Pause");
            Press(menu, "A_Pause", KeyCode.P);
            Clear(menu, "B_Jump");

            menu.Reset();

            Assert.Equal(("Space", "J"), (menu.Item("A_Pause").tempText, menu.Item("B_Jump").tempText));
        }

        [Fact]
        public void TheDefaultIsSavedWhenAGameStarts()
        {
            TestGame.AddMod("Mod A", ModA);
            TestGame.AddMod("Mod C", """[ { "type": "keybind", "varID": "C_Bad", "labelID": "TEST__LOUD", "defaultValue": "Banana" } ]""");

            SaveMissingDefaults();

            Assert.Equal(("Space", "F5", KEY_NONE), (TestGame.Saved("A_Pause"), TestGame.Saved("A_Save"), TestGame.Saved("C_Bad")));
        }

        [Fact]
        public void AKeyTwoRowsHaveIsMarkedOnBoth()
        {
            Menu menu = OpenMenu();
            Assert.Null(Clash(menu, "A_Pause"));

            Click(menu, "B_Jump");
            Press(menu, "B_Jump", KeyCode.Space);

            Assert.Equal("Also used by: Volume (Mod A)", Clash(menu, "B_Jump"));
            Assert.Equal("Also used by: Agency motto (Mod B)", Clash(menu, "A_Pause"));
            Assert.Equal(WARNING_HEIGHT, Seams.Growth[Seams.TransformOf(menu.Row("A_Pause"))]);

            Clear(menu, "B_Jump");

            Assert.Null(Clash(menu, "A_Pause"));
            Assert.Null(Clash(menu, "B_Jump"));
            Assert.Equal(0f, Seams.Growth[Seams.TransformOf(menu.Row("A_Pause"))]);
            Assert.Equal("", menu.Item("A_Pause").keybindClash.text);
        }

        [Fact]
        public void ARowOfTheSameModIsNamedByItsLabelAlone()
        {
            Menu menu = OpenMenu();

            Click(menu, "A_Save");
            Press(menu, "A_Save", KeyCode.Space);

            Assert.Equal("Also used by: Pick one", Clash(menu, "A_Pause"));
        }

        [Fact]
        public void SavedClashesAreMarkedWhenTheMenuOpens()
        {
            TestGame.Save("B_Jump", "space");
            Menu menu = OpenMenu();

            Assert.NotNull(Clash(menu, "A_Pause"));
            Assert.NotNull(Clash(menu, "B_Jump"));
        }

        [Fact]
        public void ResetTakesClashesAway()
        {
            TestGame.Save("B_Jump", "Space");
            Menu menu = OpenMenu();

            menu.Reset();

            Assert.Null(Clash(menu, "A_Pause"));
        }

        [Fact]
        public void KeysWithNoneNeverClash()
        {
            TestGame.Save("A_Pause", "None");
            TestGame.Save("B_Jump", "None");
            Menu menu = OpenMenu();

            Assert.Null(Clash(menu, "A_Pause"));
        }

        /// <summary>
        /// A game key that works only in a dialogue doesn't clash with a mod's key, which works on the agency
        /// screen; pausing, which works there too, does.
        /// </summary>
        [Fact]
        public void GameKeysClashOnlyWhereTheyWork()
        {
            Language.Data[GAME_KEYS_LABEL] = "Idol Manager";
            OwnMenu("""
                [
                    { "type": "checkbox", "varID": "ModMenus_EnableDebugCheats", "labelID": "TEST__LOUD" },
                    { "type": "keybind", "varID": "ModMenus_Key_Pause", "labelID": "TEST__VOLUME", "defaultValue": "Space" },
                    { "type": "keybind", "varID": "ModMenus_Key_DialogueConfirm2", "labelID": "TEST__PICK", "defaultValue": "Space" }
                ]
                """);
            TestGame.Save("B_Jump", "Space");
            Menu menu = OpenMenu(("Mod B", ModB));

            Assert.Equal("Also used by: Agency motto (Mod B)", Clash(menu, "ModMenus_Key_Pause"));
            Assert.Null(Clash(menu, "ModMenus_Key_DialogueConfirm2"));
            Assert.Equal("Also used by: Volume (Idol Manager)", Clash(menu, "B_Jump"));
        }

        [Fact]
        public void AClashCheckThatThrowsCountsAsAClash()
        {
            Dictionary<string, List<string>> clashes = FindClashes(new List<KeyValuePair<string, string>> { new("A", "Space"), new("B", "Space") },
                (a, b) => throw new System.InvalidOperationException("broken"));

            Assert.Equal(new[] { "B" }, clashes["A"]);
            Assert.Contains(Log.Messages, m => m.Contains("clash check failed"));
        }

        /// <summary>
        /// A row hidden by enabledIf doesn't count, and a mod's heading hides with its last shown key.
        /// </summary>
        [Fact]
        public void HiddenRowsDontClashAndTheirHeadingHides()
        {
            TestGame.Save("B_Jump", "Space");
            Menu menu = OpenMenu(("Mod A", ModA), ("Mod B", """[ { "type": "keybind", "varID": "B_Jump", "labelID": "TEST__MOTTO", "defaultValue": "J", "enabledIf": "B_On" } ]"""));

            Assert.False(menu.IsShown("B_Jump"));
            Assert.Null(Clash(menu, "A_Pause"));
            GameObject heading = menu.Rows.Single(r => Seams.NameOf(r) == SECTION_OBJ_NAME + "_com.test.ModB");
            Assert.False(Seams.IsActive(heading));
        }

        [Fact]
        public void FindClashesListsEachRowsOthers()
        {
            Dictionary<string, List<string>> clashes = FindClashes(new List<KeyValuePair<string, string>>
            {
                new("A", "Space"), new("B", "space"), new("C", "Space"), new("D", "F"), new("E", "None"), new("F", "None"),
            }, null);

            Assert.Equal(new[] { "B", "C" }, clashes["A"]);
            Assert.Equal(new[] { "A", "B" }, clashes["C"]);
            Assert.False(clashes.ContainsKey("D"));
            Assert.False(clashes.ContainsKey("E"));
        }

        /// <summary>
        /// The key that picked a row isn't read by any row until it's let go, and for one more frame, as a key let
        /// go (Input.GetKeyUp, quicksave's F5) is read the frame after it was held.
        /// </summary>
        [Fact]
        public void TheKeyTakenDoesNothingUntilLetGo()
        {
            Menu menu = OpenMenu();
            Click(menu, "A_Save");
            Press(menu, "A_Save", KeyCode.F9);
            menu.Apply();

            Frame(menu, "A_Save", new[] { KeyCode.F9 }, new KeyCode[0]);
            Assert.False(Seams.KeybindHeld("A_Save"));
            Frame(menu, "A_Save", new KeyCode[0], new KeyCode[0], new[] { KeyCode.F9 });
            Assert.False(Seams.KeybindReleased("A_Save"));
            Frame(menu, "A_Save", new KeyCode[0], new KeyCode[0]);
            Assert.False(Seams.KeybindHeld("A_Save"));
            Assert.False(Capturing);
            Frame(menu, "A_Save", new[] { KeyCode.F9 }, new[] { KeyCode.F9 });
            Assert.True(Seams.KeybindPressed("A_Save"));
        }
    }
}
