using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 5.0.0: secret rows ("type": "secret"), which replaced the password option. The field is
    /// write-only: it always opens empty, a line under it says what's saved, Remove removes it on Apply. The
    /// secret is kept in its own file, never in a save, the settings used in all saves, or the menu's values.
    /// </summary>
    public class SecretTests : IDisposable
    {
        private const string Key = "Mod A/key";
        private const string LongKey = "sk-ant-api03-abcdefgh-x7Qa";

        private readonly List<string[]> changes = new();

        public SecretTests()
        {
            TestGame.Reset();
            TestGame.LoadShippedTexts("ModMenus");
            SecretsChanged += OnSecretsChanged;
        }

        public void Dispose() => SecretsChanged -= OnSecretsChanged;

        private void OnSecretsChanged(string[] ids) => changes.Add(ids);

        private static Menu OpenMenu(string rows = """{ "type": "secret", "secretID": "Mod A/key", "labelID": "TEST__MOTTO" }""")
        {
            TestGame.AddMod("Mod A", "[ " + rows + " ]");
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        [Fact]
        public void TheFieldIsMaskedAndEmptyWithAHint()
        {
            Menu menu = OpenMenu();

            TMP_InputField field = menu.SecretField(Key);
            Assert.Equal(TMP_InputField.ContentType.Password, field.contentType);
            Assert.Equal("", field.text);
            Assert.Same(menu.Secret(Key).secretHint, field.placeholder);
            Assert.Equal("Paste your key", menu.SecretHintText(Key));
            Assert.Equal("No key saved", menu.SecretStatusText(Key));
            Assert.False(menu.RemoveShown(Key));
            // Cut loose from the rival popup, like a text field
            Assert.Null(Seams.GetComponent<InputField_Tab>(Seams.OwnerOf(field)));
            Assert.Single(Seams.Listeners(field.onValueChanged));
            Assert.Null(menu.Secret(Key).varID);
            // Not '/', which Unity's Transform.Find reads as a path
            Assert.Equal("ModMenuSecret_Mod A\\key", Seams.NameOf(menu.SecretRow(Key)));
        }

        /// <summary>
        /// Remove is a copy of the game's button (the menu's Reset to Defaults), so it looks and acts like one:
        /// its own click only, a label at the menu's text size that doesn't follow language changes, as it turns
        /// into Undo.
        /// </summary>
        [Fact]
        public void RemoveIsACopyOfTheGamesButton()
        {
            TestGame.SaveSecret(Key, LongKey);
            Menu menu = OpenMenu();

            UnityEngine.GameObject button = Seams.OwnerOf(menu.Secret(Key).secretButton);
            Assert.Equal(SECRET_BUTTON_OBJ_NAME, Seams.NameOf(button));
            Assert.NotNull(Seams.GetComponent<UnityEngine.UI.Image>(button));
            Assert.Single(Seams.Listeners(menu.Secret(Key).secretButton.onClick));
            Assert.Null(Seams.GetComponentInChildren<Lang_Button>(button));
            Assert.Equal(TEXT_SIZE, menu.Secret(Key).secretButtonText.fontSize);
            Assert.False(menu.Secret(Key).secretButtonText.enableAutoSizing);
            Assert.Equal("Remove", menu.SecretButtonText(Key));
        }

        /// <summary>
        /// Without the game's button to copy (another mod changed the menu), the row has no Remove button but
        /// still builds and saves what's typed.
        /// </summary>
        [Fact]
        public void WithoutTheGamesButtonTheRowStillWorks()
        {
            buttonTemplate = null;
            TestGame.SaveSecret(Key, LongKey);
            Menu menu = OpenMenu();
            Assert.Null(menu.Secret(Key).secretButton);
            Assert.Equal("Saved key ending in ...<noparse>x7Qa</noparse>", menu.SecretStatusText(Key));
            Assert.Contains(Log.Messages, m => m.Contains("has no Remove button"));

            menu.TypeSecret(Key, "sk-new-key-0000000");
            menu.Apply();

            Assert.Equal("sk-new-key-0000000", TestGame.SecretFile(Key));
        }

        /// <summary>
        /// The saved secret is never put in the field: the line under it shows its last 4 characters.
        /// </summary>
        [Fact]
        public void ASavedSecretShowsOnlyItsEnding()
        {
            TestGame.SaveSecret(Key, LongKey + "\n");
            Menu menu = OpenMenu();

            Assert.Equal("", menu.SecretField(Key).text);
            Assert.Equal("Saved key ending in ...<noparse>x7Qa</noparse>", menu.SecretStatusText(Key));
            Assert.Equal("Paste a new key to replace it", menu.SecretHintText(Key));
            Assert.True(menu.RemoveShown(Key));
            Assert.Equal("Remove", menu.SecretButtonText(Key));
        }

        /// <summary>
        /// The ending shows only for secrets of 16 characters or more; a shorter one, such as a PIN, shows only
        /// that one is saved, since its ending is most of it.
        /// </summary>
        [Theory]
        [InlineData("1234567890abcdef", "Saved key ending in ...<noparse>cdef</noparse>")]
        [InlineData("1234567890abcde", "A key is saved")]
        [InlineData("4321", "A key is saved")]
        public void OnlyALongSecretShowsItsEnding(string saved, string expected)
        {
            TestGame.SaveSecret(Key, saved);
            Menu menu = OpenMenu();

            Assert.Equal(expected, menu.SecretStatusText(Key));
        }

        [Fact]
        public void TheStatusTextsAreThere()
        {
            Assert.Equal("No key saved", SecretStatus(null, false));
            Assert.Equal("The saved key is removed when you press Apply.", SecretStatus(LongKey, true));
            // An ending that looks like a text tag is shown as written
            Assert.Equal("Saved key ending in ...<noparse><b>1</noparse>", SecretStatus("0123456789abc<b>1", false));
        }

        [Fact]
        public void ApplySavesWhatsTypedInItsOwnFileAndNotInTheSave()
        {
            Menu menu = OpenMenu();
            menu.TypeSecret(Key, "  " + LongKey + " ");

            menu.Apply();

            Assert.Equal(LongKey, TestGame.SecretFile(Key));
            Assert.Equal(LongKey, GetSecret(Key));
            Assert.True(HasSecret(Key));
            Assert.Equal(Path.Combine(TestGame.SecretDir, "Mod A", "key.key"), SecretStore.PathFor(Key));
            Assert.DoesNotContain(variables.variable, v => v.value != null && v.value.Contains("x7Qa"));
            Assert.Equal(new[] { Key }, Assert.Single(changes));
            Assert.Equal(1, Seams.PopupsClosed);
        }

        [Fact]
        public void TypingReplacesTheSavedSecret()
        {
            TestGame.SaveSecret(Key, "sk-old-key-000000");
            Menu menu = OpenMenu();
            menu.TypeSecret(Key, LongKey);

            menu.Apply();

            Assert.Equal(LongKey, TestGame.SecretFile(Key));
            Assert.Single(changes);
        }

        /// <summary>
        /// An empty field means "keep it": Apply never removes a secret because its field is empty.
        /// </summary>
        [Fact]
        public void AnEmptyFieldKeepsTheSavedSecret()
        {
            TestGame.SaveSecret(Key, LongKey);
            Menu menu = OpenMenu();
            menu.TypeSecret(Key, "typed");
            menu.TypeSecret(Key, "  ");

            menu.Apply();

            Assert.Equal(LongKey, TestGame.SecretFile(Key));
            Assert.Empty(changes);
        }

        [Fact]
        public void TypingTheSavedSecretAgainChangesNothing()
        {
            TestGame.SaveSecret(Key, LongKey);
            Menu menu = OpenMenu();
            menu.TypeSecret(Key, LongKey);

            menu.Apply();

            Assert.Empty(changes);
        }

        [Fact]
        public void RemoveThenApplyRemovesIt()
        {
            TestGame.SaveSecret(Key, LongKey);
            Menu menu = OpenMenu();

            menu.ClickRemove(Key);

            Assert.Equal("The saved key is removed when you press Apply.", menu.SecretStatusText(Key));
            Assert.Equal("Undo", menu.SecretButtonText(Key));
            Assert.Equal("Paste your key", menu.SecretHintText(Key));
            Assert.Equal(1, Seams.Deselected);
            menu.Apply();
            Assert.Null(TestGame.SecretFile(Key));
            Assert.Null(GetSecret(Key));
            Assert.Equal(new[] { Key }, Assert.Single(changes));
        }

        [Fact]
        public void CancelKeepsIt()
        {
            TestGame.SaveSecret(Key, LongKey);
            Menu menu = OpenMenu();
            menu.ClickRemove(Key);

            menu.Cancel();
            menu.Open();

            Assert.Equal(LongKey, TestGame.SecretFile(Key));
            Assert.Equal("Remove", menu.SecretButtonText(Key));
            Assert.Equal("Saved key ending in ...<noparse>x7Qa</noparse>", menu.SecretStatusText(Key));
        }

        [Fact]
        public void UndoKeepsIt()
        {
            TestGame.SaveSecret(Key, LongKey);
            Menu menu = OpenMenu();
            menu.ClickRemove(Key);

            menu.ClickRemove(Key);
            menu.Apply();

            Assert.Equal(LongKey, TestGame.SecretFile(Key));
            Assert.Empty(changes);
        }

        /// <summary>
        /// Remove empties the field; typing a new secret after it takes the removal back, and Apply saves it.
        /// </summary>
        [Fact]
        public void RemoveEmptiesTheFieldAndTypingTakesItBack()
        {
            TestGame.SaveSecret(Key, LongKey);
            Menu menu = OpenMenu();
            menu.TypeSecret(Key, "typed-before-0000");

            menu.ClickRemove(Key);
            Assert.Equal(("", ""), (menu.SecretField(Key).text, menu.Secret(Key).secretTyped));

            menu.TypeSecret(Key, "typed-after-00000");
            Assert.False(menu.Secret(Key).secretRemoving);
            Assert.Equal("Remove", menu.SecretButtonText(Key));
            menu.Apply();
            Assert.Equal("typed-after-00000", TestGame.SecretFile(Key));
        }

        /// <summary>
        /// What was typed and not applied is gone when the menu opens again.
        /// </summary>
        [Fact]
        public void ReopeningEmptiesTheField()
        {
            Menu menu = OpenMenu();
            menu.TypeSecret(Key, LongKey);

            menu.Cancel();
            menu.Open();

            Assert.Equal(("", ""), (menu.SecretField(Key).text, menu.Secret(Key).secretTyped));
            Assert.Null(TestGame.SecretFile(Key));
        }

        /// <summary>
        /// A secret has no varID, so another mod reading the open menu's values never gets it.
        /// </summary>
        [Fact]
        public void MenuValueNeverGivesTheSecret()
        {
            Menu menu = OpenMenu();
            menu.TypeSecret(Key, LongKey);

            Assert.Null(Seams.MenuValue(Key));
            Assert.Null(Seams.MenuValue(null));
        }

        [Theory]
        [InlineData("key")]
        [InlineData("Tel AI Mods/anthropic")]
        [InlineData("my-mod_1.2/key.v2")]
        public void ValidIDs(string secretID) => Assert.True(SecretStore.IsValidID(secretID));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("../key")]
        [InlineData("..")]
        [InlineData("a/b/c")]
        [InlineData("/key")]
        [InlineData("mod/")]
        [InlineData("mod\\key")]
        [InlineData("C:/key")]
        [InlineData(" key")]
        [InlineData("key.")]
        [InlineData("key?")]
        public void InvalidIDs(string secretID)
        {
            Assert.False(SecretStore.IsValidID(secretID));
            Assert.Null(GetSecret(secretID));
            Assert.False(SecretStore.Set(secretID, "value"));
        }

        /// <summary>
        /// A row whose secretID isn't valid is left out with an error; the mod's other rows still build.
        /// </summary>
        [Fact]
        public void ARowWithABadIDIsLeftOut()
        {
            Menu menu = OpenMenu("""
                { "type": "secret", "secretID": "../outside", "labelID": "TEST__MOTTO" },
                { "type": "secret", "labelID": "TEST__MOTTO" },
                { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" }
                """);

            Assert.Equal(new[] { "ModMenuText_Mod A", "ModMenuCheckbox_A_Loud" }, menu.RowNames);
            Assert.Equal(2, Log.Messages.FindAll(m => m.Contains("secretID")).Count);
        }

        /// <summary>
        /// A text field with the removed password option is left out with an error, so its value is never shown
        /// unmasked; the mod's other rows still build.
        /// </summary>
        [Fact]
        public void ARemovedPasswordFieldIsLeftOut()
        {
            Menu menu = OpenMenu("""
                { "type": "input", "varID": "A_Key", "labelID": "TEST__MOTTO", "password": true },
                { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" }
                """);

            Assert.Equal(new[] { "ModMenuText_Mod A", "ModMenuCheckbox_A_Loud" }, menu.RowNames);
            Assert.Contains(Log.Messages, m => m == "[ModMenus] The text field A_Key uses password, which was removed in ModMenus 5.0.0, so it's left out. Use \"type\": \"secret\" instead.");
        }

        [Fact]
        public void ARowCanBeShownOnlyWhileAnotherSettingIsOn()
        {
            Menu menu = OpenMenu("""
                { "type": "checkbox", "varID": "A_On", "labelID": "TEST__LOUD" },
                { "type": "secret", "secretID": "Mod A/key", "labelID": "TEST__MOTTO", "enabledIf": "A_On" }
                """);
            Assert.False(Seams.IsActive(menu.SecretRow(Key)));

            menu.Click("A_On");

            Assert.True(Seams.IsActive(menu.SecretRow(Key)));
        }

        /// <summary>
        /// A file changed on disk, by another mod say, is read again.
        /// </summary>
        [Fact]
        public void GetSecretReadsAChangedFileAgain()
        {
            TestGame.SaveSecret(Key, "first-key-000000");
            Assert.Equal("first-key-000000", GetSecret(Key));

            TestGame.SaveSecret(Key, "second-key-00000");
            File.SetLastWriteTimeUtc(SecretStore.PathFor(Key), DateTime.UtcNow.AddMinutes(1));

            Assert.Equal("second-key-00000", GetSecret(Key));
        }

        /// <summary>
        /// Mods hear once, after everything is saved, which secrets changed; each ID once.
        /// </summary>
        [Fact]
        public void SecretsChangedNamesEachChangedSecretOnce()
        {
            TestGame.SaveSecret("Mod A/kept", LongKey);
            Menu menu = OpenMenu("""
                { "type": "secret", "secretID": "Mod A/key", "labelID": "TEST__MOTTO" },
                { "type": "secret", "secretID": "Mod A/key", "labelID": "TEST__MOTTO" },
                { "type": "secret", "secretID": "Mod A/kept", "labelID": "TEST__MOTTO" },
                { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" }
                """);
            Assert.Equal(2, menu.Items.FindAll(item => item.secretID == Key).Count);
            menu.Items.FindAll(item => item.secretID == Key).ForEach(item => item.secretTyped = LongKey);
            string savedWhenTold = null;
            SecretsChanged += _ => savedWhenTold = TestGame.Saved("A_Loud");

            menu.Apply();

            Assert.Equal(new[] { Key }, Assert.Single(changes));
            Assert.Equal("0", savedWhenTold);
        }

        /// <summary>
        /// A mod's handler that fails is logged; the others are still told, and the menu still closes.
        /// </summary>
        [Fact]
        public void AFailingHandlerDoesntStopTheOthers()
        {
            Action<string[]> failing = _ => throw new InvalidOperationException("broken mod");
            SecretsChanged -= OnSecretsChanged;
            SecretsChanged += failing;
            SecretsChanged += OnSecretsChanged;
            try
            {
                Menu menu = OpenMenu();
                menu.TypeSecret(Key, LongKey);

                menu.Apply();

                Assert.Single(changes);
                Assert.Contains(Log.Messages, m => m.Contains("SecretsChanged handler failed") && m.Contains("broken mod"));
                Assert.Equal(1, Seams.PopupsClosed);
            }
            finally
            {
                SecretsChanged -= failing;
            }
        }

        /// <summary>
        /// If the file can't be written, the old secret is kept, the problem is logged, and Apply still saves the
        /// other settings and closes.
        /// </summary>
        [Fact]
        public void AFileThatCantBeWrittenKeepsTheOldSecret()
        {
            Directory.CreateDirectory(SecretStore.PathFor(Key));
            Menu menu = OpenMenu("""
                { "type": "secret", "secretID": "Mod A/key", "labelID": "TEST__MOTTO" },
                { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" }
                """);
            menu.TypeSecret(Key, LongKey);
            menu.Click("A_Loud");

            menu.Apply();

            Assert.Contains(Log.Messages, m => m.Contains("couldn't be saved"));
            Assert.Empty(changes);
            Assert.Equal("1", TestGame.Saved("A_Loud"));
            Assert.Equal(1, Seams.PopupsClosed);
        }

        /// <summary>
        /// A player updating AI Fan Chatter keeps the keys it saved before: its rows read the same files, so
        /// nothing needs typing again.
        /// </summary>
        [Fact]
        public void AIFanChattersSavedKeysShowInItsRows()
        {
            TestGame.AddShippedMod("AI Fan Chatter");
            File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(TestGame.SecretDir, "Tel AI Mods")).FullName, "openai.key"), "sk-proj-saved-before-AbCd");
            Menu menu = TestGame.BuildMenu();
            menu.Open();

            Assert.Equal("Saved key ending in ...<noparse>AbCd</noparse>", menu.SecretStatusText("Tel AI Mods/openai"));
            Assert.Equal("No key saved", menu.SecretStatusText("Tel AI Mods/anthropic"));
            Assert.Equal("No key saved", menu.SecretStatusText("Tel AI Mods/compatible"));
        }

        [Fact]
        public void TheShippedTextsAreThere()
        {
            Assert.Equal("Paste your key", ShownText(SECRET_HINT_NEW_LABEL));
            Assert.Equal("Paste a new key to replace it", ShownText(SECRET_HINT_REPLACE_LABEL));
            Assert.Equal("Remove", ShownText(SECRET_REMOVE_LABEL));
            Assert.Equal("Undo", ShownText(SECRET_UNDO_LABEL));
            Assert.Equal("No key saved", ShownText(SECRET_NONE_LABEL));
            Assert.Equal("A key is saved", ShownText(SECRET_SAVED_LABEL));
            Assert.Equal("Saved key ending in ...{0}", ShownText(SECRET_SAVED_ENDING_LABEL));
            Assert.Equal("The saved key is removed when you press Apply.", ShownText(SECRET_REMOVING_LABEL));
        }
    }
}
