using UnityEngine;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// Fits the menu to the popup (see LayoutFor) when it opens and whenever the screen changes size.
        /// </summary>
        public class ModMenuLayout : MonoBehaviour
        {
            public RectTransform panel;
            public RectTransform apply;
            public RectTransform cancel;
            public RectTransform reset;
            public RectTransform tabColumn;
            public RectTransform settingsArea;
            public RectTransform divider;
            public ModMenuManager manager;
            public float buttonHeight;

            private bool sizeChanged;

            public void OnEnable()
            {
                Fit();
            }

            /// <summary>
            /// Called while the canvas is being laid out, so the menu is fitted after it, in LateUpdate.
            /// </summary>
            public void OnRectTransformDimensionsChange()
            {
                sizeChanged = true;
            }

            public void LateUpdate()
            {
                if (!sizeChanged)
                {
                    return;
                }
                sizeChanged = false;
                Fit();
            }

            /// <summary>
            /// Places the panel, the buttons, and the tab column, line and settings inside the panel.
            /// </summary>
            public void Fit()
            {
                Vector2 size = ((RectTransform)transform).rect.size;
                if (size.x <= 0 || size.y <= 0)
                {
                    return;
                }
                MenuLayout layout = LayoutFor(size, buttonHeight);
                PlaceFromCentre(panel, layout.Panel);
                PlaceFromCentre(apply, layout.Apply);
                PlaceFromCentre(cancel, layout.Cancel);
                PlaceFromCentre(reset, layout.Reset);

                float tabsRight = PANEL_MARGIN + layout.TabWidth;
                float line = tabsRight + PANEL_MARGIN;
                SetRectTransform(tabColumn, Vector2.zero, Vector2.up, new Vector2(PANEL_MARGIN, PANEL_MARGIN), new Vector2(tabsRight, -PANEL_MARGIN));
                SetRectTransform(divider, Vector2.zero, Vector2.up, new Vector2(line, PANEL_MARGIN), new Vector2(line + 1, -PANEL_MARGIN));
                SetRectTransform(settingsArea, Vector2.zero, Vector2.one, new Vector2(line + 1 + PANEL_MARGIN, PANEL_MARGIN), new Vector2(-PANEL_MARGIN, -PANEL_MARGIN));

                // An open list would stay where its row was
                if (manager != null)
                {
                    manager.CloseDropdownLists();
                }
            }

            /// <summary>
            /// Puts a rect where the layout says, measured from the popup's centre.
            /// </summary>
            private static void PlaceFromCentre(RectTransform rt, Rect place)
            {
                Vector2 centre = new(0.5f, 0.5f);
                SetRectTransform(rt, centre, centre, Vector2.zero, Vector2.zero, centre);
                rt.sizeDelta = place.size;
                rt.anchoredPosition = place.center;
            }
        }
    }
}
