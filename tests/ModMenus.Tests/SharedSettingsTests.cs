using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.4.0: each tab's "Use in all saves" box. Apply keeps a ticked tab's settings in a file beside the
    /// saves, which every save and new game then takes when it starts. Secrets are never in it.
    /// </summary>
    public class SharedSettingsTests
    {
        public SharedSettingsTests()
        {
            TestGame.Reset();
            Language.Data[HOTKEYS_LABEL] = "Hotkeys";
            Language.Data[EVERY_SAVE_LABEL] = "Use in all saves";
        }

        private const string ModA = """
            [
                { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 5 },
                { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD", "defaultValue": true },
                { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO", "defaultValue": "Hello" },
                { "type": "secret", "secretID": "Mod A/key", "labelID": "TEST__MOTTO" },
                { "type": "keybind", "varID": "A_Pause", "labelID": "TEST__PICK", "defaultValue": "Space" }
            ]
            """;

        private const string ModB = """
            [
                { "type": "slider", "varID": "B_Level", "labelID": "TEST__VOLUME", "defaultValue": 7 },
                { "type": "keybind", "varID": "B_Jump", "labelID": "TEST__MOTTO", "defaultValue": "J" }
            ]
            """;

        private static Menu OpenMenu()
        {
            TestGame.AddMod("Mod A", ModA);
            TestGame.AddMod("Mod B", ModB);
            return Reopen();
        }

        /// <summary>
        /// The menu built again and opened, as the next time the player opens Mod Settings.
        /// </summary>
        private static Menu Reopen()
        {
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        private static Dictionary<string, Dictionary<string, string>> File() => SharedSettings.Load();

        /// <summary>
        /// Another save loads: the game puts its saved settings in place, then ModMenus sets the rest.
        /// </summary>
        private static void LoadSave(params (string VarID, string Value)[] saved)
        {
            variables.variable.Clear();
            foreach ((string varID, string value) in saved)
                TestGame.Save(varID, value);
            ModMenusUtils.ApplySavedSettings();
        }

        [Fact]
        public void EveryPageHasTheBoxUnderItsTitleAndItStartsUnticked()
        {
            Menu menu = OpenMenu();

            // The line separates the box and its note from the mod's rows
            Assert.Equal(new[] { "ModMenuText_MODMENU__HOTKEYS", SCOPE_ROW_OBJ_NAME, SCOPE_NOTE_OBJ_NAME, SCOPE_LINE_OBJ_NAME }, menu.AllShownRowNames.Take(4));
            menu.ClickTab("Mod A");
            Assert.Equal(new[] { "ModMenuText_Mod A", SCOPE_ROW_OBJ_NAME, SCOPE_NOTE_OBJ_NAME, SCOPE_SECRET_NOTE_OBJ_NAME, SCOPE_LINE_OBJ_NAME, "ModMenuSlider_A_Volume" }, menu.AllShownRowNames.Take(6));
            Assert.Equal(3, menu.ScopeRows.Count);
            foreach (string tab in new[] { "Hotkeys", "Mod A", "Mod B" })
            {
                Assert.False(menu.IsScopeTicked(tab));
                Assert.False(Seams.IsActive(menu.ScopeRow(tab).note));
            }
            Assert.Equal("Use in all saves", Seams.GetComponent<TextMeshProUGUI>(Seams.GetComponent<Checkbox_Text>(Seams.OwnerOf(menu.ScopeRow("Mod A"))).Title).text);
        }

        [Fact]
        public void TickingShowsTheNote()
        {
            Menu menu = OpenMenu();

            menu.ClickScope("Mod A");

            Assert.True(menu.IsScopeTicked("Mod A"));
            Assert.True(Seams.IsActive(menu.ScopeRow("Mod A").note));
            Assert.False(menu.IsScopeTicked("Mod B"));
        }

        /// <summary>
        /// A tab with a secret row says, ticked or not, that secrets are the same in every save. The note under the
        /// box is the same on every tab.
        /// </summary>
        [Fact]
        public void OnlyATabWithASecretSaysSecretsAreInEverySave()
        {
            Menu menu = OpenMenu();

            foreach (string tab in new[] { "Hotkeys", "Mod A", "Mod B" })
                Assert.Equal(EVERY_SAVE_NOTE, Seams.GetComponent<TextMeshProUGUI>(menu.ScopeRow(tab).note).text);
            menu.ClickTab("Mod A");
            Assert.Contains(SCOPE_SECRET_NOTE_OBJ_NAME, menu.AllShownRowNames);
            menu.ClickTab("Mod B");
            Assert.DoesNotContain(SCOPE_SECRET_NOTE_OBJ_NAME, menu.AllShownRowNames);
            menu.ClickTab("Hotkeys");
            Assert.DoesNotContain(SCOPE_SECRET_NOTE_OBJ_NAME, menu.AllShownRowNames);
        }

        /// <summary>
        /// The line about secrets shows whether the box is ticked or not.
        /// </summary>
        [Fact]
        public void TheSecretLineShowsTickedOrNot()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod A");
            UnityEngine.GameObject line = Assert.Single(menu.AllRows, row => Seams.NameOf(row) == SCOPE_SECRET_NOTE_OBJ_NAME);

            Assert.True(Seams.IsActive(line));
            menu.ClickScope("Mod A");
            Assert.True(Seams.IsActive(line));
            Assert.Equal(EVERY_SAVE_SECRET_NOTE, Seams.GetComponent<TextMeshProUGUI>(line).text);
        }

        /// <summary>
        /// Ticking or unticking the box never touches a secret, and neither does a save loading.
        /// </summary>
        [Fact]
        public void TheBoxNeverChangesASecret()
        {
            TestGame.SaveSecret("Mod A/key", "sk-saved-key-0000");
            Menu menu = OpenMenu();
            menu.ClickScope("Mod A");
            menu.Apply();

            menu = Reopen();
            menu.ClickScope("Mod A");
            menu.Apply();
            LoadSave();

            Assert.Equal("sk-saved-key-0000", TestGame.SecretFile("Mod A/key"));
        }

        [Fact]
        public void ApplyKeepsATickedTabsSettingsButNotSecretsOrKeys()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod A");
            menu.Drag("A_Volume", 1f);
            menu.Type("A_Motto", "Changed");
            menu.TypeSecret("Mod A/key", "sk-typed-key-0000");
            menu.ClickScope("Mod A");

            menu.Apply();

            Assert.Equal(new[] { "Mod A" }, File().Keys);
            Assert.Equal(new Dictionary<string, string> { ["A_Volume"] = "20", ["A_Loud"] = "1", ["A_Motto"] = "Changed" }, File()["Mod A"]);
            Assert.DoesNotContain("sk-typed", System.IO.File.ReadAllText(SharedSettings.FilePath));
            // The secret is in its own file, not in the save
            Assert.Equal("20", TestGame.Saved("A_Volume"));
            Assert.DoesNotContain(variables.variable, v => v.value != null && v.value.Contains("sk-typed"));
            Assert.Equal("sk-typed-key-0000", TestGame.SecretFile("Mod A/key"));
        }

        [Fact]
        public void CancelSharesNothing()
        {
            Menu menu = OpenMenu();
            menu.ClickScope("Mod A");

            menu.Cancel();
            menu.Open();

            Assert.False(System.IO.File.Exists(SharedSettings.FilePath));
            Assert.False(menu.IsScopeTicked("Mod A"));
        }

        /// <summary>
        /// With nothing ticked and nothing shared before, Apply writes no file.
        /// </summary>
        [Fact]
        public void NothingTickedWritesNoFile()
        {
            OpenMenu().Apply();

            Assert.False(System.IO.File.Exists(SharedSettings.FilePath));
        }

        [Fact]
        public void TheMenuShowsWhichTabsAreShared()
        {
            Menu menu = OpenMenu();
            menu.ClickScope("Mod B");
            menu.Apply();

            menu = Reopen();

            Assert.True(menu.IsScopeTicked("Mod B"));
            Assert.True(Seams.IsActive(menu.ScopeRow("Mod B").note));
            Assert.False(menu.IsScopeTicked("Mod A"));
        }

        [Fact]
        public void AnotherSaveTakesTheSharedSettingsOverItsOwn()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod A");
            menu.Drag("A_Volume", 1f);
            menu.ClickScope("Mod A");
            menu.Apply();

            LoadSave(("A_Volume", "3"), ("B_Level", "40"));

            Assert.Equal("20", TestGame.Saved("A_Volume"));
            // Not shared: the unticked tab keeps the save's own values
            Assert.Equal("40", TestGame.Saved("B_Level"));
        }

        [Fact]
        public void ANewGameTakesTheSharedSettings()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod A");
            menu.Type("A_Motto", "Ours");
            menu.ClickScope("Mod A");
            menu.Apply();

            LoadSave();

            Assert.Equal(("Ours", "7"), (TestGame.Saved("A_Motto"), TestGame.Saved("B_Level")));
        }

        /// <summary>
        /// An empty text field is shared as empty, not read back as missing (the game's JSON reader would drop it).
        /// </summary>
        [Fact]
        public void AnEmptyTextFieldIsSharedAsEmpty()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod A");
            menu.Type("A_Motto", "");
            menu.ClickScope("Mod A");
            menu.Apply();

            LoadSave(("A_Motto", "Mine"));

            Assert.Equal("", TestGame.Saved("A_Motto"));
        }

        [Fact]
        public void UntickingStopsSharingAndEverySaveKeepsWhatItHas()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod A");
            menu.Drag("A_Volume", 1f);
            menu.ClickScope("Mod A");
            menu.Apply();

            menu = Reopen();
            menu.ClickScope("Mod A");
            menu.Apply();

            Assert.Empty(File());
            Assert.Equal("20", TestGame.Saved("A_Volume"));
            LoadSave(("A_Volume", "3"));
            Assert.Equal("3", TestGame.Saved("A_Volume"));
        }

        [Fact]
        public void TheHotkeysBoxSharesEveryModsKeysAndAModsBoxNone()
        {
            Menu menu = OpenMenu();
            menu.ClickScope("Hotkeys");
            menu.ClickScope("Mod A");

            menu.Apply();

            Assert.Equal(new Dictionary<string, string> { ["A_Pause"] = "Space", ["B_Jump"] = "J" }, File()[HOTKEYS_LABEL]);
            Assert.DoesNotContain("A_Pause", File()["Mod A"].Keys);
        }

        /// <summary>
        /// Reset doesn't untick the box: on a ticked tab, Apply shares the defaults.
        /// </summary>
        [Fact]
        public void ResetOnATickedTabSharesTheDefaults()
        {
            Menu menu = OpenMenu();
            menu.ClickTab("Mod A");
            menu.Drag("A_Volume", 1f);
            menu.ClickScope("Mod A");
            menu.Apply();

            menu = Reopen();
            menu.ClickTab("Mod A");
            menu.Reset();

            Assert.True(menu.IsScopeTicked("Mod A"));
            menu.Apply();
            Assert.Equal("5", File()["Mod A"]["A_Volume"]);
        }

        /// <summary>
        /// A setting a mod gained after its tab was shared gets its default, and the next Apply shares it.
        /// </summary>
        [Fact]
        public void ANewSettingGetsItsDefaultUntilTheNextApply()
        {
            SharedSettings.Save(new() { ["Mod B"] = new() { ["B_Level"] = "40" } });
            TestGame.AddMod("Mod B", """
                [
                    { "type": "slider", "varID": "B_Level", "labelID": "TEST__VOLUME", "defaultValue": 7 },
                    { "type": "checkbox", "varID": "B_New", "labelID": "TEST__LOUD", "defaultValue": true }
                ]
                """);

            LoadSave();
            Assert.Equal(("40", "1"), (TestGame.Saved("B_Level"), TestGame.Saved("B_New")));

            Reopen().Apply();
            Assert.Equal(new Dictionary<string, string> { ["B_Level"] = "40", ["B_New"] = "1" }, File()["Mod B"]);
        }

        /// <summary>
        /// The shared settings of a mod that's off aren't put in saves, but stay shared for when it's back: on its
        /// own tab and on the Hotkeys tab.
        /// </summary>
        [Fact]
        public void AModThatIsOffKeepsItsSharedSettingsButSavesDontGetThem()
        {
            SharedSettings.Save(new()
            {
                ["Mod Z"] = new() { ["Z_Level"] = "9" },
                [HOTKEYS_LABEL] = new() { ["Z_Key"] = "K", ["B_Jump"] = "H" },
            });
            TestGame.AddMod("Mod B", ModB);

            LoadSave();
            Assert.Null(TestGame.Saved("Z_Level"));
            Assert.Null(TestGame.Saved("Z_Key"));
            Assert.Equal("H", TestGame.Saved("B_Jump"));

            Reopen().Apply();
            Assert.Equal(new Dictionary<string, string> { ["Z_Level"] = "9" }, File()["Mod Z"]);
            Assert.Equal(new Dictionary<string, string> { ["Z_Key"] = "K", ["B_Jump"] = "H" }, File()[HOTKEYS_LABEL]);
        }

        private const string ModP = """
            [
                { "type": "input", "varID": "P_Key", "labelID": "TEST__MOTTO", "password": true },
                { "type": "slider", "varID": "P_Volume", "labelID": "TEST__VOLUME" }
            ]
            """;

        /// <summary>
        /// A text field that still has the removed password option is left out, so a save's value for it is never
        /// shared...
        /// </summary>
        [Fact]
        public void ARemovedPasswordRowIsNeverShared()
        {
            TestGame.AddMod("Mod P", ModP);
            TestGame.Save("P_Key", "mine");
            Menu menu = Reopen();
            menu.ClickScope("Mod P");

            menu.Apply();

            Assert.Equal(new Dictionary<string, string> { ["P_Volume"] = "50" }, File()["Mod P"]);
        }

        /// <summary>
        /// ...and never takes a shared value or a starting value, even from a file written by hand.
        /// </summary>
        [Fact]
        public void ARemovedPasswordRowNeverTakesASharedValue()
        {
            SharedSettings.Save(new() { ["Mod P"] = new() { ["P_Key"] = "leaked", ["P_Volume"] = "8" } });
            TestGame.AddMod("Mod P", ModP);

            LoadSave(("P_Key", "mine"));
            Assert.Equal(("mine", "8"), (TestGame.Saved("P_Key"), TestGame.Saved("P_Volume")));
            LoadSave();
            Assert.Null(TestGame.Saved("P_Key"));
        }

        [Fact]
        public void AIFanChattersKeysAreNeverShared()
        {
            TestGame.AddShippedMod("AI Fan Chatter");
            Menu menu = Reopen();
            menu.TypeSecret("Tel AI Mods/openai", "sk-typed-key-0000");
            menu.ClickScope("AI Fan Chatter");

            menu.Apply();

            Assert.DoesNotContain("sk-typed", System.IO.File.ReadAllText(SharedSettings.FilePath));
            Assert.Contains("AIFanChatter_PerWeek", File()["AI Fan Chatter"].Keys);
            Assert.Equal("sk-typed-key-0000", TestGame.SecretFile("Tel AI Mods/openai"));
        }

        [Fact]
        public void TheShippedTextsAreThere()
        {
            TestGame.AddShippedMod("ModMenus");

            Assert.Equal("Use in all saves", ShownText(EVERY_SAVE_LABEL));
            Assert.Equal("Also used for new games. Synced by Steam Cloud.", ShownText(EVERY_SAVE_NOTE));
            Assert.Equal("API keys and other secret fields are used in every save on this PC. Steam Cloud doesn't sync them.", ShownText(EVERY_SAVE_SECRET_NOTE));
        }

        /// <summary>
        /// A file that can't be read means nothing is shared: each save keeps its own, and the problem is logged once.
        /// </summary>
        [Fact]
        public void AFileThatCantBeReadSharesNothing()
        {
            SharedSettings.Save(new() { ["Mod A"] = new() { ["A_Volume"] = "8" } });
            TestGame.AddMod("Mod A", ModA);

            using (new FileStream(SharedSettings.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                LoadSave(("A_Volume", "3"));
                LoadSave(("A_Volume", "3"));
            }

            Assert.Equal("3", TestGame.Saved("A_Volume"));
            Assert.Single(Log.Messages, m => m.Contains("couldn't be read"));
        }

        /// <summary>
        /// If the file can't be written, the save still has every change, and the problem is logged.
        /// </summary>
        [Fact]
        public void AFileThatCantBeWrittenStillSavesTheGame()
        {
            Directory.CreateDirectory(SharedSettings.FilePath);
            Menu menu = OpenMenu();
            menu.ClickTab("Mod A");
            menu.Drag("A_Volume", 1f);
            menu.ClickScope("Mod A");

            menu.Apply();

            Assert.Equal("20", TestGame.Saved("A_Volume"));
            Assert.Contains(Log.Messages, m => m.Contains("couldn't be saved"));
            Assert.Equal(1, Seams.PopupsClosed);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not a settings file\n=\n[\nA_Volume=9")]
        public void AnEmptyOrStrangeFileSharesNothing(string text)
        {
            Directory.CreateDirectory(SharedSettings.Folder);
            System.IO.File.WriteAllText(SharedSettings.FilePath, text);
            TestGame.AddMod("Mod A", ModA);

            LoadSave(("A_Volume", "3"));

            Assert.Equal("3", TestGame.Saved("A_Volume"));
            Assert.Empty(Log.Messages);
        }

        /// <summary>
        /// Names and values with line breaks, backslashes, = and brackets, and empty values, read back as written.
        /// </summary>
        [Fact]
        public void TheFileReadsBackWhatWasWritten()
        {
            Dictionary<string, Dictionary<string, string>> tabs = new()
            {
                ["Mod [1]\\x"] = new() { ["A"] = "line\nbreak\r\\n", ["B"] = "", ["C"] = "a=b" },
                ["#Hash"] = new() { ["D"] = "[E]" },
            };

            Assert.Equal(tabs, SharedSettings.Parse(SharedSettings.Write(tabs)));
        }
    }
}
