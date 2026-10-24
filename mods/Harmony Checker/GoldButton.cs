using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HarmonyChecker
{
    /// <summary>
    /// A gold copy of the game's menu button picture, which the game only has in blue, green and red,
    /// and a soft halo around it that pulses to draw the eye and holds bright while the mouse is over it.
    /// </summary>
    internal static class GoldButton
    {
        // The halo picture reaches this many pixels past the button on each side.
        public const float GLOW_PAD = 16f;

        // The game's button picture's 9-slice edges (left, bottom, right, top), so it stretches the same way.
        public static readonly ModPicture Button = new ModPicture("IMHI_Update.png", new Vector4(11f, 17f, 11f, 12f));
        public static readonly ModPicture Glow = new ModPicture("IMHI_Update_Glow.png",
            new Vector4(11f + GLOW_PAD, 17f + GLOW_PAD, 11f + GLOW_PAD, 12f + GLOW_PAD));

        /// <summary>
        /// Puts the halo over the button, reaching past its edges. The halo is clear over the button
        /// itself, so it doesn't cover the button or its text. Without the picture the button has no halo.
        /// </summary>
        public static void AddGlow(GameObject button)
        {
            Sprite sprite = Glow.Get();
            if (sprite == null)
            {
                return;
            }

            GameObject glow = new GameObject("Glow", typeof(RectTransform));
            glow.transform.SetParent(button.transform, false);
            glow.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform rect = (RectTransform)glow.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-GLOW_PAD, -GLOW_PAD);
            rect.offsetMax = new Vector2(GLOW_PAD, GLOW_PAD);

            Image image = glow.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            GlowPulse pulse = glow.AddComponent<GlowPulse>();
            pulse.Image = image;
            button.AddComponent<GlowHover>().Pulse = pulse;
        }
    }

    /// <summary>
    /// Fades the halo in and out, once every PERIOD seconds, on real time so a paused game still pulses.
    /// While the mouse is over the button the halo brightens to full and stays there; when the mouse
    /// leaves, the pulse carries on from the halo's brightness at that moment.
    /// </summary>
    internal class GlowPulse : MonoBehaviour
    {
        public const float PERIOD = 2f;
        public const float MIN_ALPHA = 0.1f;
        public const float MAX_ALPHA = 1f;

        // Seconds the halo takes to brighten from its dimmest to full when the mouse moves onto the button.
        public const float FADE_IN = 0.2f;

        public Image Image;

        private bool hovered;
        private bool restart;
        private float start;
        private float alpha = MIN_ALPHA;

        public bool Hovered
        {
            get => hovered;
            set
            {
                restart |= hovered && !value;
                hovered = value;
            }
        }

        public static float AlphaAt(float time) =>
            MIN_ALPHA + (MAX_ALPHA - MIN_ALPHA) * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * time / PERIOD));

        /// <summary>The time in the dimming half of the pulse at which AlphaAt gives this alpha.</summary>
        public static float FadingTimeAt(float alpha)
        {
            float bright = Mathf.Clamp01((alpha - MIN_ALPHA) / (MAX_ALPHA - MIN_ALPHA));
            return PERIOD - Mathf.Acos(1f - 2f * bright) * PERIOD / (2f * Mathf.PI);
        }

        /// <summary>The halo's alpha one frame later: brightening toward full while hovered, else the pulse.</summary>
        public static float Step(float alpha, bool hovered, float pulseTime, float deltaTime) => hovered
            ? Mathf.MoveTowards(alpha, MAX_ALPHA, (MAX_ALPHA - MIN_ALPHA) * deltaTime / FADE_IN)
            : AlphaAt(pulseTime);

        private void Update()
        {
            if (!hovered && restart)
            {
                start = Time.unscaledTime - FadingTimeAt(alpha);
                restart = false;
            }

            alpha = Step(alpha, hovered, Time.unscaledTime - start, Time.unscaledDeltaTime);
            if (Image != null)
            {
                Color color = Image.color;
                color.a = alpha;
                Image.color = color;
            }
        }

        // A hidden button gets no mouse-leave, so it stops counting as hovered here.
        private void OnDisable() => Hovered = false;
    }

    /// <summary>
    /// Tells the halo when the mouse is over the button. It sits on the button, since the halo takes no mouse events.
    /// </summary>
    internal class GlowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GlowPulse Pulse;

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);

        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        private void SetHovered(bool hovered)
        {
            if (!(Pulse is null))
            {
                Pulse.Hovered = hovered;
            }
        }
    }
}
