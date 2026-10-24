using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace HarmonyChecker.Tests
{
    /// <summary>
    /// The round badge on the Mods button: a tick when IM-HI works, a "!" when it is out of date, with a tooltip.
    /// </summary>
    public class BadgeTests
    {
        private static readonly Version Old = new(1, 1, 0);

        public BadgeTests()
        {
            TestGame.Reset();
            TestGame.LoadModConstants();
        }

        private static GameObject Show(Menu menu, bool outdated, Version installed = null)
        {
            Seams.ShowBadge(Seams.TransformOf(menu.ModsButton), outdated, installed ?? (outdated ? Old : IMHIUpdate.MIN_VERSION));
            return Seams.ChildNamed(menu.ModsButton, StatusBadge.NAME);
        }

        private static BadgeTooltip Tooltip(GameObject badge) => Seams.GetComponent<BadgeTooltip>(badge);

        private static string Picture(GameObject badge) => Seams.Sprites[Seams.GetComponent<Image>(badge)];

        [Fact]
        public void UpToDate_GreenTick()
        {
            GameObject badge = Show(TestGame.BuildMenu(), outdated: false);

            Assert.Equal("ok", Picture(badge));
            Assert.Equal("IM-HarmonyIntegration is installed", Tooltip(badge).Text);
        }

        [Fact]
        public void Outdated_AmberMark()
        {
            GameObject badge = Show(TestGame.BuildMenu(), outdated: true);

            Assert.Equal("outdated", Picture(badge));
            Assert.Equal("Please update IM-HarmonyIntegration", Tooltip(badge).Text);
        }

        /// <summary>
        /// The badge sits inside the Mods button on its right edge, outside the button's layout, and is the last
        /// child, so it's drawn over the label.
        /// </summary>
        [Fact]
        public void Badge_SitsOnTheRightOfTheModsButton()
        {
            Menu menu = TestGame.BuildMenu();
            GameObject badge = Show(menu, outdated: false);
            RectTransform rect = (RectTransform)Seams.TransformOf(badge);

            Assert.Equal(StatusBadge.NAME, Seams.ChildNames(menu.ModsButton).Last());
            Assert.Equal(new Vector2(1f, 0.5f), Seams.Setting(rect, "anchorMin"));
            Assert.Equal(new Vector2(1f, 0.5f), Seams.Setting(rect, "anchorMax"));
            Assert.Equal(new Vector2(-StatusBadge.INSET - StatusBadge.SIZE, -StatusBadge.SIZE / 2), Seams.Setting(rect, "offsetMin"));
            Assert.Equal(new Vector2(-StatusBadge.INSET, StatusBadge.SIZE / 2), Seams.Setting(rect, "offsetMax"));
            Assert.Equal(true, Seams.Setting(Seams.GetComponent<LayoutElement>(badge), "ignoreLayout"));
        }

        /// <summary>
        /// The badge has no click handler, so a click on it goes on to the Mods button.
        /// </summary>
        [Fact]
        public void Badge_HasNoClickHandler()
        {
            GameObject badge = Show(TestGame.BuildMenu(), outdated: false);

            Assert.Null(Seams.GetComponent<Button>(badge));
            Assert.DoesNotContain(Seams.Components(badge), c => c is UnityEngine.EventSystems.IPointerClickHandler);
        }

        /// <summary>
        /// Every refresh keeps one badge, switching its picture and text as IM-HI's status changes.
        /// </summary>
        [Fact]
        public void EveryRefresh_KeepsOneBadge()
        {
            Menu menu = TestGame.BuildMenu();

            Show(menu, outdated: false);
            Show(menu, outdated: true);
            GameObject badge = Show(menu, outdated: false);

            Assert.Single(Seams.ChildNames(menu.ModsButton), name => name == StatusBadge.NAME);
            Assert.Single(Seams.Created);
            Assert.Equal("ok", Picture(badge));
            Assert.EndsWith("is installed", Tooltip(badge).Text);
        }

        [Fact]
        public void PictureMissing_NoBadge()
        {
            Seams.OkPicture = null;
            Menu menu = TestGame.BuildMenu();

            Assert.Null(Show(menu, outdated: false));
            Assert.Empty(Seams.Created);
        }

        /// <summary>
        /// If building the badge fails, the half-built badge is removed, so the next refresh builds it again.
        /// </summary>
        [Fact]
        public void BuildFails_NoHalfBuiltBadge()
        {
            Menu menu = TestGame.BuildMenu();
            Seams.OnSetOffsetMax = () => throw new InvalidOperationException("broken");

            Assert.Throws<InvalidOperationException>(() => Show(menu, outdated: false));

            Assert.Equal(StatusBadge.NAME, Seams.NameOf(Assert.Single(Seams.Destroyed)));
            Assert.Null(Seams.ChildNamed(menu.ModsButton, StatusBadge.NAME));
            Seams.OnSetOffsetMax = null;
            Assert.NotNull(Show(menu, outdated: false));
        }

        // The tooltip text

        [Fact]
        public void TextNotLoaded_UsesFallback()
        {
            Language.Data.Remove(StatusBadge.OK_TEXT);
            Language.Data.Remove(StatusBadge.OUTDATED_TEXT);

            Assert.Equal("IM-HarmonyIntegration is installed", StatusBadge.Text(false, new Version(1, 2, 3)));
            Assert.Equal("Please update IM-HarmonyIntegration", StatusBadge.Text(true, Old));
        }

        /// <summary>
        /// A translation mod can change the text; the versions are still filled in.
        /// </summary>
        [Fact]
        public void TranslatedText_GetsVersions()
        {
            Language.Data[StatusBadge.OUTDATED_TEXT] = "IM-HI @1 ist zu alt, @2 nötig.";

            Assert.Equal($"IM-HI 1.1.0 ist zu alt, {IMHIUpdate.MIN_VERSION} nötig.", StatusBadge.Text(true, Old));
        }

        [Fact]
        public void UnknownVersion_LeftOut() =>
            Assert.Equal("IM-HarmonyIntegration is installed", StatusBadge.Text(false, null));

        /// <summary>
        /// The constants and the fallbacks say the same thing, in simple words, so the tooltip doesn't depend on
        /// which one is used.
        /// </summary>
        [Fact]
        public void Constants_MatchFallbacks()
        {
            Assert.Equal(StatusBadge.OK_FALLBACK, Language.Data[StatusBadge.OK_TEXT]);
            Assert.Equal(StatusBadge.OUTDATED_FALLBACK, Language.Data[StatusBadge.OUTDATED_TEXT]);
        }

        // Showing the tooltip

        /// <summary>The menu's dark tooltip colours, as the game's main menu prefab has them.</summary>
        private static readonly Color MenuBackground = new(0.208f, 0.235f, 0.251f, 0.773f);
        private static readonly Color MenuText = Color.white;

        private static Image tooltipBackground;
        private static TMPro.TextMeshProUGUI tooltipText;

        /// <summary>A badge, and the menu's tooltip as the game builds it: a text in a background ("BG").</summary>
        private static BadgeTooltip HoverableBadge(Menu menu)
        {
            BadgeTooltip tooltip = Tooltip(Show(menu, outdated: false));
            TooltipManager manager = Seams.Add<TooltipManager>(menu.Data);
            GameObject background = Seams.NewObject("BG", menu.Data);
            tooltipBackground = Seams.Add<Image>(background);
            Seams.Colors[tooltipBackground] = MenuBackground;
            manager.TextObj = Seams.NewObject("Text", background);
            tooltipText = Seams.Add<TMPro.TextMeshProUGUI>(manager.TextObj);
            Seams.Colors[tooltipText] = MenuText;
            return tooltip;
        }

        /// <summary>
        /// The menu's tooltip is the game's dark one; while the badge is hovered it has the light colours of the
        /// game's other tooltips, and when the mouse leaves its own colours come back.
        /// </summary>
        [Fact]
        public void Hovered_TooltipHasGameColours()
        {
            BadgeTooltip tooltip = HoverableBadge(TestGame.BuildMenu());

            tooltip.OnPointerEnter(null);
            Seams.BadgeUpdate(tooltip);
            Assert.Equal((Color)BadgeTooltip.LIGHT_BG, Seams.Colors[tooltipBackground]);
            Assert.Equal((Color)BadgeTooltip.LIGHT_TEXT, Seams.Colors[tooltipText]);

            Seams.BadgeUpdate(tooltip);
            tooltip.OnPointerExit(null);
            Seams.BadgeUpdate(tooltip);
            Assert.Equal(MenuBackground, Seams.Colors[tooltipBackground]);
            Assert.Equal(MenuText, Seams.Colors[tooltipText]);
        }

        [Fact]
        public void Disabled_PutsColoursBack()
        {
            BadgeTooltip tooltip = HoverableBadge(TestGame.BuildMenu());
            tooltip.OnPointerEnter(null);
            Seams.BadgeUpdate(tooltip);

            Seams.BadgeDisable(tooltip);

            Assert.Equal(MenuBackground, Seams.Colors[tooltipBackground]);
            Assert.Equal(MenuText, Seams.Colors[tooltipText]);
        }

        /// <summary>
        /// Without the background, the tooltip is left as it is and still shows.
        /// </summary>
        [Fact]
        public void NoTooltipBackground_ShowsUnthemed()
        {
            Menu menu = TestGame.BuildMenu();
            BadgeTooltip tooltip = Tooltip(Show(menu, outdated: false));
            TooltipManager manager = Seams.Add<TooltipManager>(menu.Data);
            manager.TextObj = Seams.NewObject("Text", menu.Data);
            Seams.Add<TMPro.TextMeshProUGUI>(manager.TextObj);
            tooltip.OnPointerEnter(null);

            Seams.BadgeUpdate(tooltip);

            Assert.Single(Seams.Tooltips);
            Assert.Empty(Seams.Colors);
        }

        /// <summary>
        /// While the mouse is over the badge, the game's tooltip shows its text every frame, with the pointer
        /// cursor of the game's buttons.
        /// </summary>
        [Fact]
        public void Hovered_ShowsTooltipEveryFrame()
        {
            BadgeTooltip tooltip = HoverableBadge(TestGame.BuildMenu());

            tooltip.OnPointerEnter(null);
            Seams.BadgeUpdate(tooltip);
            Seams.BadgeUpdate(tooltip);

            Assert.Equal(new[] { tooltip.Text, tooltip.Text }, Seams.Tooltips);
            Assert.Equal(new[] { staticVars.CursorType.pointer, staticVars.CursorType.pointer }, Seams.Cursors);
        }

        [Fact]
        public void NotHovered_NoTooltip()
        {
            BadgeTooltip tooltip = HoverableBadge(TestGame.BuildMenu());

            Seams.BadgeUpdate(tooltip);
            tooltip.OnPointerEnter(null);
            tooltip.OnPointerExit(null);
            Seams.BadgeUpdate(tooltip);

            Assert.Empty(Seams.Tooltips);
            Assert.Empty(Seams.Cursors);
        }

        /// <summary>
        /// A hidden badge gets no mouse-leave, so hiding it stops the tooltip.
        /// </summary>
        [Fact]
        public void Disabled_StopsTooltip()
        {
            BadgeTooltip tooltip = HoverableBadge(TestGame.BuildMenu());
            tooltip.OnPointerEnter(null);

            Seams.BadgeDisable(tooltip);
            Seams.BadgeUpdate(tooltip);

            Assert.Empty(Seams.Tooltips);
        }

        /// <summary>
        /// Without the game's tooltip manager the badge still sets the cursor and doesn't throw.
        /// </summary>
        [Fact]
        public void NoTooltipManager_NoError()
        {
            BadgeTooltip tooltip = Tooltip(Show(TestGame.BuildMenu(), outdated: false));
            tooltip.OnPointerEnter(null);

            Seams.BadgeUpdate(tooltip);

            Assert.Empty(Seams.Tooltips);
            Assert.Single(Seams.Cursors);
        }
    }
}
