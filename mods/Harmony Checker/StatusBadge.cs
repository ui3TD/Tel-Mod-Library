using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HarmonyChecker
{
    /// <summary>
    /// A round badge on the right of the Mods button: a green tick when IM-HI works, an amber "!" when it is out
    /// of date. Hovering it shows what it means in the game's tooltip.
    /// </summary>
    internal static class StatusBadge
    {
        public const string NAME = "IMHI_Status";

        // The badge's size, and its gap to the right edge of the Mods button (which is 50 high).
        public const float SIZE = 28f;
        public const float INSET = 12f;

        public const string OK_TEXT = "IMHI_STATUS_OK";
        public const string OUTDATED_TEXT = "IMHI_STATUS_OUTDATED";
        public const string OK_FALLBACK = "IM-HarmonyIntegration is installed";
        public const string OUTDATED_FALLBACK = "Please update IM-HarmonyIntegration";

        public static readonly ModPicture Ok = new ModPicture("IMHI_Badge_OK.png", Vector4.zero);
        public static readonly ModPicture Outdated = new ModPicture("IMHI_Badge_Outdated.png", Vector4.zero);

        // Replaced in tests.
        internal static Func<bool, Sprite> Picture = outdated => (outdated ? Outdated : Ok).Get();

        /// <summary>
        /// Adds the badge to the Mods button, or updates the one already there; runs on every menu refresh.
        /// Without its picture there is no badge. If building it fails, the half-built badge is removed, so the
        /// next refresh tries again, and the error is passed on.
        /// </summary>
        public static void Show(Transform modsButton, bool outdated, Version installed)
        {
            Sprite sprite = Picture(outdated);
            if (sprite is null)
            {
                return;
            }

            Transform existing = modsButton.Find(NAME);
            GameObject badge = existing == null ? null : existing.gameObject;
            if (badge == null)
            {
                badge = new GameObject(NAME, typeof(RectTransform));
                try
                {
                    badge.transform.SetParent(modsButton, false);
                    badge.AddComponent<LayoutElement>().ignoreLayout = true;

                    // Centred on the button's right edge, moved in by INSET
                    RectTransform rect = (RectTransform)badge.transform;
                    rect.anchorMin = new Vector2(1f, 0.5f);
                    rect.anchorMax = new Vector2(1f, 0.5f);
                    rect.offsetMin = new Vector2(-INSET - SIZE, -SIZE / 2f);
                    rect.offsetMax = new Vector2(-INSET, SIZE / 2f);

                    // The image takes the mouse so the badge can show its tooltip; a click still goes on to the
                    // Mods button, since the badge has no click handler of its own.
                    badge.AddComponent<Image>();
                    badge.AddComponent<BadgeTooltip>();
                }
                catch
                {
                    Object.Destroy(badge);
                    throw;
                }
            }

            badge.GetComponent<Image>().sprite = sprite;
            badge.GetComponent<BadgeTooltip>().Text = Text(outdated, installed);
        }

        /// <summary>The tooltip, from the language table (so a translation mod can change it), versions filled in.</summary>
        public static string Text(bool outdated, Version installed)
        {
            string key = outdated ? OUTDATED_TEXT : OK_TEXT;
            string text = Language.Data.TryGetValue(key, out string translated) ? translated : outdated ? OUTDATED_FALLBACK : OK_FALLBACK;

            // An unknown version is left out
            return text.Replace("@1", installed?.ToString() ?? "").Replace("@2", IMHIUpdate.MIN_VERSION.ToString()).Replace("  ", " ");
        }
    }

    /// <summary>
    /// Shows the badge's text in the game's tooltip while the mouse is over the badge. The game hides its
    /// tooltip each frame it isn't asked for, so this asks every frame, as the game's buttons do.
    /// </summary>
    internal class BadgeTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // The light tooltip of the game's other screens (their TooltipManager's LightBG and LightText). The main
        // menu's tooltip is left in its dark prefab colours, since no menu button has a tooltip.
        public static readonly Color32 LIGHT_BG = new Color32(255, 255, 255, 229);
        public static readonly Color32 LIGHT_TEXT = new Color32(119, 123, 186, 255);

        public string Text;

        public bool Hovered { get; private set; }

        private TooltipManager tooltips;

        // The tooltip's background and text while this badge has recoloured them, with their colours before.
        private Image themedBackground;
        private TextMeshProUGUI themedText;
        private Color backgroundBefore, textBefore;

        public void OnPointerEnter(PointerEventData eventData) => Hovered = true;

        public void OnPointerExit(PointerEventData eventData) => Hovered = false;

        private void Update()
        {
            if (!Hovered || string.IsNullOrEmpty(Text))
            {
                RestoreTheme();
                return;
            }

            if (tooltips == null)
            {
                mainScript main = Camera.main == null ? null : Camera.main.GetComponent<mainScript>();
                tooltips = main == null || main.Data == null ? null : main.Data.GetComponent<TooltipManager>();
            }
            if (tooltips != null)
            {
                Theme();
                tooltips.ShowTooltip(Text);
            }

            // The Mods button only counts the mouse as over it when it is over the button itself, not the badge
            staticVars.setCursor(staticVars.CursorType.pointer);
        }

        /// <summary>Gives the tooltip the game's light colours, keeping its own to put back.</summary>
        private void Theme()
        {
            if (themedText != null || tooltips.TextObj == null)
            {
                return;
            }

            // The text sits in the background (TooltipManager.TextObj in "BG")
            TextMeshProUGUI text = tooltips.TextObj.GetComponent<TextMeshProUGUI>();
            Image background = text == null || text.transform.parent == null ? null : text.transform.parent.GetComponent<Image>();
            if (background == null)
            {
                return;
            }

            themedText = text;
            themedBackground = background;
            textBefore = text.color;
            backgroundBefore = background.color;
            text.color = LIGHT_TEXT;
            background.color = LIGHT_BG;
        }

        /// <summary>Puts the tooltip's own colours back, so other tooltips on the menu look as before.</summary>
        private void RestoreTheme()
        {
            if (themedText == null)
            {
                return;
            }

            themedText.color = textBefore;
            if (themedBackground != null)
            {
                themedBackground.color = backgroundBefore;
            }
            themedText = null;
            themedBackground = null;
        }

        // A hidden badge gets no mouse-leave, so it stops counting as hovered here.
        private void OnDisable()
        {
            Hovered = false;
            RestoreTheme();
        }
    }
}
