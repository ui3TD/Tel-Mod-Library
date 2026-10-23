using AICore;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIFanChatter
{
    /// <summary>
    /// The mod's notices under the Refresh bar, one card each: a soft red card with a warning sign while
    /// requests are stopped or paused, soft blue cards with an info sign, and a close button, for the
    /// others. The cards are as tall as their text; the area never takes more than half the tab, and the
    /// last cards are left out when it would.
    /// </summary>
    internal class TabNoticeArea : MonoBehaviour
    {
        public const string AreaName = "AIFanChatter_Notices";
        public const string CloseTooltipKey = "AIFANCHATTER__SNS__CLOSE";
        public const string FallbackCloseTooltip = "Close";
        /// <summary>The game's plain rounded picture, as on its save file buttons, tinted per card.</summary>
        public const string CardSprite = "rounded";
        public const char WarningIcon = '';
        public const char InfoIcon = '';
        public const char CloseIcon = '';
        /// <summary>Space inside a card around its text, between cards, and for the close button, in the tab's units.</summary>
        public const float Padding = 4f;
        public const float Spacing = 3f;
        public const float CloseSize = 12f;
        /// <summary>The most of the tab's height the cards take.</summary>
        public const float MaxShare = 0.5f;

        private static readonly Color StatusColor = new(0.85f, 0.25f, 0.25f, 0.16f);
        private static readonly Color OtherColor = new(0.47f, 0.48f, 0.73f, 0.14f);

        private RectTransform area;
        private RectTransform tab;
        private TMP_Text style;
        private float size;
        private Color textColor;
        private Image cardStyle;
        private int drawnVersion = -1;
        private float drawnWidth = -1f;

        /// <summary>The cards' height, 0 with none.</summary>
        public float Height { get; private set; }

        public static TabNoticeArea Create(RectTransform tab, TMP_Text title)
        {
            GameObject area = new(AreaName, typeof(RectTransform));
            RectTransform rect = (RectTransform)area.transform;
            rect.SetParent(tab, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            TabNoticeArea notices = area.AddComponent<TabNoticeArea>();
            notices.area = rect;
            notices.tab = tab;
            // The posts' text, unless its font can't draw the signs; then the title's font at the posts' size
            TMP_Text post = PostText();
            notices.style = post != null && post.font != null && post.font.HasCharacter(WarningIcon, true) ? post : title;
            notices.size = post != null ? post.fontSize : title.fontSize;
            notices.textColor = post != null ? post.color : title.color;
            notices.cardStyle = GameCard();
            return notices;
        }

        /// <summary>The text of the game's posts, for the cards to match it.</summary>
        private static TMP_Text PostText()
        {
            try
            {
                GameObject prefab = Camera.main.GetComponent<mainScript>().Data.GetComponent<SNS_Manager>().prefab_message;
                return prefab.GetComponent<SNS_Message>().Message.GetComponent<TMP_Text>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A loaded game image with the plain rounded picture, or null for square cards.</summary>
        private static Image GameCard()
        {
            foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (image != null && image.sprite != null && image.sprite.name == CardSprite && image.type == Image.Type.Sliced)
                {
                    return image;
                }
            }
            return null;
        }

        /// <summary>
        /// Places the area with its top this far below the tab's top, shown or hidden, and draws the cards
        /// again when the notices or the width changed.
        /// </summary>
        public void Place(float top, float side, bool shown)
        {
            area.offsetMin = new Vector2(side, area.offsetMin.y);
            area.offsetMax = new Vector2(-side, top);
            if (!shown)
            {
                if (Height != 0f || area.childCount > 0)
                {
                    Draw(new List<TabNotice>());
                    drawnVersion = -1;
                }
                return;
            }
            float width = area.rect.width;
            if (TabNotices.Version == drawnVersion && Mathf.Approximately(width, drawnWidth))
            {
                return;
            }
            drawnVersion = TabNotices.Version;
            drawnWidth = width;
            Draw(TabNotices.Shown);
        }

        private void Draw(List<TabNotice> notices)
        {
            foreach (Transform child in area)
            {
                Destroy(child.gameObject);
            }
            float width = area.rect.width;
            float limit = tab.rect.height * MaxShare;
            float y = 0f;
            foreach (TabNotice notice in notices)
            {
                float height = Card(notice, y, width);
                if (y + height > limit && y > 0f)
                {
                    // Out of room: this card and the ones after it are left out
                    Destroy(area.GetChild(area.childCount - 1).gameObject);
                    break;
                }
                y += height + Spacing;
            }
            Height = y > 0f ? y - Spacing : 0f;
            area.offsetMin = new Vector2(area.offsetMin.x, area.offsetMax.y - Height);
        }

        /// <summary>Adds one card at this distance from the area's top, and returns its height.</summary>
        private float Card(TabNotice notice, float y, float width)
        {
            GameObject card = new("Notice", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)card.transform;
            rect.SetParent(area, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            Image image = card.GetComponent<Image>();
            if (cardStyle != null)
            {
                image.sprite = cardStyle.sprite;
                image.type = cardStyle.type;
                image.pixelsPerUnitMultiplier = cardStyle.pixelsPerUnitMultiplier;
            }
            image.color = notice.IsStatus ? StatusColor : OtherColor;
            // Only the close button takes clicks
            image.raycastTarget = false;

            float right = notice.IsStatus ? Padding : Padding + CloseSize;
            Color iconColor = notice.IsStatus ? (Color)mainScript.red32 : (Color)mainScript.blue32;
            TextMeshProUGUI text = Text(rect, "Text");
            // The service's own words can hold tags, so they're shown as they are
            text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(iconColor) + ">" + (notice.IsStatus ? WarningIcon : InfoIcon) + "</color> <noparse>" + notice.Text + "</noparse>";
            text.enableWordWrapping = true;
            float textWidth = Mathf.Max(1f, width - Padding - right);
            float height = text.GetPreferredValues(text.text, textWidth, 0f).y + 2f * Padding;
            RectTransform textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(Padding, Padding);
            textRect.offsetMax = new Vector2(-right, -Padding);
            rect.offsetMin = new Vector2(0f, -y - height);
            rect.offsetMax = new Vector2(0f, -y);

            if (!notice.IsStatus)
            {
                CloseButton(rect, notice);
            }
            return height;
        }

        private void CloseButton(RectTransform card, TabNotice notice)
        {
            TextMeshProUGUI close = Text(card, "Close");
            close.text = CloseIcon.ToString();
            close.color = mainScript.blue32;
            close.alignment = TextAlignmentOptions.Center;
            close.raycastTarget = true;
            RectTransform rect = (RectTransform)close.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(CloseSize, CloseSize);
            rect.anchoredPosition = new Vector2(-Padding * 0.5f, -Padding * 0.5f);
            Button button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = close;
            button.onClick.AddListener(() => TabNotices.Close(notice));
            // The game's click sound, hover sound, pointer cursor and tooltip
            ButtonDefault buttonDefault = close.gameObject.AddComponent<ButtonDefault>();
            buttonDefault.SetTooltip(RefreshButton.Text(CloseTooltipKey, FallbackCloseTooltip));
        }

        private TextMeshProUGUI Text(RectTransform parent, string name)
        {
            GameObject text = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(parent, false);
            TextMeshProUGUI label = text.GetComponent<TextMeshProUGUI>();
            label.font = style.font;
            label.fontSharedMaterial = style.fontSharedMaterial;
            label.fontSize = size;
            label.color = textColor;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            return label;
        }
    }
}
