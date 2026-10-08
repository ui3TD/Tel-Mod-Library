using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace HarmonyChecker.Tests
{
    /// <summary>
    /// When the installed IM-HI is older than the version the mod asks for, a yellow copy of the Mods button
    /// appears below it and opens the IM-HI releases page.
    /// </summary>
    public class UpdateButtonTests
    {
        private static readonly Version Old = new(1, 1, 0);
        private const string OutdatedText = "Update IM-HI to 1.2.0";

        public UpdateButtonTests()
        {
            TestGame.Reset();
            TestGame.LoadModConstants();
        }

        private static GameObject Refresh(Version installed, Type textType = null)
        {
            IMHIUpdate.InstalledVersion = () => installed;
            Menu menu = TestGame.BuildMenu(textType);
            Seams.StopSpinnerPostfix();
            return TestGame.UpdateButton(menu);
        }

        /// <summary>
        /// The text component the button's label draws to.
        /// </summary>
        private static Component LabelText(GameObject button)
        {
            GameObject label = Seams.ChildNamed(button, "Text");
            return (Component)Seams.GetComponent<TMP_Text>(label) ?? (Component)Seams.GetComponent<Text>(label) ?? Seams.GetComponent<TextMesh>(label);
        }

        [Fact]
        public void Outdated_AddsButtonBelowMods()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            Assert.Equal(new[] { "Settings", "Mods", IMHIUpdate.BUTTON_NAME, "Quit" }, Seams.ChildNames(menu.Container));
            GameObject button = TestGame.UpdateButton(menu);
            Assert.Equal(OutdatedText, TestGame.TextOf(LabelText(button)));
            Assert.Equal(IMHIUpdate.DRAWN_LABEL, Seams.GetComponentInChildren<Lang_Button>(button).Constant);
        }

        [Fact]
        public void Outdated_ButtonIsYellow()
        {
            GameObject button = Refresh(Old);

            Image background = Seams.GetComponent<Image>(button);
            Assert.Equal((Color)mainScript.gold32, Seams.Colors[background]);
            Assert.Null(Seams.Sprites[background]);
        }

        /// <summary>
        /// Dark text, readable on the yellow button.
        /// </summary>
        [Theory]
        [InlineData(typeof(TextMeshProUGUI))]
        [InlineData(typeof(Text))]
        public void Outdated_ButtonTextIsDark(Type textType)
        {
            GameObject button = Refresh(Old, textType);

            Assert.Equal((Color)mainScript.blackGold32, Seams.Colors[LabelText(button)]);
        }

        /// <summary>
        /// The Mods label, "Mods [IM-HI installed]", is the longest text known to fit the button.
        /// </summary>
        [Fact]
        public void Label_NoLongerThanModsLabel()
        {
            string label = IMHIUpdate.FALLBACK_LABEL.Replace("@2", IMHIUpdate.MIN_VERSION.ToString());

            Assert.True(label.Length <= "Mods [IM-HI installed]".Length, label);
        }

        /// <summary>
        /// The game's TextMesh has no colour setter, so a TextMesh label keeps its colour but still shows the text.
        /// </summary>
        [Fact]
        public void TextMeshLabel_ShowsText()
        {
            GameObject button = Refresh(Old, typeof(TextMesh));

            Assert.Equal(OutdatedText, TestGame.TextOf(LabelText(button)));
        }

        [Fact]
        public void Click_OpensReleasesPage()
        {
            GameObject button = Refresh(Old);
            Button click = Seams.GetComponent<Button>(button);

            Seams.Click(click);

            Assert.Equal(new[] { "https://github.com/ui3TD/IM-HarmonyIntegration/releases/latest" }, Seams.OpenedUrls);
        }

        /// <summary>
        /// The copy starts with the Mods button's click handler; it's replaced, not added to.
        /// </summary>
        [Fact]
        public void Click_DoesNotOpenModsList()
        {
            GameObject button = Refresh(Old);
            Button click = Seams.GetComponent<Button>(button);

            List<Delegate> listeners = Seams.Listeners(click.onClick);
            Assert.Single(listeners);
            Assert.NotEqual(nameof(Menu.OpenModsList), listeners[0].Method.Name);
            Assert.Equal(0, click.onClick.GetPersistentEventCount());
        }

        /// <summary>
        /// The Mods label says IM-HI is out of date; the Mods button keeps its look and click.
        /// </summary>
        [Fact]
        public void Outdated_ModsLabelSaysOutOfDate()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            Assert.Equal(IMHIUpdate.MODS_LABEL, menu.ModsLangButton.Constant);
            Assert.Equal("Mods [IM-HI out-of-date]", TestGame.TextOf(menu.Text));
            Assert.False(Seams.Colors.ContainsKey(menu.Text));
            Assert.False(Seams.Colors.ContainsKey(menu.ModsBackground));
            Assert.Equal(TestGame.PurpleSprite, Seams.Sprites[menu.ModsBackground]);
            Assert.Equal(TestGame.ModsTints, Seams.Tints[menu.ModsClick]);
            List<Delegate> listeners = Seams.Listeners(menu.ModsClick.onClick);
            Assert.Equal(nameof(Menu.OpenModsList), Assert.Single(listeners).Method.Name);
        }

        [Fact]
        public void EveryRefresh_KeepsOneButton()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            Menu menu = TestGame.BuildMenu();
            MainMenu_Buttons_Controller controller = menu.Controller;

            Seams.StartPostfix(ref controller);
            Seams.StopSpinnerPostfix();
            Seams.StopSpinnerPostfix();

            Assert.Single(Seams.Instantiated);
            Assert.Single(Seams.ChildNames(menu.Container), IMHIUpdate.BUTTON_NAME);
        }

        [Theory]
        [InlineData("1.2.0")]
        [InlineData("1.2.1")]
        [InlineData("2.0")]
        public void UpToDate_NoButton(string installed)
        {
            IMHIUpdate.InstalledVersion = () => Version.Parse(installed);
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            Assert.Null(TestGame.UpdateButton(menu));
            Assert.Empty(Seams.Instantiated);
            Assert.Equal("Mods [IM-HI installed]", TestGame.TextOf(menu.Text));
        }

        /// <summary>
        /// The copy's tints start from white, so its yellow isn't darkened by the Mods button's purple, and each
        /// state still changes the colour as much as on the Mods button.
        /// </summary>
        [Fact]
        public void Outdated_TintKeepsYellowAndHover()
        {
            GameObject button = Refresh(Old);

            ColorBlock tints = Seams.Tints[Seams.GetComponent<Button>(button)];
            AssertColor(Color.white, tints.normalColor);
            AssertColor(new Color(1.2f, 1.2f, 1f), tints.highlightedColor);
            AssertColor(new Color(0.5f, 0.5f, 0.5f), tints.pressedColor);
            AssertColor(Color.white, tints.selectedColor);
            AssertColor(new Color(1f, 1f, 1f, 0.4f), tints.disabledColor);
        }

        /// <summary>
        /// Tints can't go above white, so a brighter hover is capped there.
        /// </summary>
        private static void AssertColor(Color expected, Color actual)
        {
            for (int i = 0; i < 4; i++)
                Assert.Equal(Mathf.Clamp01(expected[i]), actual[i], 3);
        }

        [Fact]
        public void Relative_BlackNormalLeavesWhite() =>
            AssertColor(Color.white, IMHIUpdate.Relative(new Color(0.3f, 0.3f, 0.3f), new Color(0f, 0f, 0f, 0f)));

        /// <summary>
        /// If the installed version can't be read, nobody is told to update.
        /// </summary>
        [Fact]
        public void UnknownVersion_NoButton()
        {
            Assert.Null(Refresh(null));
            Assert.Empty(Seams.Instantiated);
        }

        [Theory]
        [InlineData("1.0.0")]
        [InlineData("1.1.0")]
        [InlineData("1.1.9")]
        public void OlderVersions_AreOutdated(string installed) => Assert.True(IMHIUpdate.IsOutdated(Version.Parse(installed)));

        /// <summary>
        /// Without the mod's constants, the button still says what it's for.
        /// </summary>
        [Fact]
        public void ConstantsNotLoaded_UsesFallbackText()
        {
            TestGame.Reset();

            GameObject button = Refresh(Old);

            Assert.Equal(OutdatedText, TestGame.TextOf(LabelText(button)));
        }

        /// <summary>
        /// Changing language reloads the language table without the filled-in label; the button keeps its text
        /// rather than showing the placeholders.
        /// </summary>
        [Fact]
        public void LanguageReset_KeepsVersions()
        {
            GameObject button = Refresh(Old);

            Language.Data.Clear();
            TestGame.LoadModConstants();
            Seams.ResetText(Seams.GetComponentInChildren<Lang_Button>(button));

            Assert.Equal(OutdatedText, TestGame.TextOf(LabelText(button)));
        }

        /// <summary>
        /// A Mods button that can't be clicked isn't copied into a dead button.
        /// </summary>
        [Fact]
        public void ModsButtonNotClickable_RemovesCopy()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            Menu menu = TestGame.BuildMenu();
            Seams.RemoveComponent(menu.ModsClick);

            Seams.StopSpinnerPostfix();

            Assert.Null(TestGame.UpdateButton(menu));
            Assert.False(Seams.InScene(Assert.Single(Seams.Destroyed)));
            Assert.Equal("Mods [IM-HI out-of-date]", TestGame.TextOf(menu.Text));
        }
    }

    /// <summary>
    /// The installed IM-HI version is read from BepInEx by name, since the mod doesn't reference BepInEx.
    /// </summary>
    public class InstalledVersionTests
    {
        private static readonly Assembly[] WithBepInEx = { typeof(IMHIUpdate).Assembly, typeof(BepInEx.Bootstrap.Chainloader).Assembly };

        public InstalledVersionTests() => BepInEx.Bootstrap.Chainloader.PluginInfos.Clear();

        private static void Install(string guid, object version) =>
            BepInEx.Bootstrap.Chainloader.PluginInfos[guid] = new BepInEx.PluginInfo { Metadata = new BepInEx.BepInPlugin { Version = version } };

        [Fact]
        public void ReadsIMHIVersion()
        {
            Install("some.other.plugin", new Version(9, 9, 9));
            Install(IMHIUpdate.IMHI_GUID, new Version(1, 1, 0));

            Assert.Equal(new Version(1, 1, 0), IMHIUpdate.ReadInstalledVersion(WithBepInEx));
        }

        /// <summary>
        /// The mod's default lookup searches every loaded assembly.
        /// </summary>
        [Fact]
        public void DefaultLookup_SearchesLoadedAssemblies()
        {
            Install(IMHIUpdate.IMHI_GUID, new Version(1, 0, 0));

            Assert.Equal(new Version(1, 0, 0), Seams.DefaultInstalledVersion());
        }

        /// <summary>
        /// A version type other than System.Version (BepInEx 6 uses SemVer) is read from its text.
        /// </summary>
        [Fact]
        public void ReadsOtherVersionTypes()
        {
            Install(IMHIUpdate.IMHI_GUID, "1.1.0");

            Assert.Equal(new Version(1, 1, 0), IMHIUpdate.ReadInstalledVersion(WithBepInEx));
        }

        [Fact]
        public void NoBepInEx_Unknown() => Assert.Null(IMHIUpdate.ReadInstalledVersion(new[] { typeof(IMHIUpdate).Assembly }));

        [Fact]
        public void IMHINotLoaded_Unknown()
        {
            Install("some.other.plugin", new Version(1, 0, 0));

            Assert.Null(IMHIUpdate.ReadInstalledVersion(WithBepInEx));
        }

        [Fact]
        public void NoMetadata_Unknown()
        {
            BepInEx.Bootstrap.Chainloader.PluginInfos[IMHIUpdate.IMHI_GUID] = new BepInEx.PluginInfo();

            Assert.Null(IMHIUpdate.ReadInstalledVersion(WithBepInEx));
        }

        [Fact]
        public void UnreadableVersion_Unknown()
        {
            Install(IMHIUpdate.IMHI_GUID, "not a version");

            Assert.Null(IMHIUpdate.ReadInstalledVersion(WithBepInEx));
        }

        [Fact]
        public void ThrowingMetadata_Unknown()
        {
            BepInEx.Bootstrap.Chainloader.PluginInfos[IMHIUpdate.IMHI_GUID] = new BepInEx.PluginInfo { Throws = true };

            Assert.Null(IMHIUpdate.ReadInstalledVersion(WithBepInEx));
        }
    }
}

// Stand-ins for the BepInEx 5 members the mod reads: Chainloader.PluginInfos[guid].Metadata.Version.
namespace BepInEx.Bootstrap
{
    public static class Chainloader
    {
        public static System.Collections.Generic.Dictionary<string, PluginInfo> PluginInfos { get; } = new();
    }
}

namespace BepInEx
{
    public class PluginInfo
    {
        public bool Throws;
        private BepInPlugin metadata;

        public BepInPlugin Metadata
        {
            get => Throws ? throw new System.InvalidOperationException() : metadata;
            set => metadata = value;
        }
    }

    public class BepInPlugin
    {
        public object Version { get; set; }
    }
}
