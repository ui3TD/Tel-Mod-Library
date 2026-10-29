using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UiCopies;

namespace ConcertHype
{
    /// <summary>
    /// The game's own parts of the UI objects the show copies, read from the game with no mods loaded. The game adds
    /// FixScrollWheel to its buttons while it runs. A copied portrait keeps the game's Concert_Girl script, which
    /// does nothing until it is given an idol, and the show never gives it one.
    /// </summary>
    internal static class Shapes
    {
        public static readonly VanillaShape Button = new VanillaShape(
            new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(ButtonDefault), typeof(FixScrollWheel) },
            new[] { "Text" });

        public static readonly VanillaShape Label = new VanillaShape(
            new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup), typeof(Lang_Button), typeof(TextMeshProUGUI) },
            new string[0]);

        /// <summary>
        /// The info icon before the Accident Chance title: a font symbol that shows its tooltip while hovered.
        /// </summary>
        public static readonly VanillaShape Info = new VanillaShape(
            new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Button), typeof(ButtonDefault), typeof(TextMeshProUGUI), typeof(Lang_Button), typeof(FixScrollWheel) },
            new string[0]);

        public static readonly VanillaShape Portrait = new VanillaShape(
            new[] { typeof(RectTransform), typeof(Concert_Girl) },
            new[] { "BG", "Center", "Center/shadow_corner (1)", "Center/shadow_right (1)", "Center/shadow_bottom (1)", "Stamina" });

        /// <summary>
        /// A single's marketing wheel: its turning rings and the dark disc in the middle.
        /// </summary>
        public static readonly VanillaShape Wheel = new VanillaShape(
            new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) },
            new[] { "Spinny_Part", "Spinny_Part/Ring_Fail", "Spinny_Part/Ring_Crit_Fail", "Spinny_Part/Ring_Crit_Succ", "Spinny_Part/Ring_Succ", "Fill" });

        /// <summary>
        /// The marketing wheel's pointer: a font symbol over the top of the wheel.
        /// </summary>
        public static readonly VanillaShape Pointer = new VanillaShape(
            new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI) },
            new string[0]);

        /// <summary>
        /// The settings popup's Quality dropdown: its box, and the list that opens under it.
        /// </summary>
        public static readonly VanillaShape Dropdown = new VanillaShape(
            new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Michsky.UI.ModernUIPack.CustomDropdown), typeof(Button), typeof(Michsky.UI.ModernUIPack.UIManagerDropdown), typeof(Animator) },
            new[] { "Trigger", "Content", "Content/Item List", "Content/Item List/Shadow", "Content/Item List/Scroll Area", "Content/Item List/Scroll Area/List",
                "Content/Item List/Scroll Area/Scrollbar", "Content/Item List/Scroll Area/Scrollbar/Background", "Content/Item List/Scroll Area/Scrollbar/Sliding Area",
                "Content/Item List/Scroll Area/Scrollbar/Sliding Area/Handle", "Content/Main", "Content/Main/Selected Image", "Content/Main/Selected Text", "Content/Main/Expand" });

        /// <summary>
        /// The café popup's idol list scrollbar: a slider with a thin track and a round thumb.
        /// </summary>
        public static readonly VanillaShape Scrollbar = new VanillaShape(
            new[] { typeof(RectTransform), typeof(Slider), typeof(SliderDefault) },
            new[] { "Background", "Fill Area", "Fill Area/Fill", "Handle Slide Area", "Handle Slide Area/Handle" });

        /// <summary>
        /// A tab of the group appeal popup: a tinted box with its label (Unity's own Text) inside.
        /// </summary>
        public static readonly VanillaShape Tab = new VanillaShape(
            new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(ButtonDefault), typeof(FixScrollWheel) },
            new[] { "Text" });
    }

    internal static class Ui
    {
        private const string LogPrefix = "[Concert Hype]";

        public static GameObject Copy(GameObject source, Transform parent, VanillaShape shape, string what, string name)
        {
            GameObject copy = UiCopy.CopyGameObject(source, parent, shape, LogPrefix, what);
            copy.name = name;
            foreach (Lang_Button lang in copy.GetComponentsInChildren<Lang_Button>(true))
                UnityEngine.Object.DestroyImmediate(lang);
            copy.SetActive(true);
            return copy;
        }

        /// <summary>
        /// A copy of one of the game's labels, fully shown. Text never takes a press or a hover from the portrait or
        /// button under it.
        /// </summary>
        public static TextMeshProUGUI CopyLabel(GameObject source, Transform parent, string name)
        {
            GameObject copy = Copy(source, parent, Shapes.Label, "label", name);
            CanvasGroup group = copy.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 1f;
            TextMeshProUGUI text = copy.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            return text;
        }

        public static TextMeshProUGUI Label(GameObject template, Transform parent, string name, float x, float y, float width, float height, float fontSize)
        {
            TextMeshProUGUI text = CopyLabel(template, parent, name);
            Place(text.gameObject, x, y, width, height);
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.text = "";
            return text;
        }

        /// <summary>
        /// The next-song screen's Stamina Cost title, which the show's labels are copied from.
        /// </summary>
        public static GameObject LabelTemplate(Concert_Popup popup) =>
            popup.NextSong_Container.transform.Find("StaminaCostTitle")?.gameObject ?? popup.NextSong_Stamina;

        /// <summary>
        /// The colour of the game's values that are neither good nor bad.
        /// </summary>
        public static Color PlainColor(Concert_Popup popup) =>
            popup.NextSong_AccidentChance?.GetComponent<TextMeshProUGUI>()?.color ?? Color.white;

        /// <summary>
        /// A new empty object in the parent.
        /// </summary>
        public static RectTransform NewChild(Transform parent, string name)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        /// <summary>
        /// Runs a UI action; an error ends the show, which carries on as in the original game.
        /// </summary>
        public static void Safely(Action action, string what)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, what);
            }
        }

        public static Button Button(GameObject template, Transform parent, string name, float x, float y, float width, float height, UnityAction onClick)
        {
            GameObject copy = Copy(template, parent, Shapes.Button, "button", name);
            Place(copy, x, y, width, height);
            Button button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
            return button;
        }

        public static void SetButtonText(Button button, string text)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = text;
        }

        public static void Enable(Button button, bool enabled)
        {
            ButtonDefault buttonDefault = button.GetComponent<ButtonDefault>();
            if (buttonDefault != null)
                buttonDefault.Activate(enabled);
            else
                button.interactable = enabled;
        }

        /// <summary>
        /// Places an object by its top-left corner, in the 1024 x 640 coordinates of the concert popup.
        /// </summary>
        public static void Place(GameObject obj, float x, float y, float width, float height)
        {
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// Places an object by its centre, in the coordinates of <see cref="Place"/>; it scales from its centre.
        /// </summary>
        public static void PlaceCentred(GameObject obj, float x, float y, float width, float height)
        {
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// Gives an object the anchors and pivot of another.
        /// </summary>
        public static void MatchAnchors(RectTransform rect, RectTransform from)
        {
            rect.anchorMin = from.anchorMin;
            rect.anchorMax = from.anchorMax;
            rect.pivot = from.pivot;
        }

        /// <summary>
        /// Where an object is, in the same top-left coordinates as <see cref="Place"/>.
        /// </summary>
        public static Rect Placed(GameObject obj, Transform container)
        {
            Vector3[] corners = new Vector3[4];
            ((RectTransform)obj.transform).GetWorldCorners(corners);
            RectTransform parent = (RectTransform)container;
            Vector3 topLeft = parent.InverseTransformPoint(corners[1]), bottomRight = parent.InverseTransformPoint(corners[3]);
            return Rect.MinMaxRect(topLeft.x - parent.rect.xMin, parent.rect.yMax - topLeft.y, bottomRight.x - parent.rect.xMin, parent.rect.yMax - bottomRight.y);
        }

        /// <summary>
        /// Moves an object so its top-left corner is at x, y in the coordinates of <see cref="Place"/>, keeping its
        /// own anchors and size.
        /// </summary>
        public static void MoveTo(GameObject obj, Transform container, float x, float y)
        {
            Rect now = Placed(obj, container);
            ((RectTransform)obj.transform).anchoredPosition += new Vector2(x - now.xMin, now.yMin - y);
        }

        public static void Stretch(GameObject obj)
        {
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// While hovered, shows its text in the game's tooltip, as a game button with a tooltip does. It takes no
    /// press, so a press on it goes on to the portrait it sits on.
    /// </summary>
    internal sealed class IconTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Text = "";
        private bool hovering;
        private TooltipManager tooltips;

        public void OnPointerEnter(PointerEventData eventData) => hovering = true;

        public void OnPointerExit(PointerEventData eventData) => hovering = false;

        private void OnDisable() => hovering = false;

        private void Update()
        {
            if (!hovering || string.IsNullOrEmpty(Text))
                return;
            if (tooltips == null)
                tooltips = Camera.main?.GetComponent<mainScript>()?.Data?.GetComponent<TooltipManager>();
            if (tooltips == null)
                return;
            // The game hides its tooltips only in a frame nobody shows one, so an idol's profile card left from her
            // picture would stay under this tip: put it away as the game's hide does
            GameObject card = tooltips.TooltipStatusButton;
            if (card != null && card.activeSelf)
            {
                CanvasGroup fade = card.GetComponent<CanvasGroup>();
                // What DOKill does, without the extension the unit tests can't compile against
                DG.Tweening.DOTween.Kill(fade);
                fade.alpha = 0f;
                card.SetActive(false);
            }
            tooltips.ShowTooltip(Text);
        }
    }

    /// <summary>
    /// Scrolls with the mouse wheel only: a drag on the bench drags an idol, not the list.
    /// </summary>
    internal sealed class WheelScroll : ScrollRect
    {
        public override void OnBeginDrag(PointerEventData eventData) { }

        public override void OnDrag(PointerEventData eventData) { }

        public override void OnEndDrag(PointerEventData eventData) { }
    }
}
