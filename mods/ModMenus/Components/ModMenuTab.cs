using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// A mod's tab, in the column on the left. Clicking it shows the mod's page.
        /// </summary>
        public class ModMenuTab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public string modName;
            public GameObject page;
            public ModMenuManager manager;
            public Image image;
            public TextMeshProUGUI label;
            public Button button;

            /// <summary>Whether the pointer is over the tab.</summary>
            public bool hovered;

            /// <summary>Whether the tab's page is shown.</summary>
            public bool shown;

            public void OnClick()
            {
                manager.ShowPage(this);
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                hovered = true;
                Render(shown);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                hovered = false;
                Render(shown);
            }

            /// <summary>
            /// The shown tab is green with white text, as the game's own tabs are. The others are blue text with
            /// no fill, and a faint blue one while the pointer is over them.
            /// </summary>
            public void Render(bool selected)
            {
                shown = selected;
                Color32 blue = mainScript.blue32;
                image.color = selected ? mainScript.green32 : new Color32(blue.r, blue.g, blue.b, (byte)(hovered ? 38 : 0));
                label.color = selected ? Color.white : (Color)blue;
            }
        }
    }
}
