using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace HarmonyChecker.Tests
{
    /// <summary>
    /// The mod's constants.json changes the Mods label to "Mods [IM-HI not installed]" in every language. With
    /// the mod's DLL loaded (so IM-HI is installed), the button gets back the label the game would show without
    /// the mod, whether the mod loads before the menu starts or after, and the menu asks for the status badge.
    /// </summary>
    public class LabelTests
    {
        public LabelTests()
        {
            TestGame.Reset();
            TestGame.LoadModConstants();
        }

        private static void AssertInstalled(Menu menu, string label = TestGame.VanillaText)
        {
            Assert.Equal(TestGame.VanillaConstant, menu.ModsLangButton.Constant);
            Assert.Equal(label, Language.Data[TestGame.VanillaConstant]);
            Assert.Equal(label, TestGame.TextOf(menu.Text));
        }

        /// <summary>
        /// The button is left as drawn: with the mod's constants loaded, it says IM-HI isn't installed.
        /// </summary>
        private static void AssertUnchanged(Menu menu)
        {
            Assert.Equal(TestGame.VanillaConstant, menu.ModsLangButton.Constant);
            Assert.Equal(TestGame.NotInstalledText, TestGame.TextOf(menu.Text));
            Assert.Empty(Seams.Badges);
        }

        /// <summary>
        /// Mods loaded before the menu started: the Start postfix restores the label.
        /// </summary>
        [Fact]
        public void MenuStart_RestoresLabel()
        {
            Menu menu = TestGame.BuildMenu();
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(controller);

            AssertInstalled(menu);
        }

        /// <summary>
        /// On Steam the menu can start before Workshop mods finish loading, and the button then says
        /// "Mods [IM-HI not installed]" from the mod's constants. When the spinner stops, the mod finds the
        /// menu through the camera and restores the label, redrawing the text the game already drew.
        /// </summary>
        [Fact]
        public void SpinnerStop_RestoresLabelDrawnBeforeModsLoaded()
        {
            Menu menu = TestGame.BuildMenu();
            Assert.Equal(TestGame.NotInstalledText, TestGame.TextOf(menu.Text));

            Seams.StopSpinnerPostfix();

            AssertInstalled(menu);
        }

        [Fact]
        public void BothRefreshes_LeaveTheSameLabel()
        {
            Menu menu = TestGame.BuildMenu();
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(controller);
            Seams.StopSpinnerPostfix();
            Seams.StopSpinnerPostfix();

            AssertInstalled(menu);
        }

        /// <summary>
        /// In another language the label is that language's, not English: the game reads English first, then
        /// the language's file, then the mods'.
        /// </summary>
        [Theory]
        [InlineData("Languages/jp/JSON/Constants/constants.json", "MOD")]
        [InlineData("Languages/ru/JSON/Constants/constants.json", "Моды")]
        public void OtherLanguage_GetsItsOwnLabel(string languageFile, string label)
        {
            TestGame.AddFileBeforeOwn(languageFile, TestGame.Constants((TestGame.VanillaConstant, label)));
            TestGame.LoadLanguage();
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            AssertInstalled(menu, label);
        }

        /// <summary>
        /// Brazilian Portuguese has no Mods label of its own, so the game shows the English one.
        /// </summary>
        [Fact]
        public void LanguageWithoutModsLabel_GetsEnglish()
        {
            TestGame.AddFileBeforeOwn("Languages/ptbr/JSON/Constants/constants.json", TestGame.Constants(("SETTINGS", "Configurações")));
            TestGame.AddFileBeforeOwn("Languages/ptbr/JSON/Constants/missing.json", null);
            TestGame.LoadLanguage();
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            AssertInstalled(menu);
        }

        /// <summary>
        /// A translation mod that loads before this mod is the label the game would show without this mod.
        /// </summary>
        [Fact]
        public void TranslationModBefore_GetsItsLabel()
        {
            TestGame.AddFileBeforeOwn("Mods/Traducao/JSON/Constants/constants.json", TestGame.Constants((TestGame.VanillaConstant, "Modificações")));
            TestGame.LoadLanguage();
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            AssertInstalled(menu, "Modificações");
        }

        /// <summary>
        /// A translation mod that loads after this mod already shows its label, which is kept.
        /// </summary>
        [Fact]
        public void TranslationModAfter_IsKept()
        {
            TestGame.ConstantsFiles.Add(new("Mods/Traducao/JSON/Constants/constants.json", TestGame.Constants((TestGame.VanillaConstant, "Modificações"))));
            TestGame.LoadLanguage();
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            AssertInstalled(menu, "Modificações");
        }

        /// <summary>
        /// Changing language reloads the table, with this mod's label again; the reload's postfix puts the
        /// game's label back and redraws it.
        /// </summary>
        [Fact]
        public void LanguageReload_RestoresLabelAgain()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.StopSpinnerPostfix();
            TestGame.AddFileBeforeOwn("Languages/jp/JSON/Constants/constants.json", TestGame.Constants((TestGame.VanillaConstant, "MOD")));

            TestGame.LoadLanguage(menu.ModsLangButton);
            Assert.Equal(TestGame.NotInstalledText, TestGame.TextOf(menu.Text));
            Seams.LoadPostfix();

            AssertInstalled(menu, "MOD");
        }

        /// <summary>
        /// Outside the main menu the label in the language table is still put back, ready for the menu.
        /// </summary>
        [Fact]
        public void LanguageReloadOutsideMenu_RestoresTable()
        {
            TestGame.LoadLanguage();

            Seams.LoadPostfix();

            Assert.Equal(TestGame.VanillaText, Language.Data[TestGame.VanillaConstant]);
            Assert.Empty(Seams.Badges);
        }

        /// <summary>
        /// If the constants files can't be read, the error is logged and the menu still gets its badge.
        /// </summary>
        [Fact]
        public void FilesUnreadable_LogsAndStillShowsBadge()
        {
            ModsLabel.ReadFile = path => throw new System.IO.IOException("locked");
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            Assert.Contains(Log.Messages, m => m.Contains("[Harmony Checker] Couldn't restore the Mods label") && m.Contains("locked"));
            Assert.Equal(TestGame.NotInstalledText, TestGame.TextOf(menu.Text));
            Assert.Single(Seams.Badges);
        }

        /// <summary>
        /// The game's Lang_Button draws to whichever text component the label has.
        /// </summary>
        [Theory]
        [InlineData(typeof(TextMeshProUGUI))]
        [InlineData(typeof(Text))]
        [InlineData(typeof(TextMesh))]
        public void Label_DrawnOnAnyTextComponent(Type textType)
        {
            Menu menu = TestGame.BuildMenu(textType);

            Seams.StopSpinnerPostfix();

            AssertInstalled(menu);
        }

        [Fact]
        public void OtherButtons_Untouched()
        {
            Menu menu = TestGame.BuildMenu();
            Lang_Button credits = TestGame.AddButton(menu.Container, "Credits", "SETTINGS", typeof(TextMeshProUGUI));

            Seams.StopSpinnerPostfix();

            foreach (Lang_Button other in new[] { menu.SettingsLangButton, credits })
            {
                Assert.Equal("SETTINGS", other.Constant);
                Assert.Equal("Settings", TestGame.TextOf(Seams.GetComponent<TextMeshProUGUI>(other)));
            }
            AssertInstalled(menu);
        }

        /// <summary>
        /// The badge goes on the Mods button, with the tick when IM-HI is up to date and the "!" when not.
        /// </summary>
        [Fact]
        public void Badge_ShowsStatus()
        {
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();
            IMHIUpdate.InstalledVersion = () => new Version(1, 0, 0);
            Seams.StopSpinnerPostfix();

            Assert.Equal(new[] { (Seams.TransformOf(menu.ModsButton), false, IMHIUpdate.MIN_VERSION), (Seams.TransformOf(menu.ModsButton), true, new Version(1, 0, 0)) },
                Seams.Badges);
        }

        // Outside the main menu (or while it's being built), the menu is left alone rather than throwing.

        [Fact]
        public void NoMainCamera_DoesNothing()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.MainCamera = null;

            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }

        [Fact]
        public void CameraWithoutMainScript_DoesNothing()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.RemoveComponent(menu.Main);

            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }

        [Fact]
        public void NoDataObject_DoesNothing()
        {
            Menu menu = TestGame.BuildMenu();
            menu.Main.Data = null;

            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }

        /// <summary>
        /// In a game (not the main menu), Data has no menu controller.
        /// </summary>
        [Fact]
        public void NoMenuController_DoesNothing()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.RemoveComponent(menu.Controller);

            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }

        [Fact]
        public void NoMainContainer_DoesNothing()
        {
            Menu menu = TestGame.BuildMenu();
            menu.Controller.Main_Container = null;
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(controller);
            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }

        [Fact]
        public void NoModsButton_DoesNothing()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.Remove(menu.ModsButton);
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(controller);
            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }

        /// <summary>
        /// A Mods button without a label still gets the badge.
        /// </summary>
        [Fact]
        public void ModsButtonWithoutLabel_StillGetsBadge()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.Remove(menu.Label);
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(controller);

            Assert.Equal(TestGame.NotInstalledText, TestGame.TextOf(menu.Text));
            Assert.Single(Seams.Badges);
        }

        /// <summary>
        /// Another mod's label on the Mods button is kept by every refresh. The update button
        /// still appears when IM-HI is out of date.
        /// </summary>
        [Fact]
        public void AnotherModsLabel_IsKept()
        {
            IMHIUpdate.InstalledVersion = () => new Version(1, 0, 0);
            Language.Data["OTHER_MOD_MODS"] = "Mods (12)";
            Menu menu = TestGame.BuildMenu();
            menu.ModsLangButton.Constant = "OTHER_MOD_MODS";
            Seams.Texts[menu.Text] = "Mods (12)";

            Seams.StartPostfix(menu.Controller);
            Seams.StopSpinnerPostfix();
            Seams.LoadPostfix();

            Assert.Equal("OTHER_MOD_MODS", menu.ModsLangButton.Constant);
            Assert.Equal("Mods (12)", TestGame.TextOf(menu.Text));
            Assert.NotNull(TestGame.UpdateButton(menu));
        }

        /// <summary>
        /// The label stays the game's whether IM-HI is up to date or not; only the badge changes.
        /// </summary>
        [Fact]
        public void Outdated_KeepsGameLabel()
        {
            Menu menu = TestGame.BuildMenu();

            IMHIUpdate.InstalledVersion = () => new Version(1, 0, 0);
            Seams.StopSpinnerPostfix();

            AssertInstalled(menu);
        }

        /// <summary>
        /// An error while updating the menu stays in the postfix and is logged, so the game's method and other
        /// mods' patches on it still finish. Here reading the installed version fails.
        /// </summary>
        [Fact]
        public void Error_StaysInEveryPostfix()
        {
            Menu menu = TestGame.BuildMenu();
            IMHIUpdate.InstalledVersion = () => throw new InvalidOperationException("broken");

            Seams.StartPostfix(menu.Controller);
            Seams.StopSpinnerPostfix();
            Seams.LoadPostfix();

            Assert.Equal(3, Log.Messages.FindAll(m => m.Contains("[Harmony Checker] Couldn't update the main menu") && m.Contains("broken")).Count);
            AssertUnchanged(menu);
        }
    }

    /// <summary>
    /// Which file's Mods label the game would show without this mod.
    /// </summary>
    public class ModsLabelTests
    {
        private static string Label(string text) => TestGame.Constants((TestGame.VanillaConstant, text));

        private static string Without(params (string path, string text)[] files) =>
            ModsLabel.WithoutThisMod(Array.ConvertAll(files, f => f.path), path => path.StartsWith("own"),
                path => Array.Find(files, f => f.path == path).text);

        [Fact]
        public void OwnLast_GivesTheOneBefore() =>
            Assert.Equal("MOD", Without(("en", Label("Mods")), ("jp", Label("MOD")), ("own", Label("Mods [IM-HI not installed]"))));

        [Fact]
        public void OtherLast_GivesNull() =>
            Assert.Null(Without(("en", Label("Mods")), ("own", Label("x")), ("other", Label("Mods (12)"))));

        [Fact]
        public void OwnNotSettingIt_GivesNull() =>
            Assert.Null(Without(("en", Label("Mods")), ("own", TestGame.Constants(("IMHI_UPDATE", "u")))));

        [Fact]
        public void MissingFiles_AreSkipped() =>
            Assert.Equal("Mods", Without(("en", Label("Mods")), ("gone", null), ("own", Label("x"))));

        [Fact]
        public void NothingBefore_GivesNull() => Assert.Null(Without(("own", Label("x"))));

        [Theory]
        [InlineData(@"C:\Steam\workshop\3102983863\JSON\Constants\constants.json", true)]
        [InlineData("C:/Steam/workshop/3102983863/JSON/Constants/constants.json", true)]
        [InlineData(@"c:\steam\WORKSHOP\3102983863\x.json", true)]
        [InlineData(@"C:\Steam\workshop\31029838630\x.json", false)]
        [InlineData(@"C:\Steam\workshop\other\x.json", false)]
        public void IsUnder_MatchesFolderOnly(string path, bool under) =>
            Assert.Equal(under, ModsLabel.IsUnder(path, new[] { null, "", @"C:\Steam\workshop\3102983863\" }));
    }
}
