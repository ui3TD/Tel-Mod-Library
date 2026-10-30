using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConcertHype
{
    /// <summary>
    /// Shows or hides the preview on the game's hype bar each frame: it shows only while the next-song screen's panel
    /// is up. It sits on the hype bar, which stays up while the panel's objects are hidden during songs.
    /// </summary>
    internal sealed class HypePreviewDriver : MonoBehaviour
    {
        private void Update() => HypePreview.Follow(LineupPanel.Showing);
    }

    /// <summary>
    /// Drawn on the game's hype bar while the next-song screen is up: a light segment past the fill to where the
    /// shown tab's next song or talk break should take the hype, or a dark one over the fill's end for a loss, and a
    /// thin marker where the hype was before the last song or talk break. Hovering the bar shows the numbers. The
    /// bar's own value, colour, text and animation stay the game's: this only adds objects of its own.
    /// </summary>
    internal static class HypePreview
    {
        private const string BackName = "ConcertHype Preview Back", FrontName = "ConcertHype Preview Front", HoverName = "ConcertHype Preview Hover";
        private const float MarkerWidth = 2f;
        private static readonly Color GainColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color LossColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color MarkerColor = new Color(1f, 1f, 1f, 0.9f);

        private static Concert_HypeBar bar;
        private static GameObject back, front;
        private static Image gain, loss, marker, hoverImage;
        private static IconTip hover;

        private static bool Alive => bar != null && back != null && front != null && hover != null;

        /// <summary>
        /// Draws the view on the popup's hype bar; leaves it out, logged once, when the game's bar isn't there or
        /// doesn't fill from its left edge.
        /// </summary>
        public static void Set(Concert_Popup popup, HypeBarView view)
        {
            try
            {
                if (!Ensure(popup))
                    return;
                float now = PanelView.HypeShare(view.Hype), to = view.HasTarget ? PanelView.HypeShare(view.Target) : now;
                Span(gain, now, to);
                Span(loss, to, now);
                marker.gameObject.SetActive(view.HasBefore);
                if (view.HasBefore)
                {
                    RectTransform rect = marker.rectTransform;
                    float at = PanelView.HypeShare(view.Before);
                    rect.anchorMin = new Vector2(at, 0f);
                    rect.anchorMax = new Vector2(at, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(MarkerWidth, 0f);
                }
                hover.Text = view.Tip;
                Follow(LineupPanel.Showing);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("hype preview", "couldn't draw on the game's hype bar; the preview on it is left out: " + ex.Message);
            }
        }

        /// <summary>
        /// Shown only while the next-song screen is up; the hover goes quiet with it.
        /// </summary>
        public static void Follow(bool show)
        {
            if (!Alive)
                return;
            if (back.activeSelf != show)
                back.SetActive(show);
            if (front.activeSelf != show)
                front.SetActive(show);
            hoverImage.raycastTarget = show && hover.Text.Length > 0;
        }

        public static void Hide()
        {
            if (!Alive)
                return;
            hover.Text = "";
            Follow(false);
        }

        /// <summary>
        /// A stretch of the bar from one share of it to a later one; hidden when it's empty.
        /// </summary>
        private static void Span(Image image, float from, float to)
        {
            bool shown = to - from > 1e-4f;
            image.gameObject.SetActive(shown);
            if (!shown)
                return;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static bool Ensure(Concert_Popup popup)
        {
            Concert_HypeBar hypeBar = popup.Hype != null ? popup.Hype.GetComponent<Concert_HypeBar>() : null;
            if (Alive && ReferenceEquals(hypeBar, bar))
                return true;
            bar = null;
            RectTransform fill = hypeBar != null && hypeBar.BarValue != null ? hypeBar.BarValue.transform as RectTransform : null;
            if (fill == null || fill.parent == null)
            {
                Log.WarnOnce("hype bar", "couldn't find the game's hype bar; the preview on it is left out");
                return false;
            }
            // The game fills the bar by scaling it from there
            if (Math.Abs(fill.pivot.x) > 0.01f)
            {
                Log.WarnOnce("hype bar pivot", "the game's hype bar doesn't fill from its left edge; the preview on it is left out");
                return false;
            }
            // Behind the fill, so a gain shows past its end; just over it, so whatever the game draws later stays on top
            back = Layer(fill, BackName);
            back.transform.SetSiblingIndex(fill.GetSiblingIndex());
            front = Layer(fill, FrontName);
            front.transform.SetSiblingIndex(fill.GetSiblingIndex() + 1);
            gain = Piece(back.transform, "Gain", GainColor);
            loss = Piece(front.transform, "Loss", LossColor);
            marker = Piece(front.transform, "Marker", MarkerColor);

            Transform whole = hypeBar.transform, found = whole.Find(HoverName);
            GameObject hoverObj = found != null ? found.gameObject : Ui.NewChild(whole, HoverName).gameObject;
            Ui.Stretch(hoverObj);
            hoverObj.transform.SetAsLastSibling();
            hoverImage = Component<Image>(hoverObj);
            hoverImage.color = new Color(1f, 1f, 1f, 0f);
            hover = Component<IconTip>(hoverObj);
            Component<HypePreviewDriver>(hoverObj);
            bar = hypeBar;
            return true;
        }

        private static T Component<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            return component != null ? component : obj.AddComponent<T>();
        }

        /// <summary>
        /// An object of our own beside the game's fill that covers the whole bar, as the fill does at 200%.
        /// </summary>
        private static GameObject Layer(RectTransform fill, string name)
        {
            Transform found = fill.parent.Find(name);
            RectTransform rect = found != null ? (RectTransform)found : Ui.NewChild(fill.parent, name);
            Ui.MatchAnchors(rect, fill);
            rect.sizeDelta = fill.sizeDelta;
            rect.anchoredPosition = fill.anchoredPosition;
            rect.localScale = Vector3.one;
            return rect.gameObject;
        }

        private static Image Piece(Transform parent, string name, Color color)
        {
            Transform found = parent.Find(name);
            GameObject obj = found != null ? found.gameObject : Ui.NewChild(parent, name).gameObject;
            Image image = Component<Image>(obj);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
