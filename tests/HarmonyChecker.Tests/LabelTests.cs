using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace HarmonyChecker.Tests
{
    /// <summary>
    /// With the mod loaded (so IM-HI is installed), the main menu's Mods button reads "Mods [IM-HI installed]",
    /// whether the mod loads before the menu starts or after.
    /// </summary>
    public class LabelTests
    {
        private const string InstalledText = "Mods [IM-HI installed]";

        public LabelTests()
        {
            TestGame.Reset();
            TestGame.LoadModConstants();
        }

        private static void AssertInstalled(Menu menu)
        {
            Assert.Equal(HarmonyCheckerStatus.BUTTON_LABEL, menu.ModsLangButton.Constant);
            Assert.Equal(InstalledText, TestGame.TextOf(menu.Text));
        }

        /// <summary>
        /// The button is left as drawn: with the mod's constants loaded, it says IM-HI isn't installed.
        /// </summary>
        private static void AssertUnchanged(Menu menu)
        {
            Assert.Equal(TestGame.VanillaConstant, menu.ModsLangButton.Constant);
            Assert.Equal(TestGame.NotInstalledText, TestGame.TextOf(menu.Text));
        }

        /// <summary>
        /// Mods loaded before the menu started: the Start postfix relabels the button.
        /// </summary>
        [Fact]
        public void MenuStart_LabelsModsButton()
        {
            Menu menu = TestGame.BuildMenu();
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(ref controller);

            AssertInstalled(menu);
        }

        /// <summary>
        /// On Steam the menu can start before Workshop mods finish loading, and the button then says
        /// "Mods [IM-HI not installed]" from the mod's constants. When the spinner stops, the mod finds the
        /// menu through the camera and relabels the button, redrawing the text the game already drew.
        /// </summary>
        [Fact]
        public void SpinnerStop_LabelsModsButtonDrawnBeforeModsLoaded()
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

            Seams.StartPostfix(ref controller);
            Seams.StopSpinnerPostfix();
            Seams.StopSpinnerPostfix();

            AssertInstalled(menu);
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
        /// If the mod's constants aren't in the language table yet, the game's ResetText keeps the old text;
        /// the button still holds the new constant, so the game's next language reset draws it.
        /// </summary>
        [Fact]
        public void ConstantsNotLoaded_KeepsTextUntilLanguageReset()
        {
            TestGame.Reset();
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            Assert.Equal(HarmonyCheckerStatus.BUTTON_LABEL, menu.ModsLangButton.Constant);
            Assert.Equal(TestGame.VanillaText, TestGame.TextOf(menu.Text));

            TestGame.LoadModConstants();
            Seams.ResetText(menu.ModsLangButton);
            AssertInstalled(menu);
        }

        // Outside the main menu (or while it's being built), the mod does nothing rather than throwing.

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

            Seams.StartPostfix(ref controller);
            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }

        [Fact]
        public void NoModsButton_DoesNothing()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.Remove(menu.ModsButton);
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(ref controller);
            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }

        [Fact]
        public void ModsButtonWithoutLabel_DoesNothing()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.Remove(menu.Label);
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(ref controller);
            Seams.StopSpinnerPostfix();

            AssertUnchanged(menu);
        }
    }
}
