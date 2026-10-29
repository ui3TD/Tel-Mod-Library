using ConcertHype.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public static TextMeshProUGUI Label(GameObject template, Transform parent, string name, float x, float y, float width, float height, float fontSize)
        {
            GameObject copy = Copy(template, parent, Shapes.Label, "label", name);
            CanvasGroup group = copy.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 1f;
            Place(copy, x, y, width, height);
            TextMeshProUGUI text = copy.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            // Text never takes a press or a hover from the portrait or button under it
            text.raycastTarget = false;
            text.text = "";
            return text;
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

        public static void SetEnergyBar(Image bar, double energy)
        {
            if (bar == null)
                return;
            bar.color = energy < 30 ? mainScript.red32 : energy >= 70 ? mainScript.green32 : mainScript.blue32;
            bar.transform.localScale = new Vector3((float)(energy / 100.0), 1f, 1f);
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
            tooltips?.ShowTooltip(Text);
        }
    }

    /// <summary>
    /// A lineup icon with its meaning as a tooltip, and a number beside it (how many songs a hurt idol is out).
    /// Without its picture it shows the old text symbol.
    /// </summary>
    internal sealed class IconWidget
    {
        public const float Size = 16f;

        public GameObject Root;
        public Image Image;
        public IconTip Tip;
        public TextMeshProUGUI Text;

        public static IconWidget Make(GameObject labelTemplate, Transform parent, string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            IconWidget icon = new IconWidget { Root = root, Image = root.AddComponent<Image>(), Tip = root.AddComponent<IconTip>() };
            icon.Image.preserveAspect = true;
            icon.Text = Ui.Label(labelTemplate, root.transform, "Text", -Size - 10f, -1f, Size + 8f, Size, 12f);
            icon.Text.alignment = TextAlignmentOptions.TopRight;
            icon.Text.fontStyle = FontStyles.Bold;
            icon.Text.color = Color.white;
            Ui.Place(root, 0f, 0f, Size, Size);
            return icon;
        }

        public void Set(IconKind kind, string tip, string count = "")
        {
            Root.SetActive(kind != IconKind.None);
            if (kind == IconKind.None)
                return;
            Sprite sprite = Icons.Get(kind);
            Image.sprite = sprite;
            Image.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            Tip.Text = tip;
            Text.text = sprite != null ? count : PanelView.FallbackText(kind, count);
        }

        public void PlaceAt(float x, float y) => Ui.Place(Root, x, y, Size, Size);
    }

    /// <summary>
    /// A copy of one of the game's tabs (the group appeal popup's Total Fans), tinted as the game tints its tabs:
    /// full for the tab shown, faded for the other. Without the game's tab, a copy of the Start button.
    /// </summary>
    internal sealed class TabWidget
    {
        /// <summary>
        /// The tabs' top: under the song's title, which ends about 122 down, and above the formation.
        /// </summary>
        public const float Top = 128f;

        /// <summary>
        /// Narrower than the game's tab, so the two clear the readouts' titles; the label shrinks to fit, as the
        /// game's does.
        /// </summary>
        public const float Width = 105f;

        public Button Button;
        private Image box;
        private Text text;
        private TextMeshProUGUI tmpText;

        /// <summary>
        /// The game's tab, or the Start button (logged once) when it can't be found.
        /// </summary>
        public static GameObject Template(Concert_Popup popup)
        {
            GameObject tab = PopupManager.GetObject(PopupManager._type.group_appeal)?.GetComponent<Group_Appeal_Popup>()?.Tab_TotalFans;
            if (tab != null && tab.GetComponent<Button>() != null && tab.GetComponent<Image>() != null && tab.transform.Find("Text")?.GetComponent<Text>() != null)
                return tab;
            Log.WarnOnce("tab", "couldn't find the game's tab; the tabs are copies of the Start button");
            return popup.NextSong_Button;
        }

        /// <summary>
        /// The tab's width, at the template's own height as the game lays it out.
        /// </summary>
        public static Vector2 Size(GameObject template)
        {
            float height = ((RectTransform)template.transform).rect.height;
            return new Vector2(Width, height > 1f ? height : 24f);
        }

        public static TabWidget Make(GameObject template, Transform parent, string name, float x, float y, Vector2 size, UnityAction onClick)
        {
            bool gameTab = template.GetComponent<Text>() == null && template.transform.Find("Text")?.GetComponent<Text>() != null;
            GameObject copy = Ui.Copy(template, parent, gameTab ? Shapes.Tab : Shapes.Button, gameTab ? "tab" : "button", name);
            Ui.Place(copy, x, y, size.x, size.y);
            TabWidget tab = new TabWidget { Button = copy.GetComponent<Button>(), box = copy.GetComponent<Image>() };
            // A new event drops the game's own listener, which would switch the group appeal popup's tab
            tab.Button.onClick = new Button.ButtonClickedEvent();
            tab.Button.onClick.AddListener(onClick);
            tab.text = copy.transform.Find("Text")?.GetComponent<Text>();
            if (tab.text == null)
                tab.tmpText = copy.GetComponentInChildren<TextMeshProUGUI>(true);
            return tab;
        }

        /// <summary>
        /// A disabled tab still shows its tooltip, as the game's disabled buttons can.
        /// </summary>
        public void Enable(bool enabled, string tip)
        {
            ButtonDefault buttonDefault = Button.GetComponent<ButtonDefault>();
            if (buttonDefault == null)
            {
                Button.interactable = enabled;
                return;
            }
            buttonDefault.Activate(enabled, showTooltip: !enabled);
            buttonDefault.SetTooltip(enabled ? "" : tip);
        }

        public void Set(string label, bool shown)
        {
            if (text != null)
                text.text = label;
            if (tmpText != null)
                tmpText.text = label;
            if (box != null)
                box.color = shown ? mainScript.blue_tab_active : mainScript.blue_tab_not_active;
        }
    }

    /// <summary>
    /// While the show's panel is up, the next song's title ends before the tabs: a long one shrinks, and past the
    /// smallest size it ends in an ellipsis. The game's settings come back when the panel goes.
    /// </summary>
    internal static class SongTitle
    {
        private const float SmallestShare = 0.6f;

        private static TextMeshProUGUI title;
        private static Vector2 offsetMax;
        private static bool autoSizing, wrapping;
        private static float fontSize, fontSizeMin, fontSizeMax;
        private static TextOverflowModes overflow;

        /// <param name="right">Where the title's right edge goes, in <see cref="Ui.Place"/> coordinates.</param>
        public static void Fit(GameObject obj, Transform container, float right)
        {
            TextMeshProUGUI text = obj != null ? obj.GetComponent<TextMeshProUGUI>() : null;
            if (text == null)
            {
                Log.WarnOnce("title", "couldn't find the next song's title; a long one may run under the tabs");
                return;
            }
            if (title != text)
            {
                Restore();
                title = text;
                offsetMax = text.rectTransform.offsetMax;
                autoSizing = text.enableAutoSizing;
                wrapping = text.enableWordWrapping;
                fontSize = text.fontSize;
                fontSizeMin = text.fontSizeMin;
                fontSizeMax = text.fontSizeMax;
                overflow = text.overflowMode;
            }
            RectTransform rect = text.rectTransform;
            rect.offsetMax = offsetMax;
            float over = Ui.Placed(obj, container).xMax - right;
            if (over > 0f)
                rect.offsetMax = new Vector2(offsetMax.x - over, offsetMax.y);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * SmallestShare;
        }

        public static void Restore()
        {
            if (title == null)
                return;
            title.rectTransform.offsetMax = offsetMax;
            title.enableAutoSizing = autoSizing;
            title.enableWordWrapping = wrapping;
            title.fontSizeMin = fontSizeMin;
            title.fontSizeMax = fontSizeMax;
            title.fontSize = fontSize;
            title.overflowMode = overflow;
            title = null;
        }
    }

    /// <summary>
    /// The game's info icon (the one before Accident Chance) and where it sits from the title after it.
    /// </summary>
    internal sealed class InfoIcon
    {
        public GameObject Template;

        /// <summary>
        /// How far right of the icon its title starts, and how far below the title's top the icon is.
        /// </summary>
        public float Gap, Drop;

        /// <summary>
        /// Null when the next-song screen has no info icon (logged once).
        /// </summary>
        public static InfoIcon From(Concert_Popup popup)
        {
            Transform container = popup.NextSong_Container.transform;
            GameObject icon = popup.NextSong_AccidentChanceInfo, title = container.Find("AccidentChanceTitle")?.gameObject;
            if (icon == null || title == null || icon.GetComponent<ButtonDefault>() == null)
            {
                Log.WarnOnce("info icon", "couldn't find the game's info icon; tips show when the title is hovered");
                return null;
            }
            Rect at = Ui.Placed(icon, container), titleAt = Ui.Placed(title, container);
            return new InfoIcon { Template = icon, Gap = titleAt.xMin - at.xMin, Drop = at.yMin - titleAt.yMin };
        }
    }

    /// <summary>
    /// A title over a value, copied from a pair of the game's (Stamina Cost, or Profit on the results screen) so it
    /// has their font, size, colour and spacing, with the game's info icon before the title when it has a tip.
    /// </summary>
    internal sealed class ReadoutWidget
    {
        public GameObject Root;
        public TextMeshProUGUI Title, Value;
        public GameObject Info;
        private ButtonDefault infoTip;
        private IconTip titleTip;
        private Color plain;

        /// <param name="x">Where the value's left edge goes; the title keeps the game's offset from it.</param>
        /// <param name="y">Where the title's top goes; the value keeps the game's offset below it.</param>
        /// <param name="plain">The value's colour when it's neither good nor bad: the game colours its values itself.</param>
        public static ReadoutWidget Make(Transform parent, Transform container, string name, GameObject title, GameObject value, InfoIcon info, float x, float y, Color plain)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Ui.Stretch(root);
            Rect titleAt = Ui.Placed(title, container), valueAt = Ui.Placed(value, container);
            float titleX = x + titleAt.xMin - valueAt.xMin + (info != null ? info.Gap : 0f);
            ReadoutWidget widget = new ReadoutWidget { Root = root, plain = plain };
            widget.Title = Copy(title, root.transform, container, "Title", titleX, y);
            widget.Value = Copy(value, root.transform, container, "Value", x, y + valueAt.yMin - titleAt.yMin);
            if (info != null)
            {
                widget.Info = Ui.Copy(info.Template, root.transform, Shapes.Info, "info icon", "Info");
                Ui.MoveTo(widget.Info, container, titleX - info.Gap, y + info.Drop);
                Button button = widget.Info.GetComponent<Button>();
                if (button != null)
                    button.onClick = new Button.ButtonClickedEvent();
                widget.infoTip = widget.Info.GetComponent<ButtonDefault>();
            }
            else
            {
                // Without the game's icon, the title itself shows the tip
                widget.titleTip = widget.Title.gameObject.AddComponent<IconTip>();
            }
            return widget;
        }

        private static TextMeshProUGUI Copy(GameObject source, Transform parent, Transform container, string name, float x, float y)
        {
            GameObject copy = Ui.Copy(source, parent, Shapes.Label, "label", name);
            CanvasGroup group = copy.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 1f;
            Ui.MoveTo(copy, container, x, y);
            TextMeshProUGUI text = copy.GetComponent<TextMeshProUGUI>();
            // Text never takes a press or a hover from the portrait or button under it
            text.raycastTarget = false;
            return text;
        }

        public void Set(Readout readout)
        {
            Root.SetActive(readout.Shown);
            if (!readout.Shown)
                return;
            Title.text = readout.Title;
            Value.text = readout.Value;
            Value.color = readout.Tone == Tone.Good ? mainScript.green32 : readout.Tone == Tone.Bad ? mainScript.red32 : plain;
            bool tip = readout.Tip.Length > 0;
            if (infoTip != null)
            {
                Info.SetActive(tip);
                if (tip)
                    infoTip.SetTooltip(readout.Tip);
            }
            if (titleTip != null)
            {
                titleTip.Text = readout.Tip;
                Title.raycastTarget = tip;
            }
        }
    }

    /// <summary>
    /// A copy of the concert's idol portrait (the picture is a game button, with its hover profile) whose energy
    /// bar shows the show's energy, with a short label and an icon. Pressing and holding the picture drags her.
    /// </summary>
    internal sealed class PortraitWidget
    {
        public GameObject Root;
        public CanvasGroup Group;
        public Button Button;
        public ButtonDefault Hover;
        public DragSource Drag;
        public Image Picture;
        public Image Background;
        public Image Bar;
        public TextMeshProUGUI Label;
        public IconWidget Icon;
        private Color backgroundColor;
        private Image.Type pictureType;
        private float pictureUnits = 1f;
        private bool hasGirl;
        private readonly List<GameObject> shadows = new List<GameObject>();

        /// <param name="iconOnPicture">The icon sits on the picture (the bench), else beside the label under it.</param>
        public static PortraitWidget Make(GameObject prefab, GameObject labelTemplate, Transform parent, string name, DropPoint from, bool iconOnPicture = false)
        {
            PortraitWidget widget = new PortraitWidget { Root = Ui.Copy(prefab, parent, Shapes.Portrait, "idol portrait", name) };
            widget.Group = widget.Root.AddComponent<CanvasGroup>();
            Transform center = widget.Root.transform.Find("Center");
            widget.Picture = center?.GetComponent<Image>();
            if (widget.Picture != null)
            {
                widget.pictureType = widget.Picture.type;
                widget.pictureUnits = widget.Picture.pixelsPerUnitMultiplier;
            }
            widget.Button = center?.GetComponent<Button>();
            widget.Hover = center?.GetComponent<ButtonDefault>();
            widget.Background = widget.Root.transform.Find("BG")?.GetComponent<Image>();
            widget.Bar = widget.Root.transform.Find("Stamina")?.GetComponent<Image>();
            if (widget.Background != null)
                widget.backgroundColor = widget.Background.color;
            if (widget.Button != null)
            {
                // The game's portrait is a button only for its hover profile: its Button starts disabled. A drop is
                // found as the game finds it, by the button the mouse hovers, so the picture's thin shadows (on top
                // of it) don't take the hover.
                widget.Button.onClick = new Button.ButtonClickedEvent();
                widget.Button.enabled = true;
                Ui.Enable(widget.Button, true);
                foreach (Graphic shadow in center.GetComponentsInChildren<Graphic>(true))
                {
                    if (shadow.gameObject == center.gameObject)
                        continue;
                    shadow.raycastTarget = false;
                    widget.shadows.Add(shadow.gameObject);
                }
                widget.Drag = center.gameObject.AddComponent<DragSource>();
                widget.Drag.From = from;
            }
            widget.Label = Ui.Label(labelTemplate, widget.Root.transform, "Label", -20f, 58f, 46f, 18f, 13f);
            widget.Label.alignment = TextAlignmentOptions.TopRight;
            // On the picture, the icon is part of it: a press there still drags her
            widget.Icon = IconWidget.Make(labelTemplate, iconOnPicture && center != null ? center : widget.Root.transform, "Icon");
            widget.Icon.PlaceAt(iconOnPicture ? 27f : 29f, iconOnPicture ? 27f : 59f);
            return widget;
        }

        /// <summary>
        /// An empty place looks like the game's empty senbatsu place: its framed box, with no shadows.
        /// </summary>
        public void SetGirl(data_girls.girls girl)
        {
            GirlProfileOnHover hover = Picture?.GetComponent<GirlProfileOnHover>();
            hasGirl = girl != null;
            foreach (GameObject shadow in shadows)
                shadow.SetActive(hasGirl);
            if (girl is null)
            {
                if (hover != null)
                    hover.enabled = false;
                if (Picture != null)
                {
                    Image empty = Icons.GameEmptyPlace();
                    Picture.sprite = empty?.sprite;
                    Picture.type = empty != null ? empty.type : pictureType;
                    Picture.pixelsPerUnitMultiplier = empty != null ? empty.pixelsPerUnitMultiplier : pictureUnits;
                    Picture.color = empty != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
                return;
            }
            if (Picture != null)
            {
                Picture.type = pictureType;
                Picture.pixelsPerUnitMultiplier = pictureUnits;
                Picture.color = Color.white;
            }
            if (hover != null)
            {
                hover.enabled = true;
                hover.Set(girl);
            }
        }

        public void SetDimmed(bool dimmed)
        {
            if (Picture != null && hasGirl)
                Picture.color = dimmed ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
            if (Bar != null)
            {
                Color bar = Bar.color;
                Bar.color = new Color(bar.r, bar.g, bar.b, dimmed ? 0.45f : 1f);
            }
        }

        public void SetHighlight(Highlight highlight)
        {
            if (Background != null)
                Background.color = highlight == Highlight.Allowed ? (Color)mainScript.green32 : highlight == Highlight.Refused ? (Color)mainScript.red32
                    : backgroundColor;
        }
    }

    /// <summary>
    /// Press and hold drags the idol, a quick click is a click: as the game's own drags (GirlDraggable), held for
    /// 0.1 s it's a drag.
    /// </summary>
    internal sealed class DragSource : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public const float HoldSeconds = 0.1f;

        public DropPoint From;

        /// <summary>
        /// The idol of a bench portrait; a place or the host place drags whoever is there.
        /// </summary>
        public int BenchMember = -1;

        private Coroutine hold;
        private bool dragging;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            dragging = false;
            if (hold != null)
                StopCoroutine(hold);
            hold = StartCoroutine(Hold());
        }

        private IEnumerator Hold()
        {
            yield return new WaitForSeconds(HoldSeconds);
            hold = null;
            if (Input.GetMouseButton(0))
                dragging = LineupPanel.BeginDrag(LineupPanel.MemberAt(From, BenchMember), From);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            if (hold != null)
            {
                StopCoroutine(hold);
                hold = null;
                LineupPanel.OnClick(LineupPanel.MemberAt(From, BenchMember), From);
                return;
            }
            if (dragging)
            {
                dragging = false;
                LineupPanel.EndDrag();
            }
        }

        private void OnDisable()
        {
            hold = null;
            if (dragging)
            {
                dragging = false;
                LineupPanel.EndDrag(cancel: true);
            }
        }
    }

    /// <summary>
    /// Follows the mouse while an idol is dragged.
    /// </summary>
    internal sealed class PanelDriver : MonoBehaviour
    {
        private void Update() => LineupPanel.Tick();
    }

    /// <summary>
    /// The lineup panel on the next-song screen, in place of the game's center, stamina and accident readouts: two
    /// tabs, one with the song's formation and one with the talk break's row of hosts, and readouts like the game's
    /// saying how the song or talk break is expected to go.
    /// </summary>
    internal static class LineupPanel
    {
        private const string Name = "ConcertHype Lineup";
        private const float TriangleCenterX = 630f, TriangleTop = 160f, RowHeight = PanelView.RowHeight, CellWidth = PanelView.CellWidth;

        /// <summary>
        /// The space kept between the formation and what's around it, and between the Last readout and the buttons.
        /// </summary>
        private const float Gap = 16f;

        /// <summary>
        /// The readouts sit as the game's do (Center, Stamina Cost), in two columns left of the formation: the
        /// second row a readout's height below the first, the second column where the first's titles end.
        /// </summary>
        private const float SecondRow = 131f, SecondColumnX = 290f, HintTop = 470f, HintWidth = 470f, HintGap = 10f;

        /// <summary>
        /// The song's title ends this far left of the tabs.
        /// </summary>
        private const float TitleGap = 16f;

        private static Concert_Popup owner;
        private static GameObject root;
        private static ReadoutWidget expected, changed, last;
        private static TextMeshProUGUI hint;
        private static Button resetLineup;
        private static TabWidget songTab, talkTab;
        private static GameObject talkBox;
        private static readonly List<PortraitWidget> Slots = new List<PortraitWidget>();
        private static readonly List<PortraitWidget> HostPlaces = new List<PortraitWidget>();
        private static readonly List<GameObject> CancelOverlays = new List<GameObject>();
        private static readonly List<GameObject> Hidden = new List<GameObject>();
        private static TextMeshProUGUI startLabel;
        private static string gameStartText;
        private static bool subscribed;

        private static DragView drag;
        private static string message;

        /// <summary>
        /// The tab shown: the song's on each new next-song screen.
        /// </summary>
        public static FormationTab Tab { get; private set; }

        private static PanelView.FormationFrame frame = new PanelView.FormationFrame { CenterX = TriangleCenterX, Scale = 1f };

        /// <summary>
        /// How much the formation's places are scaled on the next-song screen: the stage's portraits start from it.
        /// </summary>
        public static float FormationScale => frame.Scale;

        private static bool Alive => root != null && owner != null;

        public static void Show(Concert_Popup popup, ShowSession session)
        {
            Ensure(popup);
            drag = null;
            message = null;
            Tab = FormationTab.Song;
            HideVanilla(popup);
            root.SetActive(true);
            Render();
        }

        /// <summary>
        /// A show the mod doesn't run: the game's readouts come back and the panel goes.
        /// </summary>
        public static void Hide(Concert_Popup popup)
        {
            if (Alive && ReferenceEquals(owner, popup))
            {
                root.SetActive(false);
                SetStartLabel(null);
            }
            SongTitle.Restore();
            foreach (GameObject obj in Hidden)
            {
                if (obj != null)
                    obj.SetActive(true);
            }
            Hidden.Clear();
        }

        private static void HideVanilla(Concert_Popup popup)
        {
            List<GameObject> objects = new List<GameObject> { popup.NextSong_Stamina, popup.NextSong_AccidentChance, popup.NextSong_AccidentChanceInfo };
            foreach (string title in new[] { "CenterTitle", "StaminaCostTitle", "AccidentChanceTitle" })
                objects.Add(popup.NextSong_Container.transform.Find(title)?.gameObject);
            foreach (GameObject obj in objects)
            {
                if (obj != null && obj.activeSelf)
                {
                    obj.SetActive(false);
                    Hidden.Add(obj);
                }
            }
            // The game turns these on again for each song
            foreach (GameObject center in popup.NextSong_Center)
            {
                if (center != null)
                    center.SetActive(false);
            }
        }

        private static void Ensure(Concert_Popup popup)
        {
            if (Alive && ReferenceEquals(owner, popup))
                return;
            owner = popup;
            Slots.Clear();
            HostPlaces.Clear();
            CancelOverlays.Clear();
            Hidden.Clear();
            Transform container = popup.NextSong_Container.transform;
            root = new GameObject(Name, typeof(RectTransform));
            root.transform.SetParent(container, false);
            Ui.Stretch(root);
            root.AddComponent<PanelDriver>();

            GameObject labelTemplate = container.Find("StaminaCostTitle")?.gameObject ?? popup.NextSong_Stamina;
            // Titles and values copied from the game's, in its places: the first column where Center is, the
            // second row where the center's portrait would end
            GameObject centerTitle = container.Find("CenterTitle")?.gameObject ?? labelTemplate;
            Rect first = Ui.Placed(centerTitle, container), value = Ui.Placed(popup.NextSong_Stamina, container), title = Ui.Placed(labelTemplate, container);
            float columnX = first.xMin + value.xMin - title.xMin;
            Color plain = popup.NextSong_AccidentChance?.GetComponent<TextMeshProUGUI>()?.color ?? Color.white;
            InfoIcon info = InfoIcon.From(popup);
            expected = ReadoutWidget.Make(root.transform, container, "Expected", labelTemplate, popup.NextSong_Stamina, info, columnX, first.yMin, plain);
            changed = ReadoutWidget.Make(root.transform, container, "Changed", labelTemplate, popup.NextSong_Stamina, info, SecondColumnX, first.yMin, plain);
            last = ReadoutWidget.Make(root.transform, container, "Last", labelTemplate, popup.NextSong_Stamina, null, SecondColumnX, first.yMin + SecondRow, plain);
            // The talk break's tab: its hosts in a row where the formation's top would be
            talkBox = new GameObject("TalkBreak", typeof(RectTransform));
            talkBox.transform.SetParent(root.transform, false);
            Ui.Stretch(talkBox);
            for (int k = 0; k < Engine.MaxHosts; k++)
            {
                PortraitWidget host = PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, talkBox.transform, "Host " + k, DropPoint.AtHost(k));
                Ui.Place(host.Root, TriangleCenterX + (k - (Engine.MaxHosts - 1) / 2f) * CellWidth - 27f, TriangleTop, 54f, 58f);
                host.Label.text = "";
                host.Icon.Set(IconKind.None, "");
                HostPlaces.Add(host);
                CancelOverlays.Add(MakeCancelOverlay(host));
            }
            hint = Ui.Label(labelTemplate, root.transform, "Hint", first.xMin, HintTop, HintWidth, 64f, 14f);
            resetLineup = Ui.Button(popup.NextSong_Button, root.transform, "ResetLineup", 30f, 420f, 128f, 39f, OnReset);
            Ui.SetButtonText(resetLineup, Texts.Get("CONCERTHYPE__RESET_LINEUP"));
            LevelReset();

            for (int j = 0; j < GameReader.SenbatsuPositions; j++)
                Slots.Add(PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, root.transform, "Slot " + j, DropPoint.AtPlace(j)));

            // The tabs side by side, centred over the formation
            GameObject tabTemplate = TabWidget.Template(popup);
            Vector2 tabSize = TabWidget.Size(tabTemplate);
            songTab = TabWidget.Make(tabTemplate, root.transform, "SongTab", TriangleCenterX - tabSize.x, TabWidget.Top, tabSize, () => OnTab(FormationTab.Song));
            talkTab = TabWidget.Make(tabTemplate, root.transform, "TalkTab", TriangleCenterX, TabWidget.Top, tabSize, () => OnTab(FormationTab.Talk));

            startLabel = popup.NextSong_Button.GetComponentInChildren<TextMeshProUGUI>(true);
            gameStartText = null;

            if (!subscribed)
            {
                ShowSession.Changed += () =>
                {
                    if (Alive && root.activeInHierarchy && ShowSession.Current != null)
                        Render();
                    else
                        BenchColumn.Render(ShowSession.Current == null ? null : PanelView.Build(ShowSession.Current, tab: Tab));
                };
                subscribed = true;
            }
        }

        /// <summary>
        /// Level with the game's Start button beside it, whose left edge leaves room for a narrower button. Done on
        /// each redraw: the game anchors its button to the popup's bottom, which moves with the window's shape.
        /// </summary>
        private static void LevelReset()
        {
            Transform container = owner.NextSong_Container.transform;
            Rect start = Ui.Placed(owner.NextSong_Button, container);
            float height = ((RectTransform)container).rect.height;
            bool level = start.height > 1f && start.yMin > 300f && start.yMax < height;
            Ui.Place(resetLineup.gameObject, 30f, level ? start.yMin : 420f, 128f, level ? start.height : 39f);
        }

        public static void Render()
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !Alive)
                return;
            session.SyncHype();
            // The talk break's tab is disabled while none can be called
            if (Tab == FormationTab.Talk && DropRules.TalkBlocked(session.State) != null)
                Tab = FormationTab.Song;
            PanelView view = PanelView.Build(session, drag, message, Tab);
            LevelReset();
            // Before the readouts get their text: measuring a title's width leaves it on the label
            Layout(view);
            expected.Set(view.Expected);
            changed.Set(view.Changed);
            last.Set(view.Last);
            hint.text = view.Hint;
            SetStartLabel(view.StartLabel);
            bool song = Tab == FormationTab.Song;
            songTab.Set(Texts.Get("CONCERTHYPE__TAB_SONG"), song);
            talkTab.Set(view.TalkTitle, !song);
            talkTab.Enable(view.TalkOpen, view.TalkTip);

            talkBox.SetActive(!song);
            for (int k = 0; k < HostPlaces.Count && k < view.Hosts.Count; k++)
            {
                PortraitWidget widget = HostPlaces[k];
                HostView host = view.Hosts[k];
                data_girls.girls girl = host.Member < 0 ? null : session.Show.Girls[host.Member];
                widget.SetGirl(girl);
                Ui.SetEnergyBar(widget.Bar, girl is null ? 0 : session.State.Energy[host.Member]);
                widget.SetHighlight(host.Highlight);
                widget.SetDimmed(host.Dragged);
                if (CancelOverlays[k] != null)
                    CancelOverlays[k].SetActive(false);
                Ui.Place(widget.Root, frame.CenterX + (k - (Engine.MaxHosts - 1) / 2f) * CellWidth * frame.Scale - 27f * frame.Scale, TriangleTop, 54f, 58f);
                widget.Root.transform.localScale = new Vector3(frame.Scale, frame.Scale, 1f);
            }
            ShowCancel();

            Vector2[] places = FormationPlaces(view.Slots.Select(slot => slot.Row).ToList());
            for (int j = 0; j < Slots.Count; j++)
            {
                PortraitWidget widget = Slots[j];
                if (!song || j >= view.Slots.Count)
                {
                    widget.Root.SetActive(false);
                    continue;
                }
                SlotView slot = view.Slots[j];
                widget.Root.SetActive(true);
                Ui.Place(widget.Root, places[j].x, places[j].y, 54f, 58f);
                widget.Root.transform.localScale = new Vector3(frame.Scale, frame.Scale, 1f);
                widget.SetGirl(slot.Member >= 0 ? session.Show.Girls[slot.Member] : null);
                Ui.SetEnergyBar(widget.Bar, slot.Member >= 0 ? session.State.Energy[slot.Member] : 0);
                widget.SetDimmed(slot.Dragged);
                widget.SetHighlight(slot.Highlight);
                widget.Label.text = slot.Roll;
                widget.Icon.Set(slot.Icon, slot.Tip);
                // The roll, then the icon; the icon alone sits in the middle
                widget.Icon.PlaceAt(slot.Roll.Length > 0 ? 29f : 27f - IconWidget.Size / 2f, 59f);
            }
            BenchColumn.Render(view);
        }

        private static void OnTab(FormationTab tab) => Safely(() =>
        {
            if (drag != null || Tab == tab || ShowSession.Current == null)
                return;
            if (tab == FormationTab.Talk && DropRules.TalkBlocked(ShowSession.Current.State) != null)
                return;
            Tab = tab;
            message = null;
            Render();
        }, "switching tabs");

        /// <summary>
        /// The top-left corner of each place's portrait in the game's triangle, given each place's row: each row
        /// centred under the one before, in place order, at the formation's scale.
        /// </summary>
        public static Vector2[] FormationPlaces(IList<int> rows)
        {
            int[] perRow = new int[5], placed = new int[5];
            foreach (int row in rows)
                perRow[row]++;
            Vector2[] places = new Vector2[rows.Count];
            float scale = frame.Scale;
            for (int j = 0; j < rows.Count; j++)
            {
                int row = rows[j];
                places[j] = new Vector2(frame.CenterX + ((placed[row]++ - (perRow[row] - 1) / 2f) * CellWidth - 27f) * scale, TriangleTop + row * RowHeight * scale);
            }
            return places;
        }

        /// <summary>
        /// Lays the panel out for the window's shape, which moves the game's Start button: the Last readout stays a
        /// gap above the buttons, the hint goes under them, and the tabs and formation are centred in the space
        /// between the readouts and the bench, as large as fits. The song's title ends before the tabs.
        /// </summary>
        private static void Layout(PanelView view)
        {
            Transform container = owner.NextSong_Container.transform;
            float height = ((RectTransform)container).rect.height;
            Rect start = Ui.Placed(owner.NextSong_Button, container), reset = Ui.Placed(resetLineup.gameObject, container);
            float buttonsTop = Math.Min(start.yMin, reset.yMin);

            RectTransform lastRect = (RectTransform)last.Root.transform;
            lastRect.anchoredPosition = Vector2.zero;
            float lastBottom = Ui.Placed(last.Value.gameObject, container).yMax;
            float lift = Math.Max(0f, lastBottom + Gap - buttonsTop);
            lastRect.anchoredPosition = new Vector2(0f, lift);

            Rect hintAt = Ui.Placed(hint.gameObject, container);
            float hintLeft = hintAt.xMin;
            Ui.Place(hint.gameObject, hintLeft, Math.Max(start.yMax, reset.yMax) + HintGap, Math.Max(start.xMax - hintLeft, 128f), hintAt.height);

            // The second column's widest title, as the readouts draw it
            float titles = new[] { "CONCERTHYPE__CHANGED_TITLE", "CONCERTHYPE__LAST_SONG_TITLE", "CONCERTHYPE__LAST_TALK_TITLE" }
                .Max(key => changed.Title.GetPreferredValues(Texts.Get(key)).x);
            List<int> widths = new List<int>();
            foreach (SlotView slot in view.Slots)
            {
                while (widths.Count <= slot.Row)
                    widths.Add(0);
                widths[slot.Row]++;
            }
            // The talk break's row sits where the front row is
            if (widths.Count == 0)
                widths.Add(0);
            widths[0] = Math.Max(widths[0], Engine.MaxHosts);
            frame = PanelView.FitFormation(widths, new PanelView.FormationSpace
            {
                ReadoutsRight = Ui.Placed(changed.Title.gameObject, container).xMin + titles + Gap,
                ReadoutsBottom = lastBottom - lift + Gap,
                ButtonsRight = Math.Max(start.xMax, reset.xMax) + Gap,
                ButtonsTop = buttonsTop - Gap,
                BenchLeft = Ui.Placed(owner.Girls, container).xMin - Gap,
                Top = TriangleTop,
                Bottom = height - Gap,
                Margin = hintLeft,
                TabsHalf = TabWidget.Width,
            });

            Vector2 tab = ((RectTransform)songTab.Button.transform).rect.size;
            Ui.Place(songTab.Button.gameObject, frame.CenterX - TabWidget.Width, TabWidget.Top, TabWidget.Width, tab.y);
            Ui.Place(talkTab.Button.gameObject, frame.CenterX, TabWidget.Top, TabWidget.Width, tab.y);
            SongTitle.Fit(owner.NextSong_Title, container, frame.CenterX - TabWidget.Width - TitleGap);
        }

        /// <summary>
        /// The Start button reads "Start Talk Break" while a host is placed, and the game's own text otherwise.
        /// </summary>
        private static void SetStartLabel(string text)
        {
            if (startLabel == null)
                return;
            if (text == null)
            {
                if (gameStartText != null)
                    startLabel.text = gameStartText;
                gameStartText = null;
                return;
            }
            if (gameStartText == null)
                gameStartText = startLabel.text;
            startLabel.text = text;
        }

        private static void Safely(Action action, string what)
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

        private static void OnReset() => Safely(() =>
        {
            message = null;
            ShowSession.Current?.ResetLineup();
            Render();
        }, "resetting the lineup");

        /// <summary>
        /// Who a drag from this spot picks up: the bench portrait's idol, or whoever is on the place or host place.
        /// </summary>
        public static int MemberAt(DropPoint from, int benchMember)
        {
            ShowSession session = ShowSession.Current;
            if (session == null)
                return -1;
            switch (from.Spot)
            {
                case Spot.Place:
                    return from.Place >= 0 && from.Place < session.Lineup.Length ? session.Lineup[from.Place] : -1;
                case Spot.Host:
                    return from.Place >= 0 && from.Place < session.Hosts.Length ? session.Hosts[from.Place] : -1;
                default:
                    return benchMember;
            }
        }

        private static DragAndDropManager Manager() => Camera.main?.GetComponent<mainScript>()?.Data?.GetComponent<DragAndDropManager>();

        /// <summary>
        /// The game's floating portrait follows the mouse, with its drag cursor and sound. False when she can't be
        /// dragged now (nobody there, can't perform, or a song is playing).
        /// </summary>
        public static bool BeginDrag(int member, DropPoint from)
        {
            bool started = false;
            Safely(() =>
            {
                ShowSession session = ShowSession.Current;
                if (session == null || !Alive || !root.activeInHierarchy || session.Playing || session.State.Finished || member < 0 || drag != null)
                    return;
                if (from.Spot == Spot.Bench && !session.State.IsAvailable(member))
                    return;
                drag = new DragView { Member = member, From = from, Over = from.Spot == Spot.Bench ? (DropPoint?)null : from };
                message = null;
                started = true;
                try
                {
                    Manager()?.OnMouseDown(session.Show.Girls[member], staticVars.DragAndDrop.senbatsu);
                }
                catch (Exception ex)
                {
                    Log.WarnOnce("drag", "couldn't show the dragged portrait; dragging works without it: " + ex.Message);
                }
                Render();
            }, "picking up an idol");
            return started;
        }

        /// <summary>
        /// While dragging: the place or host place the mouse is over, found as the game finds a senbatsu drop,
        /// by the button it hovers.
        /// </summary>
        public static void Tick()
        {
            if (drag == null)
            {
                ShowCancel();
                return;
            }
            if (!Input.GetMouseButton(0))
            {
                EndDrag();
                return;
            }
            DropPoint? over = Hovered();
            if (!Nullable.Equals(over, drag.Over))
            {
                drag.Over = over;
                Safely(Render, "showing the drop");
            }
        }

        private static DropPoint? Hovered()
        {
            if (!Alive)
                return null;
            // Only the shown tab's places: a hidden one may still think it's hovered
            for (int j = 0; j < Slots.Count; j++)
            {
                if (Hovering(Slots[j]))
                    return DropPoint.AtPlace(j);
            }
            for (int k = 0; k < HostPlaces.Count; k++)
            {
                if (Hovering(HostPlaces[k]))
                    return DropPoint.AtHost(k);
            }
            return null;
        }

        private static bool Hovering(PortraitWidget widget) => widget.Root.activeInHierarchy && widget.Hover != null && widget.Hover.hovering;

        /// <summary>
        /// The mouse let go: the drop by the rules, and the game's "plop" when it changed something; a refused drop
        /// changes nothing and the hint says why.
        /// </summary>
        public static void EndDrag(bool cancel = false)
        {
            DragView done = drag;
            if (done == null)
                return;
            drag = null;
            ShowSession session = ShowSession.Current;
            DragAndDropManager manager = Manager();
            data_girls.girls girl = session != null && done.Member < session.Show.Girls.Count ? session.Show.Girls[done.Member] : null;
            Safely(() =>
            {
                if (cancel || session == null || !Alive)
                    return;
                DropPoint target = Hovered() ?? done.Over ?? DropPoint.Bench;
                DropResult result = session.Drop(done.Member, done.From, target);
                message = result.Allowed ? null : result.Message;
                if (result.Allowed && result.Changed)
                    manager?.OnInsert();
                Render();
            }, "dropping an idol");
            // The game's own drop ends the floating portrait and cursor. It runs a frame later, once the manager's
            // per-frame check has seen that no senbatsu screen is open, so it never drops her on one.
            if (manager != null && girl != null)
                manager.StartCoroutine(GameDropNextFrame(manager, girl));
        }

        private static IEnumerator GameDropNextFrame(DragAndDropManager manager, data_girls.girls girl)
        {
            yield return null;
            try
            {
                manager.OnMouseUp(girl, staticVars.DragAndDrop.senbatsu, null);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("drop", "couldn't end the dragged portrait: " + ex.Message);
            }
        }

        /// <summary>
        /// The game's cancel icon (the agency idol list's job cancel button) centred on the host's picture, off until
        /// hovered. It takes no raycast, so the hover and the click stay on the portrait.
        /// </summary>
        private static GameObject MakeCancelOverlay(PortraitWidget host)
        {
            Sprite sprite = Icons.GameCancel();
            Transform center = host.Picture?.transform;
            if (sprite == null || center == null)
                return null;
            GameObject overlay = new GameObject("Cancel", typeof(RectTransform));
            overlay.transform.SetParent(center, false);
            Image image = overlay.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform rect = (RectTransform)overlay.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(34f, 34f);
            overlay.SetActive(false);
            return overlay;
        }

        /// <summary>
        /// While the mouse is over a talk break host, the game's cancel icon over her says a click takes her off.
        /// </summary>
        private static void ShowCancel()
        {
            if (!Alive)
                return;
            ShowSession session = ShowSession.Current;
            for (int k = 0; k < HostPlaces.Count; k++)
            {
                GameObject overlay = CancelOverlays[k];
                if (overlay == null)
                    continue;
                bool show = session != null && k < session.Hosts.Length && session.Hosts[k] >= 0 && !session.Playing && drag == null && Hovering(HostPlaces[k]);
                if (overlay.activeSelf == show)
                    continue;
                overlay.SetActive(show);
                HostPlaces[k].SetDimmed(show);
            }
        }

        /// <summary>
        /// A quick click: a bench idol takes the first empty place she can on the tab shown, otherwise the hint says
        /// to drag; a talk break host is taken off.
        /// </summary>
        public static void OnClick(int member, DropPoint from) => Safely(() =>
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !Alive || drag != null || session.Playing)
                return;
            if (from.Spot == Spot.Host)
            {
                if (member >= 0 && session.Drop(member, from, DropPoint.Bench).Changed)
                {
                    message = null;
                    Render();
                }
                return;
            }
            if (from.Spot != Spot.Bench || member < 0)
                return;
            DropResult result = Tab == FormationTab.Song ? session.Click(member) : session.ClickHost(member);
            message = result.Allowed ? null : result.Message;
            if (result.Allowed && result.Changed)
                Manager()?.OnInsert();
            Render();
        }, "clicking an idol");
    }

    /// <summary>
    /// A small copy of a single's marketing wheel: its arcs are an idol's chances, and the pointer takes the colour
    /// of the arc under it, as the game's.
    /// </summary>
    internal sealed class WheelWidget
    {
        /// <summary>
        /// The pointer is drawn this much larger than the wheel's scale, so it reads at this size.
        /// </summary>
        private const float PointerBoost = 1.8f;

        public GameObject Root;
        private Transform spin;
        private Image success, criticalSuccess, criticalFailure;
        private TextMeshProUGUI pointer;
        private float scale;
        private double[] chances = new double[Outcomes.Count];

        /// <summary>
        /// The game's marketing wheel, from the single release popup; null (logged once) when it can't be found.
        /// </summary>
        public static Single_Marketing_Roll Template()
        {
            Single_Marketing_Roll roll = PopupManager.GetObject(PopupManager._type.single_release)?.GetComponent<Single_Release>()
                ?.Marketing_Roll_Container?.GetComponentInChildren<Single_Marketing_Roll>(true);
            if (roll == null || roll.Spinner_Container == null || roll.Spinner_Icon == null || roll.Spinner_Bar_LightGreen == null
                || roll.Spinner_Bar_DarkGreen == null || roll.Spinner_Bar_DarkRed == null || !(roll.Spinner_Container.transform.parent is RectTransform))
            {
                Log.WarnOnce("wheel", "couldn't find the game's marketing wheel; the rolls show as numbers only");
                return null;
            }
            return roll;
        }

        /// <summary>
        /// A wheel this wide centred on (x, y) of the parent, in <see cref="Ui.Place"/> coordinates.
        /// </summary>
        public static WheelWidget Make(Single_Marketing_Roll game, Transform parent, float x, float y, float size)
        {
            GameObject frame = game.Spinner_Container.transform.parent.gameObject;
            RectTransform frameRect = (RectTransform)frame.transform;
            Vector2 gameSize = frameRect.rect.size;
            GameObject root = Ui.Copy(frame, parent, Shapes.Wheel, "marketing wheel", "Wheel");
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = gameSize;
            rect.localRotation = Quaternion.identity;
            WheelWidget wheel = new WheelWidget { Root = root, scale = size / gameSize.x };
            wheel.SetPop(1f);

            wheel.spin = root.transform.Find(game.Spinner_Container.name);
            wheel.success = wheel.spin?.Find(game.Spinner_Bar_LightGreen.name)?.GetComponent<Image>();
            wheel.criticalSuccess = wheel.spin?.Find(game.Spinner_Bar_DarkGreen.name)?.GetComponent<Image>();
            wheel.criticalFailure = wheel.spin?.Find(game.Spinner_Bar_DarkRed.name)?.GetComponent<Image>();

            // Where the game's pointer sits from the wheel's centre
            RectTransform gamePointer = (RectTransform)game.Spinner_Icon.transform;
            Vector3[] corners = new Vector3[4];
            gamePointer.GetWorldCorners(corners);
            Vector2 offset = (Vector2)frameRect.InverseTransformPoint((corners[0] + corners[2]) / 2f) - frameRect.rect.center;
            GameObject copy = Ui.Copy(game.Spinner_Icon, root.transform, Shapes.Pointer, "marketing wheel pointer", "Pointer");
            RectTransform pointerRect = (RectTransform)copy.transform;
            pointerRect.anchorMin = pointerRect.anchorMax = pointerRect.pivot = new Vector2(0.5f, 0.5f);
            pointerRect.anchoredPosition = offset;
            pointerRect.sizeDelta = gamePointer.rect.size;
            pointerRect.localScale = new Vector3(PointerBoost, PointerBoost, 1f);
            wheel.pointer = copy.GetComponent<TextMeshProUGUI>();

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            return wheel;
        }

        /// <summary>
        /// How much of its size the wheel shows: 0 to 1 as it pops in, 1 to 0 as it gives way to her number.
        /// </summary>
        public void SetPop(float share) => Root.transform.localScale = new Vector3(scale * share, scale * share, 1f);

        /// <summary>
        /// The arcs as the game fills them: success, then critical success, then critical failure, from the top
        /// clockwise; failure is the ring under them.
        /// </summary>
        public void SetChances(double[] chances)
        {
            this.chances = chances;
            double s = chances[(int)Outcome.Success], cs = chances[(int)Outcome.CriticalSuccess], cf = chances[(int)Outcome.CriticalFailure];
            if (success != null)
                success.fillAmount = (float)(s / 100);
            if (criticalSuccess != null)
                criticalSuccess.fillAmount = (float)((s + cs) / 100);
            if (criticalFailure != null)
                criticalFailure.fillAmount = (float)((s + cs + cf) / 100);
        }

        public void SetAngle(double angle)
        {
            float turned = (float)(angle % 360);
            if (spin != null)
                spin.localRotation = Quaternion.Euler(0f, 0f, turned);
            if (pointer == null)
                return;
            double at = turned / 3.6, s = chances[(int)Outcome.Success], cs = chances[(int)Outcome.CriticalSuccess], cf = chances[(int)Outcome.CriticalFailure];
            pointer.color = StageRolls.ColorOf(at <= s ? Outcome.Success : at <= s + cs ? Outcome.CriticalSuccess : at <= s + cs + cf ? Outcome.CriticalFailure : Outcome.Failure);
        }
    }

    internal sealed class StageDriver : MonoBehaviour
    {
        private void Update() => StageRolls.Tick();
    }

    /// <summary>
    /// The formation on stage while a song plays, spread over the screen: each idol's portrait with a small
    /// marketing wheel above it. The portraits slide from the lineup panel's places to the stage's as the song
    /// starts, and back as it ends, while the bench fades out and in. The wheels spin as the song starts and land
    /// one by one, back row first and the center last; each then gives way to her roll in its outcome's colour,
    /// with the outcome and her chances on hover.
    /// </summary>
    internal static class StageRolls
    {
        private const string Name = "ConcertHype Stage";
        private const float EnterSeconds = 0.5f, LeaveSeconds = 0.4f, PopSeconds = 0.2f;

        /// <summary>
        /// The landed numbers' font: the heaviest the game ships. Without it they keep the label's own font,
        /// thickened.
        /// </summary>
        internal const string NumberFontName = "OpenSans-ExtraBold SDF";

        /// <summary>
        /// A hurt idol's picture stays this grey after its red flash, as the bench dims an idol who can't perform.
        /// </summary>
        private static readonly Color HurtGrey = new Color(0.7f, 0.7f, 0.7f, 1f);

        /// <summary>
        /// The wheel's and the number's centre, from the portrait's top-left corner.
        /// </summary>
        private static readonly Vector2 WheelCentre = new Vector2(27f, (float)(StageView.WheelSize / 2 - StageView.PortraitTop));

        private enum Phase
        {
            Entering,
            Playing,
            Leaving,
        }

        private static Concert_Popup owner;
        private static GameObject root;
        private static CanvasGroup group;
        private static readonly List<PortraitWidget> Places = new List<PortraitWidget>();
        private static readonly List<WheelWidget> Wheels = new List<WheelWidget>();
        private static readonly List<IconTip> NumberTips = new List<IconTip>();

        private static StageView view;
        private static Phase phase;
        private static float phaseTime;
        private static bool started;
        private static float elapsed, waited;
        private static float[] landedAt;

        /// <summary>
        /// Each place's portrait corner on the lineup panel and on stage, and its scale on stage.
        /// </summary>
        private static Vector2[] from, to;
        private static float stageScale = 1f;

        private static bool Alive => root != null && owner != null;

        public static Color ColorOf(Outcome outcome) => outcome switch
        {
            Outcome.CriticalFailure => mainScript.red32,
            Outcome.Failure => mainScript.red_light32,
            Outcome.Success => mainScript.green_light32,
            _ => mainScript.green32,
        };

        /// <summary>
        /// The song now playing; a talk break shows no stage. A failure here only loses the stage, not the show.
        /// </summary>
        public static void Show(Concert_Popup popup, ShowSession session)
        {
            try
            {
                view = StageView.Build(session);
                if (!view.Shown)
                {
                    Hide();
                    return;
                }
                Ensure(popup);
                List<int> rows = view.Slots.Select(slot => slot.Row).ToList();
                from = LineupPanel.FormationPlaces(rows);
                StagePlace[] stage = StageView.Layout(rows);
                stageScale = (float)stage[0].Scale;
                to = stage.Select(place => new Vector2(
                    (float)(place.CenterX - StageView.ItemWidth / 2 * place.Scale),
                    (float)(place.CenterY + (StageView.PortraitTop - StageView.ItemHeight / 2) * place.Scale))).ToArray();

                for (int j = 0; j < Places.Count; j++)
                {
                    PortraitWidget widget = Places[j];
                    WheelWidget wheel = j < Wheels.Count ? Wheels[j] : null;
                    bool used = j < view.Slots.Count;
                    widget.Root.SetActive(used);
                    if (!used)
                        continue;
                    StageSlotView slot = view.Slots[j];
                    widget.SetGirl(slot.HasRoll ? session.Show.Girls[slot.Member] : null);
                    Ui.SetEnergyBar(widget.Bar, slot.HasRoll ? slot.EnergyBefore : 0);
                    widget.Icon.Set(IconKind.None, "");
                    widget.Label.gameObject.SetActive(false);
                    if (wheel != null)
                    {
                        wheel.Root.SetActive(slot.HasRoll);
                        wheel.SetPop(0f);
                        if (slot.HasRoll)
                        {
                            wheel.SetChances(slot.Chances);
                            wheel.SetAngle(0);
                        }
                    }
                }
                phase = Phase.Entering;
                phaseTime = 0f;
                started = false;
                elapsed = waited = 0f;
                landedAt = Enumerable.Repeat(-1f, view.Slots.Count).ToArray();
                Move(0f);
                group.alpha = 1f;
                // Over the song's title strip, under the bench column and the hype bar
                root.transform.SetSiblingIndex(popup.Song.transform.GetSiblingIndex() + 1);
                root.SetActive(true);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("stage", "couldn't show the rolls on stage: " + ex);
                Hide();
            }
        }

        public static void Hide()
        {
            view = null;
            if (!(root is null))
                Deactivate();
        }

        // Apart from Hide, so a show with no stage built (and the unit tests) never reaches the engine
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Deactivate()
        {
            if (root != null)
                root.SetActive(false);
            BenchColumn.Fade(1f);
        }

        private static void Ensure(Concert_Popup popup)
        {
            if (Alive && ReferenceEquals(owner, popup))
                return;
            owner = popup;
            Places.Clear();
            Wheels.Clear();
            NumberTips.Clear();
            root = new GameObject(Name, typeof(RectTransform));
            root.transform.SetParent(popup.transform, false);
            Ui.Stretch(root);
            group = root.AddComponent<CanvasGroup>();
            root.AddComponent<StageDriver>();

            GameObject labelTemplate = popup.NextSong_Container.transform.Find("StaminaCostTitle")?.gameObject ?? popup.NextSong_Stamina;
            Single_Marketing_Roll template = WheelWidget.Template();
            TMP_FontAsset font = NumberFont();
            for (int j = 0; j < GameReader.SenbatsuPositions; j++)
            {
                PortraitWidget widget = PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, root.transform, "Place " + j, DropPoint.AtPlace(j), iconOnPicture: true);
                // Nobody is dragged off the stage while she sings
                if (widget.Drag != null)
                    UnityEngine.Object.Destroy(widget.Drag);
                TextMeshProUGUI number = widget.Label;
                // In the wheel's place, slamming down from its middle
                RectTransform numberRect = (RectTransform)number.transform;
                Ui.Place(number.gameObject, 0f, 0f, 90f, 44f);
                numberRect.pivot = new Vector2(0.5f, 0.5f);
                numberRect.anchoredPosition = new Vector2(WheelCentre.x, -WheelCentre.y);
                number.alignment = TextAlignmentOptions.Center;
                number.enableWordWrapping = false;
                number.fontSize = (float)StageView.NumberSize;
                StyleNumber(number, font);
                number.raycastTarget = true;
                NumberTips.Add(number.gameObject.AddComponent<IconTip>());
                // The hurt icon, larger, in the picture's bottom-right corner, slamming from its middle
                float iconSize = (float)StageView.HurtIconSize, iconAt = 47f - iconSize;
                Ui.Place(widget.Icon.Root, iconAt, iconAt, iconSize, iconSize);
                RectTransform iconRect = (RectTransform)widget.Icon.Root.transform;
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(iconAt + iconSize / 2f, -iconAt - iconSize / 2f);
                Places.Add(widget);
                if (template != null)
                    Wheels.Add(WheelWidget.Make(template, widget.Root.transform, WheelCentre.x, WheelCentre.y, (float)StageView.WheelSize));
            }
        }

        private static TMP_FontAsset NumberFont()
        {
            TMP_FontAsset font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(asset => asset != null && asset.name == NumberFontName);
            if (font == null)
                Log.WarnOnce("number font", $"couldn't find the game's {NumberFontName}; the rolls show in the label font");
            return font;
        }

        /// <summary>
        /// A heavy number with a thick dark outline and a soft shadow, on its own copy of the font's material so
        /// the game's other text keeps its look. Without the heavy font, the label's own is thickened.
        /// </summary>
        internal static void StyleNumber(TextMeshProUGUI number, TMP_FontAsset font)
        {
            if (font != null)
            {
                number.font = font;
                number.fontStyle = FontStyles.Normal;
            }
            else
            {
                number.fontStyle = FontStyles.Bold;
            }
            Material material = number.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.8f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.8f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, font != null ? 0f : 0.1f);
            number.outlineWidth = 0.3f;
            number.outlineColor = new Color32(0, 0, 0, 205);
        }

        private static float Ease(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        /// <summary>
        /// The portraits this far (0 to 1) from the lineup panel's places to the stage's.
        /// </summary>
        private static void Move(float share)
        {
            float scale = Mathf.Lerp(LineupPanel.FormationScale, stageScale, share);
            for (int j = 0; j < view.Slots.Count && j < Places.Count; j++)
            {
                Vector2 at = Vector2.Lerp(from[j], to[j], share);
                Ui.Place(Places[j].Root, at.x, at.y, 54f, 58f);
                Places[j].Root.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public static void Tick()
        {
            if (!Alive || view == null)
                return;
            try
            {
                ShowSession session = ShowSession.Current;
                if (session == null || !ReferenceEquals(owner.Concert, session.Concert))
                {
                    Hide();
                    return;
                }
                if (phase != Phase.Leaving && session.Pending == null)
                {
                    phase = Phase.Leaving;
                    phaseTime = 0f;
                }
                phaseTime += Time.deltaTime;

                if (phase == Phase.Leaving)
                {
                    float back = Mathf.Clamp01(phaseTime / LeaveSeconds);
                    Move(1f - Ease(back));
                    group.alpha = 1f - back;
                    BenchColumn.Fade(back);
                    if (back >= 1f)
                        Hide();
                    return;
                }

                if (phase == Phase.Entering)
                {
                    float there = Mathf.Clamp01(phaseTime / EnterSeconds);
                    Move(Ease(there));
                    BenchColumn.Fade(1f - there);
                    if (there >= 1f)
                    {
                        phase = Phase.Playing;
                        phaseTime = 0f;
                    }
                }
                float pop = phase == Phase.Playing ? Mathf.Clamp01(phaseTime / PopSeconds) : 0f;

                // The wheels turn with the song's progress bar
                if (!started)
                {
                    float bar = owner.SongProgressBar != null ? owner.SongProgressBar.transform.localScale.x : 0f;
                    waited += Time.unscaledDeltaTime;
                    if (bar > 0f && bar < 0.5f || waited >= 2f)
                        started = true;
                }
                if (started)
                    elapsed += Time.deltaTime;
                float song = Pace.SongSeconds();
                for (int j = 0; j < view.Slots.Count; j++)
                {
                    StageSlotView slot = view.Slots[j];
                    if (!slot.HasRoll)
                        continue;
                    WheelWidget wheel = j < Wheels.Count ? Wheels[j] : null;
                    if (landedAt[j] < 0f)
                    {
                        wheel?.SetPop(pop);
                        double t = Math.Min(1.0, elapsed / Math.Max(0.01, slot.LandAt * song));
                        wheel?.SetAngle(slot.Angle * (1 - Math.Pow(1 - t, 4)));
                        if (started && t >= 1.0)
                            Land(j, slot);
                        continue;
                    }
                    float since = elapsed - landedAt[j];
                    wheel?.SetPop(Mathf.Min(pop, 1f - Mathf.Clamp01(since / PopSeconds)));
                    PortraitWidget widget = Places[j];
                    NumberPose pose = StageView.NumberMotion(since, slot.Outcome);
                    TextMeshProUGUI number = widget.Label;
                    number.transform.localScale = new Vector3((float)pose.Scale, (float)pose.Scale, 1f);
                    ((RectTransform)number.transform).anchoredPosition = new Vector2(WheelCentre.x + (float)pose.OffsetX, -WheelCentre.y);
                    Color color = Color.Lerp(ColorOf(slot.Outcome), Color.white, (float)pose.Flash);
                    color.a = (float)pose.Alpha;
                    number.color = color;
                    if (slot.Hurt)
                    {
                        float iconScale = (float)StageView.NumberMotion(since, Outcome.Success).Scale;
                        widget.Icon.Root.transform.localScale = new Vector3(iconScale, iconScale, 1f);
                        if (widget.Picture != null)
                            widget.Picture.color = Color.Lerp(HurtGrey, mainScript.red32, (float)StageView.HurtFlash(since));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WarnOnce("stage", "couldn't show the rolls on stage: " + ex);
                Hide();
            }
        }

        private static void Land(int j, StageSlotView slot)
        {
            landedAt[j] = elapsed;
            PortraitWidget widget = Places[j];
            widget.Label.gameObject.SetActive(true);
            widget.Label.text = slot.Roll;
            widget.Label.fontSize = (float)(slot.Critical ? StageView.CriticalNumberSize : StageView.NumberSize);
            widget.Label.color = new Color(1f, 1f, 1f, 0f);
            NumberTips[j].Text = slot.Tip;
            Ui.SetEnergyBar(widget.Bar, slot.EnergyAfter);
            if (slot.Hurt)
            {
                widget.Icon.Set(IconKind.Hurt, slot.HurtTip);
                widget.Icon.Root.transform.localScale = Vector3.zero;
                mainScript.playAudioClip("cheers_fail");
            }
            if (slot.Outcome == Outcome.CriticalSuccess)
                mainScript.playAudioClip("spinner_win_crit");
            else if (slot.Outcome == Outcome.CriticalFailure)
                mainScript.playAudioClip("spinner_fail_crit");
        }
    }

    /// <summary>
    /// The popup's column of idols becomes the show's bench: everyone in the agency who isn't in the shown tab's
    /// set-up (on stage, or hosting the talk break), with the show's energy, to drag onto its places. The column
    /// picks its columns and scale from how many it holds.
    /// </summary>
    internal static class BenchColumn
    {
        private const int MaxColumns = 4;
        private const float PortraitWidth = 54f, PortraitHeight = 58f, CellWidth = 62f, CellHeight = 66f;

        private static Concert_Popup owner;
        private static readonly List<PortraitWidget> Widgets = new List<PortraitWidget>();
        private static GridLayoutGroup grid;
        private static bool gridWasEnabled;
        private static bool saved;

        public static void Build(Concert_Popup popup, ShowSession session)
        {
            owner = popup;
            Widgets.Clear();
            Transform column = popup.Girls.transform;
            for (int i = column.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(column.GetChild(i).gameObject);

            // The show lays the column out itself; the game's grid comes back for its own show
            grid = column.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                if (!saved)
                {
                    gridWasEnabled = grid.enabled;
                    saved = true;
                }
                grid.enabled = false;
            }

            GameObject labelTemplate = popup.NextSong_Container.transform.Find("StaminaCostTitle")?.gameObject ?? popup.NextSong_Stamina;
            for (int m = 0; m < session.Show.Girls.Count; m++)
            {
                // Why she can't perform is an icon on her picture
                PortraitWidget widget = PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, column, "Bench " + m, DropPoint.Bench, iconOnPicture: true);
                widget.SetGirl(session.Show.Girls[m]);
                if (widget.Drag != null)
                    widget.Drag.BenchMember = m;
                widget.Label.gameObject.SetActive(false);
                Widgets.Add(widget);
            }
            Render(PanelView.Build(session, tab: LineupPanel.Tab));
        }

        /// <summary>
        /// The bench portraits fade while a song plays: they're the show's own, so the game's column is untouched.
        /// </summary>
        public static void Fade(float alpha)
        {
            foreach (PortraitWidget widget in Widgets)
            {
                if (widget.Group != null)
                    widget.Group.alpha = alpha;
            }
        }

        /// <summary>
        /// Gives the column back to the game for its own show.
        /// </summary>
        public static void Restore()
        {
            if (grid != null && saved)
                grid.enabled = gridWasEnabled;
            saved = false;
            Widgets.Clear();
            owner = null;
        }

        public static void Render(PanelView view)
        {
            ShowSession session = ShowSession.Current;
            if (view == null || session == null || owner == null || Widgets.Count != session.Show.Girls.Count)
                return;
            Rect area = ((RectTransform)owner.Girls.transform).rect;
            float scale = PanelView.BenchGrid(view.Bench.Count, area.width, area.height, CellWidth, CellHeight, MaxColumns, out int columns);
            float blockWidth = columns * CellWidth * scale;
            Dictionary<int, int> order = new Dictionary<int, int>();
            for (int i = 0; i < view.Bench.Count; i++)
                order[view.Bench[i].Member] = i;

            for (int m = 0; m < Widgets.Count; m++)
            {
                PortraitWidget widget = Widgets[m];
                if (widget.Root == null)
                    continue;
                if (!order.TryGetValue(m, out int i))
                {
                    widget.Root.SetActive(false);
                    continue;
                }
                BenchView bench = view.Bench[i];
                widget.Root.SetActive(true);
                // Right-aligned like the game's column, read left to right
                float x = area.width - blockWidth + ((i % columns) * CellWidth + (CellWidth - PortraitWidth) / 2f) * scale;
                float y = ((i / columns) * CellHeight + (CellHeight - PortraitHeight) / 2f) * scale;
                Ui.Place(widget.Root, x, y, PortraitWidth, PortraitHeight);
                widget.Root.transform.localScale = new Vector3(scale, scale, 1f);
                Ui.SetEnergyBar(widget.Bar, bench.Energy);
                widget.Icon.Set(bench.Icon, bench.Tip, bench.Count);
                // An idol who can't perform is dimmed and can't be dragged; the icon saying why stays bright
                widget.SetDimmed(!bench.Available);
            }
        }
    }

    /// <summary>
    /// The show's score under the profit on the results screen, as the game shows production cost, revenue and
    /// profit, with what it's made of on its info icon.
    /// </summary>
    internal static class ScoreLine
    {
        private const string Name = "ConcertHype Score";

        private static ReadoutWidget score;

        /// <param name="readout">Null for a show the mod didn't run.</param>
        public static void Show(Concert_Popup popup, Readout readout)
        {
            Transform final = popup.Final_Container.transform;
            bool made = score != null && score.Root != null && score.Root.transform.parent == final;
            if (!made && readout == null)
                return;
            if (!made)
            {
                GameObject title = final.Find("ProfitTitle")?.gameObject, value = final.Find("Profit")?.gameObject;
                GameObject revenueTitle = final.Find("RevenueTitle")?.gameObject;
                if (title == null || value == null)
                    return;
                Rect titleAt = Ui.Placed(title, final), valueAt = Ui.Placed(value, final);
                // One step below Profit, as Profit is below Revenue
                float step = revenueTitle != null ? titleAt.yMin - Ui.Placed(revenueTitle, final).yMin : 132f;
                Color plain = popup.NextSong_AccidentChance?.GetComponent<TextMeshProUGUI>()?.color ?? Color.white;
                score = ReadoutWidget.Make(final, final, Name, title, value, InfoIcon.From(popup), valueAt.xMin, titleAt.yMin + step, plain);
            }
            score.Set(readout ?? new Readout());
        }
    }
}
