using DG.Tweening;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Module = NationalTour.Readiness.Module;

namespace NationalTour
{
    /// <summary>
    /// The tour results popup: a national tour fades and zooms in the National Tour picture, with its blurred
    /// copy behind the results. A world tour shows the game's own background.
    /// </summary>
    public static class ResultsPictures
    {
        public static readonly Vector2 ZOOM_START = new(1024, 0);
        public static readonly Vector2 ZOOM_END = new(1074, 20);
        public const float FADE_SECONDS = 0.5f;
        public const float ZOOM_SECONDS = 4.5f;

        /// <summary>
        /// The tour the results are for. The game resets the popup before it gives the popup the tour.
        /// </summary>
        public static SEvent_Tour.tour Launching;

        // The game's own looks, read before the mod first changes them
        private static bool gameLooksRead;
        private static Sprite gameBlurSprite;
        private static Color gameBlurColor;
        private static Vector2 gameSize;

        // What the mod changed, so it puts back only that
        private static bool blurChanged;
        private static RectTransform zoomed;

        public static void OnReset()
        {
            PopupManager popupManager = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>();
            GameObject background = popupManager.BGImage;
            Image blur = popupManager.GetByType(PopupManager._type.sevent_tour).obj.transform.Find("BG")?.GetComponent<Image>();
            if (background == null || blur == null)
            {
                Readiness.Fail(Module.ResultsPictures, "the tour results popup has no background");
                return;
            }
            RectTransform rect = background.GetComponent<RectTransform>();

            if (!gameLooksRead)
            {
                gameBlurSprite = blur.sprite;
                gameBlurColor = blur.color;
                gameSize = rect.sizeDelta;
                gameLooksRead = true;
            }

            if (Prefectures.IsNational(Launching) && Pictures.World_tour_def_tex != null && Pictures.World_tour_def_BG != null)
            {
                // Fade in over the game's background (PopupManager.Open has just started its own fade), zooming in slightly
                background.GetComponent<RawImage>().texture = Pictures.World_tour_def_tex;
                background.GetComponent<CanvasGroup>().alpha = 0f;
                rect.sizeDelta = ZOOM_START;
                TweenSettingsExtensions.SetEase(DOTweenModuleUI.DOFade(background.GetComponent<CanvasGroup>(), 1f, FADE_SECONDS), Ease.OutQuint);
                TweenSettingsExtensions.SetEase(DOTweenModuleUI.DOSizeDelta(rect, ZOOM_END, ZOOM_SECONDS), Ease.OutQuint);
                zoomed = rect;

                blur.color = Color.white;
                blur.sprite = Pictures.World_tour_def_BG;
                blurChanged = true;
            }
            else
            {
                RestoreSize();
                if (blurChanged)
                {
                    blur.sprite = gameBlurSprite;
                    blur.color = gameBlurColor;
                    blurChanged = false;
                }
            }
        }

        /// <summary>
        /// The background is shared by every popup, so its zoom ends with the results.
        /// </summary>
        public static void RestoreSize()
        {
            if (zoomed == null)
                return;
            ShortcutExtensions.DOKill(zoomed, false);
            zoomed.sizeDelta = gameSize;
            zoomed = null;
        }

        /// <summary>
        /// For tests: a new session.
        /// </summary>
        internal static void Reset()
        {
            Launching = null;
            gameLooksRead = false;
            gameBlurSprite = null;
            gameBlurColor = default;
            gameSize = default;
            blurChanged = false;
            zoomed = null;
        }
    }


    // Which tour the results popup is about to show
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.StartTour))]
    public class SEvent_Tour_StartTour
    {
        public static void Prefix(SEvent_Tour __instance) => ResultsPictures.Launching = __instance.Tour;
    }


    [HarmonyPatch(typeof(Tour_Popup), nameof(Tour_Popup.Reset))]
    public class Tour_Popup_Reset
    {
        public static void Postfix() => ResultsPictures.OnReset();
    }


    [HarmonyPatch(typeof(Tour_Popup), nameof(Tour_Popup.OnFinish))]
    public class Tour_Popup_OnFinish
    {
        public static void Prefix() => ResultsPictures.RestoreSize();
    }
}
