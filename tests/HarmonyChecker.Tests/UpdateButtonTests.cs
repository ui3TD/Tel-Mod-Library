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
    /// When the installed IM-HI is older than the version the mod asks for, a gold button appears below the Mods
    /// button and opens the IM-HI releases page. Since 1.4.1 it's built from the game's button parts, as the
    /// menu's own buttons are, instead of being copied from the Mods button. The tests start without the gold
    /// picture (the fallback); the WithPicture tests give it one.
    /// </summary>
    public class UpdateButtonTests
    {
        private static readonly Version Old = new(1, 1, 0);
        // MIN_VERSION goes up with each IM-HI release (tools/update_harmony_checker.py), so tests follow it.
        private static readonly string OutdatedText = "Update IM-HI to " + IMHIUpdate.MIN_VERSION;

        public UpdateButtonTests()
        {
            TestGame.Reset();
            TestGame.LoadModConstants();
        }

        private static GameObject Refresh(Version installed)
        {
            IMHIUpdate.InstalledVersion = () => installed;
            Menu menu = TestGame.BuildMenu();
            Seams.StopSpinnerPostfix();
            return TestGame.UpdateButton(menu);
        }

        private static GameObject Label(GameObject button) => Seams.ChildNamed(button, "Text");

        private static TextMeshProUGUI LabelText(GameObject button) => Seams.GetComponent<TextMeshProUGUI>(Label(button));

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

        /// <summary>
        /// The menu's buttons are 50 high; the menu's column sets their width.
        /// </summary>
        [Fact]
        public void Button_IsAsTallAsTheMenusButtons()
        {
            GameObject button = Refresh(Old);

            Assert.Equal(new Vector2(0f, 50f), Seams.Setting(Seams.TransformOf(button), "sizeDelta"));
        }

        /// <summary>
        /// The game's button parts only: none of the Mods button's own parts (its popup opener, demo blocker
        /// or loading spinner) or anything another mod added to it.
        /// </summary>
        [Fact]
        public void Button_IsBuiltFromTheGamesButtonParts()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            Menu menu = TestGame.BuildMenu();
            Seams.AddComponent(menu.ModsButton, typeof(OpenPopup));
            Seams.NewObject("Spinner (1)", menu.ModsButton);

            Seams.StopSpinnerPostfix();

            GameObject button = TestGame.UpdateButton(menu);
            Assert.Equal(new[] { typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonDefault) },
                Seams.Components(button).Select(c => c.GetType()));
            Assert.Equal(new[] { "Text" }, Seams.ChildNames(button));
            Assert.Equal(new[] { typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Lang_Button) },
                Seams.Components(Label(button)).Select(c => c.GetType()));
        }

        /// <summary>
        /// The menu buttons' text: their font at size 20, centred on one line, raised a little.
        /// </summary>
        [Fact]
        public void Label_LooksLikeTheMenusButtons()
        {
            GameObject button = Refresh(Old);

            TextMeshProUGUI text = LabelText(button);
            Assert.Same(Seams.MenuFont, Seams.Setting(text, "font"));
            Assert.Equal(20f, Seams.Setting(text, "fontSize"));
            Assert.Equal(TextAlignmentOptions.Center, Seams.Setting(text, "alignment"));
            Assert.Equal(false, Seams.Setting(text, "enableWordWrapping"));

            Transform rect = Seams.TransformOf(Label(button));
            Assert.Equal(Vector2.zero, Seams.Setting(rect, "anchorMin"));
            Assert.Equal(Vector2.one, Seams.Setting(rect, "anchorMax"));
            Assert.Equal(new Vector2(0f, 1.3f), Seams.Setting(rect, "offsetMin"));
            Assert.Equal(new Vector2(0f, 1.3f), Seams.Setting(rect, "offsetMax"));
        }

        [Fact]
        public void MenuFontMissing_UsesDefaultFontAndSaysSo()
        {
            IMHIUpdate.MenuFont = () => null;

            GameObject button = Refresh(Old);

            Assert.NotNull(button);
            Assert.Null(Seams.Setting(LabelText(button), "font"));
            Assert.Equal(OutdatedText, TestGame.TextOf(LabelText(button)));
            Assert.Contains(Log.Messages, m => m.Contains("[Harmony Checker] The menu font Linotte-SemiBold SDF isn't loaded"));
        }

        [Fact]
        public void WithoutPicture_ButtonIsPlainGold()
        {
            GameObject button = Refresh(Old);

            Image background = Seams.GetComponent<Image>(button);
            Assert.Equal((Color)mainScript.gold32, Seams.Colors[background]);
            Assert.False(Seams.Sprites.ContainsKey(background));
        }

        /// <summary>
        /// Dark text, readable on the plain gold button.
        /// </summary>
        [Fact]
        public void WithoutPicture_ButtonTextIsDark()
        {
            GameObject button = Refresh(Old);

            Assert.Equal((Color)mainScript.blackGold32, Seams.Colors[LabelText(button)]);
        }

        [Fact]
        public void WithoutPicture_NoGlow()
        {
            List<GameObject> glowed = RecordGlow();

            Refresh(Old);

            Assert.Empty(glowed);
        }

        /// <summary>
        /// The button's image draws the gold picture as the game draws its button pictures: sliced, at twice its
        /// pixel density.
        /// </summary>
        [Fact]
        public void WithPicture_ButtonUsesIt()
        {
            IMHIUpdate.GoldSprite = () => Seams.Fake<Sprite>();

            GameObject button = Refresh(Old);

            Image background = Seams.GetComponent<Image>(button);
            Assert.Equal("sprite", Seams.Sprites[background]);
            Assert.False(Seams.Colors.ContainsKey(background));
            Assert.Equal(Image.Type.Sliced, Seams.Setting(background, "type"));
            Assert.Equal(2f, Seams.Setting(background, "pixelsPerUnitMultiplier"));
        }

        /// <summary>
        /// White text, like the other menu buttons.
        /// </summary>
        [Fact]
        public void WithPicture_ButtonTextIsWhite()
        {
            IMHIUpdate.GoldSprite = () => Seams.Fake<Sprite>();

            GameObject button = Refresh(Old);

            Assert.Equal(Color.white, Seams.Colors[LabelText(button)]);
        }

        /// <summary>
        /// The halo is added to the update button once, not again on later menu refreshes.
        /// </summary>
        [Fact]
        public void WithPicture_GlowsOnce()
        {
            IMHIUpdate.GoldSprite = () => Seams.Fake<Sprite>();
            List<GameObject> glowed = RecordGlow();

            GameObject button = Refresh(Old);
            Seams.StopSpinnerPostfix();

            Assert.Single(glowed);
            Assert.Same(button, glowed[0]);
        }

        private static List<GameObject> RecordGlow()
        {
            List<GameObject> glowed = new();
            IMHIUpdate.AddGlow = glowed.Add;
            return glowed;
        }

        /// <summary>
        /// The Mods label without IM-HI, "Mods [IM-HI not installed]", is the longest text known to fit the button.
        /// </summary>
        [Fact]
        public void Label_NoLongerThanModsLabel()
        {
            string label = IMHIUpdate.FALLBACK_LABEL.Replace("@2", IMHIUpdate.MIN_VERSION.ToString());

            Assert.True(label.Length <= TestGame.NotInstalledText.Length, label);
        }

        /// <summary>
        /// The only click handler opens the releases page.
        /// </summary>
        [Fact]
        public void Click_OpensReleasesPage()
        {
            GameObject button = Refresh(Old);
            Button click = Seams.GetComponent<Button>(button);

            Seams.Click(click);

            Assert.Equal(new[] { "https://github.com/ui3TD/IM-HarmonyIntegration/releases/latest" }, Seams.OpenedUrls);
            Assert.Single(Seams.Listeners(click.onClick));
            Assert.Equal(0, click.onClick.GetPersistentEventCount());
        }

        /// <summary>
        /// The Mods button keeps the game's label, look and click; the badge on it shows IM-HI is out of date.
        /// </summary>
        [Fact]
        public void Outdated_ModsButtonKeepsGameLabel()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            Assert.Equal(TestGame.VanillaConstant, menu.ModsLangButton.Constant);
            Assert.Equal(TestGame.VanillaText, TestGame.TextOf(menu.Text));
            Assert.True(Assert.Single(Seams.Badges).outdated);
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

            Seams.StartPostfix(controller);
            Seams.StopSpinnerPostfix();
            Seams.StopSpinnerPostfix();

            Assert.Single(Seams.Created, obj => Seams.NameOf(obj) == IMHIUpdate.BUTTON_NAME);
            Assert.Single(Seams.ChildNames(menu.Container), IMHIUpdate.BUTTON_NAME);
        }

        /// <summary>
        /// The button doesn't need the Mods button: without it (another mod removed or renamed it), it goes at
        /// the end of the menu's buttons.
        /// </summary>
        [Fact]
        public void NoModsButton_ButtonGoesLast()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            Menu menu = TestGame.BuildMenu();
            Seams.Remove(menu.ModsButton);

            Seams.StopSpinnerPostfix();

            Assert.Equal(new[] { "Settings", "Quit", IMHIUpdate.BUTTON_NAME }, Seams.ChildNames(menu.Container));
        }

        /// <summary>
        /// A Mods button without its parts (another mod changed it) doesn't stop the update button.
        /// </summary>
        [Fact]
        public void ModsButtonChanged_ButtonStillAdded()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            Menu menu = TestGame.BuildMenu();
            Seams.RemoveComponent(menu.ModsClick);
            Seams.RemoveComponent(menu.ModsLangButton);

            Seams.StopSpinnerPostfix();

            Assert.Equal(new[] { "Settings", "Mods", IMHIUpdate.BUTTON_NAME, "Quit" }, Seams.ChildNames(menu.Container));
        }

        /// <summary>
        /// If building fails halfway, the half-built button is removed and the error logged; the game's method
        /// still finishes, and the next refresh builds the button.
        /// </summary>
        [Fact]
        public void BuildFails_NoHalfBuiltButtonAndNextRefreshBuildsIt()
        {
            IMHIUpdate.InstalledVersion = () => Old;
            IMHIUpdate.GoldSprite = () => Seams.Fake<Sprite>();
            IMHIUpdate.AddGlow = button => throw new InvalidOperationException("glow failed");
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            Assert.Null(TestGame.UpdateButton(menu));
            Assert.Equal(IMHIUpdate.BUTTON_NAME, Seams.NameOf(Assert.Single(Seams.Destroyed)));
            Assert.Contains(Log.Messages, m => m.Contains("[Harmony Checker] Couldn't update the main menu") && m.Contains("glow failed"));

            IMHIUpdate.AddGlow = button => { };
            Seams.StopSpinnerPostfix();

            Assert.NotNull(TestGame.UpdateButton(menu));
        }

        /// <summary>
        /// The asked-for version, a patch above it, and a two-part version of the next major.
        /// </summary>
        public static IEnumerable<object[]> UpToDateVersions()
        {
            Version min = IMHIUpdate.MIN_VERSION;
            yield return new object[] { min.ToString() };
            yield return new object[] { new Version(min.Major, min.Minor, min.Build + 1).ToString() };
            yield return new object[] { new Version(min.Major + 1, 0).ToString() };
        }

        [Theory]
        [MemberData(nameof(UpToDateVersions))]
        public void UpToDate_NoButton(string installed)
        {
            IMHIUpdate.InstalledVersion = () => Version.Parse(installed);
            Menu menu = TestGame.BuildMenu();

            Seams.StopSpinnerPostfix();

            Assert.Null(TestGame.UpdateButton(menu));
            Assert.Empty(Seams.Created);
            Assert.Equal(TestGame.VanillaText, TestGame.TextOf(menu.Text));
            Assert.False(Assert.Single(Seams.Badges).outdated);
        }

        /// <summary>
        /// If the installed version can't be read, nobody is told to update.
        /// </summary>
        [Fact]
        public void UnknownVersion_NoButton()
        {
            Assert.Null(Refresh(null));
            Assert.Empty(Seams.Created);
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
