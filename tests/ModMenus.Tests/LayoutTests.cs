using System.Collections.Generic;
using UnityEngine;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.0.0: the menu fits the screen. The game's popups are always 1024 units wide, so the popup's
    /// height depends on the screen's shape: 576 at 16:9, 640 at 16:10, 768 at 4:3, 432 at 21:9, 288 at 32:9.
    /// A mod that changes the popups' scaling can make them any size.
    /// </summary>
    public class LayoutTests
    {
        public LayoutTests() => TestGame.Reset();

        /// <summary>
        /// How tall the game's Apply and Cancel buttons are.
        /// </summary>
        private const float ButtonHeight = 38.66f;

        public static IEnumerable<object[]> PopupSizes() => new[]
        {
            new object[] { 1024f, 576f },   // 16:9
            new object[] { 1024f, 640f },   // 16:10, the Steam Deck
            new object[] { 1024f, 768f },   // 4:3
            new object[] { 1024f, 432f },   // 21:9
            new object[] { 1024f, 288f },   // 32:9
            new object[] { 1024f, 1820f },  // a portrait screen
            new object[] { 1792f, 768f },   // scaled by height instead
            new object[] { 432f, 768f },
            new object[] { 2048f, 1536f },
        };

        [Theory]
        [MemberData(nameof(PopupSizes))]
        public void MenuFitsInsideThePopup(float width, float height)
        {
            MenuLayout layout = LayoutFor(new Vector2(width, height), ButtonHeight);
            Rect popup = new(-width / 2, -height / 2, width, height);

            foreach (Rect part in new[] { layout.Panel, layout.Apply, layout.Cancel })
            {
                Assert.True(part.xMin >= popup.xMin + POPUP_MARGIN_MIN - 0.01f && part.xMax <= popup.xMax - POPUP_MARGIN_MIN + 0.01f, $"{part} is wider than {popup}");
                Assert.True(part.yMin >= popup.yMin + POPUP_MARGIN_MIN - 0.01f && part.yMax <= popup.yMax - POPUP_MARGIN_MIN + 0.01f, $"{part} is taller than {popup}");
            }
            Assert.False(layout.Apply.Overlaps(layout.Cancel));
            Assert.True(layout.Panel.height > 0);
        }

        [Theory]
        [MemberData(nameof(PopupSizes))]
        public void PanelIsNoBiggerThan840By640(float width, float height)
        {
            MenuLayout layout = LayoutFor(new Vector2(width, height), ButtonHeight);

            Assert.True(layout.Panel.width <= PANEL_MAX_WIDTH);
            Assert.True(layout.Panel.height <= PANEL_MAX_HEIGHT);
        }

        /// <summary>
        /// The panel and the buttons under it are centred on the screen together.
        /// </summary>
        [Theory]
        [MemberData(nameof(PopupSizes))]
        public void PanelAndButtonsAreCentredTogether(float width, float height)
        {
            MenuLayout layout = LayoutFor(new Vector2(width, height), ButtonHeight);

            Assert.Equal(0, layout.Panel.center.x, 3);
            Assert.Equal(layout.Panel.yMax, -layout.Apply.yMin, 3);
        }

        /// <summary>
        /// Apply on the right and Cancel on the left, as in the game's own popup, and never past the panel's sides.
        /// </summary>
        [Theory]
        [MemberData(nameof(PopupSizes))]
        public void ButtonsSitUnderThePanelEitherSideOfItsCentre(float width, float height)
        {
            MenuLayout layout = LayoutFor(new Vector2(width, height), ButtonHeight);

            foreach (Rect button in new[] { layout.Apply, layout.Cancel })
            {
                Assert.Equal(layout.Panel.yMin - BUTTON_GAP, button.yMax, 3);
                Assert.Equal(ButtonHeight, button.height);
                Assert.True(button.xMin >= layout.Panel.xMin - 0.01f && button.xMax <= layout.Panel.xMax + 0.01f, $"{button} is wider than the panel");
            }
            Assert.True(layout.Apply.center.x > 0);
            Assert.Equal(-layout.Apply.center.x, layout.Cancel.center.x, 3);
        }

        /// <summary>
        /// Wherever there's room, the buttons are the game's size and spacing.
        /// </summary>
        [Fact]
        public void ButtonsAreTheGamesSizeWhenTheyFit()
        {
            MenuLayout layout = LayoutFor(new Vector2(1024, 576), ButtonHeight);

            Assert.Equal(new Vector2(BUTTON_WIDTH, ButtonHeight), layout.Apply.size);
            Assert.Equal(BUTTON_SPREAD, layout.Apply.center.x, 3);
        }

        /// <summary>
        /// The wider the screen, the shorter the popup and the menu; it only stops growing at its largest size.
        /// </summary>
        [Fact]
        public void ShorterPopupsGetShorterPanels()
        {
            float[] heights = { 1820, 768, 640, 576, 432, 288 };
            float last = float.MaxValue;
            foreach (float height in heights)
            {
                float panel = LayoutFor(new Vector2(1024, height), ButtonHeight).Panel.height;
                Assert.True(panel <= last, $"The panel is taller in a {height}-tall popup than in a taller one");
                last = panel;
            }
            Assert.Equal(PANEL_MAX_HEIGHT, LayoutFor(new Vector2(1024, 768), ButtonHeight).Panel.height);
            Assert.True(LayoutFor(new Vector2(1024, 288), ButtonHeight).Panel.height > 150);
        }

        [Theory]
        [InlineData(1024, 200)]
        [InlineData(700, 156.94f)]
        [InlineData(600, 140)]
        public void TabColumnNarrowsOnNarrowPanels(float width, float expected)
        {
            Assert.Equal(expected, LayoutFor(new Vector2(width, 576), ButtonHeight).TabWidth, 2);
        }

        /// <summary>
        /// At 16:9 the panel was 472 by 425 before 4.0.0.
        /// </summary>
        [Fact]
        public void SixteenByNineGivesAnEightFortyByFourEightyThreePanel()
        {
            Rect panel = LayoutFor(new Vector2(1024, 576), ButtonHeight).Panel;

            Assert.Equal(840, panel.width, 2);
            Assert.Equal(483.26f, panel.height, 2);
        }

        /// <summary>
        /// Opening the menu fits it to the popup: the panel and buttons where LayoutFor says, the tab column on
        /// the panel's left, a line, then the settings, with the same margin all round.
        /// </summary>
        [Fact]
        public void LayoutIsAppliedWhenTheMenuOpens()
        {
            GameObject root = Seams.NewObject("ModMenu");
            ModMenuLayout layout = Seams.Add<ModMenuLayout>(root);
            GameObject panel = Seams.NewObject("Panel", root);
            layout.manager = Seams.Add<ModMenuManager>(panel);
            layout.panel = (RectTransform)Seams.TransformOf(panel);
            layout.apply = (RectTransform)Seams.TransformOf(Seams.NewObject("Apply", root));
            layout.cancel = (RectTransform)Seams.TransformOf(Seams.NewObject("Cancel", root));
            layout.tabColumn = (RectTransform)Seams.TransformOf(Seams.NewObject(TABS_SCROLLRECT_OBJ_NAME, panel));
            layout.divider = (RectTransform)Seams.TransformOf(Seams.NewObject(DIVIDER_OBJ_NAME, panel));
            layout.settingsArea = (RectTransform)Seams.TransformOf(Seams.NewObject(SCROLLRECT_OBJ_NAME, panel));
            layout.buttonHeight = ButtonHeight;
            Seams.Sizes[Seams.TransformOf(root)] = new Vector2(1024, 432);

            Seams.LayoutOnEnable(layout);

            MenuLayout expected = LayoutFor(new Vector2(1024, 432), ButtonHeight);
            Assert.Equal(expected.Panel, Seams.Placements[layout.panel]);
            Assert.Equal(expected.Apply, Seams.Placements[layout.apply]);
            Assert.Equal(expected.Cancel, Seams.Placements[layout.cancel]);

            var tabs = Seams.Rects[layout.tabColumn];
            var line = Seams.Rects[layout.divider];
            var settings = Seams.Rects[layout.settingsArea];
            Assert.Equal((Vector2.zero, Vector2.up), (tabs.anchorMin, tabs.anchorMax));
            Assert.Equal(new Vector2(PANEL_MARGIN, PANEL_MARGIN), tabs.offsetMin);
            Assert.Equal(new Vector2(PANEL_MARGIN + expected.TabWidth, -PANEL_MARGIN), tabs.offsetMax);
            Assert.Equal(tabs.offsetMax.x + PANEL_MARGIN, line.offsetMin.x);
            Assert.Equal(line.offsetMin.x + 1, line.offsetMax.x);
            Assert.Equal((Vector2.zero, Vector2.one), (settings.anchorMin, settings.anchorMax));
            Assert.Equal(new Vector2(line.offsetMax.x + PANEL_MARGIN, PANEL_MARGIN), settings.offsetMin);
            Assert.Equal(new Vector2(-PANEL_MARGIN, -PANEL_MARGIN), settings.offsetMax);
        }

        /// <summary>
        /// A popup with no size yet (its canvas isn't laid out) leaves the menu as it is.
        /// </summary>
        [Fact]
        public void APopupWithNoSizeIsLeftAlone()
        {
            GameObject root = Seams.NewObject("ModMenu");
            ModMenuLayout layout = Seams.Add<ModMenuLayout>(root);
            layout.panel = (RectTransform)Seams.TransformOf(Seams.NewObject("Panel", root));

            Seams.LayoutFit(layout);

            Assert.Empty(Seams.Placements);
        }
    }
}
