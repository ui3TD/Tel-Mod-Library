using UnityEngine;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// Where the panel and the Apply, Cancel and Reset buttons go, in the popup's units, measured from its centre.
        /// </summary>
        public struct MenuLayout
        {
            public Rect Panel;
            public Rect Apply;
            public Rect Cancel;
            public float TabWidth;
            public Rect Reset;
        }

        /// <summary>
        /// Fits the menu to the popup, which covers the screen. The game's popups keep the screen 1024 wide, so
        /// a wider screen gives a shorter popup. The panel is as big as fits, up to PANEL_MAX_WIDTH by
        /// PANEL_MAX_HEIGHT, inside a margin that shrinks on short screens; Cancel, Reset and Apply sit in a row
        /// under it, and the two are centred together. The tab column is a quarter of the panel, between
        /// TAB_MIN_WIDTH and TAB_WIDTH.
        /// </summary>
        /// <param name="popupSize">The popup's size.</param>
        /// <param name="buttonHeight">How tall the buttons are.</param>
        public static MenuLayout LayoutFor(Vector2 popupSize, float buttonHeight)
        {
            float margin = Mathf.Clamp(0.04f * popupSize.y, POPUP_MARGIN_MIN, POPUP_MARGIN_MAX);
            float width = Mathf.Max(0, Mathf.Min(PANEL_MAX_WIDTH, popupSize.x - 2 * margin));
            float height = Mathf.Clamp(popupSize.y - 2 * margin - BUTTON_GAP - buttonHeight, 0, PANEL_MAX_HEIGHT);
            float top = (height + BUTTON_GAP + buttonHeight) / 2;
            float buttonsBottom = top - height - BUTTON_GAP - buttonHeight;

            // On a panel too narrow for them, the buttons and the gaps between them get narrower, staying under it
            float gap = Mathf.Min(BUTTON_ROW_GAP, width / 20);
            float buttonWidth = Mathf.Max(0, Mathf.Min(BUTTON_WIDTH, (width - 2 * gap) / 3));
            float spread = buttonWidth + gap;

            return new MenuLayout
            {
                Panel = new Rect(-width / 2, top - height, width, height),
                Apply = new Rect(spread - buttonWidth / 2, buttonsBottom, buttonWidth, buttonHeight),
                Cancel = new Rect(-spread - buttonWidth / 2, buttonsBottom, buttonWidth, buttonHeight),
                Reset = new Rect(-buttonWidth / 2, buttonsBottom, buttonWidth, buttonHeight),
                TabWidth = Mathf.Clamp(0.24f * width, TAB_MIN_WIDTH, TAB_WIDTH),
            };
        }

        /// <summary>
        /// How tall a label may be: as tall as its wrapped lines, or, with a limit, up to that many lines. A label
        /// past the limit gets half a line more, so its last line fits whole and the next one doesn't; TextMeshPro
        /// then ends the last line in "…".
        /// </summary>
        /// <param name="wrapped">The label's height, wrapped at its width.</param>
        /// <param name="oneLine">The label's height on one line.</param>
        /// <param name="maxLines">The most lines it shows, or 0 for no limit.</param>
        public static float CappedLabelHeight(float wrapped, float oneLine, int maxLines)
        {
            if (maxLines <= 0)
            {
                return wrapped;
            }
            return Mathf.Min(wrapped, (maxLines + 0.5f) * oneLine);
        }

        /// <summary>
        /// Where a scroll area must scroll to show a child whole, moving as little as it can.
        /// </summary>
        /// <param name="contentHeight">How tall the scrolled content is.</param>
        /// <param name="viewHeight">How tall the visible area is.</param>
        /// <param name="top">The child's top, down from the content's top.</param>
        /// <param name="bottom">The child's bottom, down from the content's top.</param>
        /// <param name="position">Where it's scrolled to now: 1 at the top, 0 at the bottom.</param>
        /// <returns>Where to scroll to, in the same terms.</returns>
        public static float ScrollPositionShowing(float contentHeight, float viewHeight, float top, float bottom, float position)
        {
            float scrollable = contentHeight - viewHeight;
            if (scrollable <= 0)
            {
                return 1;
            }
            float offset = (1 - Mathf.Clamp01(position)) * scrollable;
            if (top < offset)
            {
                offset = top;
            }
            else if (bottom > offset + viewHeight)
            {
                offset = bottom - viewHeight;
            }
            return 1 - Mathf.Clamp01(offset / scrollable);
        }

        /// <summary>
        /// How far an open dropdown list must move up to stay on screen. The game's lists open below their box;
        /// one that would run past the bottom goes above its box instead, the same gap away, if it fits there, and
        /// otherwise moves up until its bottom is on screen. All values are heights on screen, bottom to top.
        /// </summary>
        /// <returns>How far to move the list up, or 0 if it fits where it is.</returns>
        public static float ListShift(float listBottom, float listTop, float boxBottom, float boxTop, float screenBottom, float screenTop)
        {
            if (listBottom >= screenBottom)
            {
                return 0;
            }
            float above = boxTop + Mathf.Max(0, boxBottom - listTop) - listBottom;
            return listTop + above <= screenTop ? above : screenBottom - listBottom;
        }
    }
}
