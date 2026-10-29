using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// The lineup panel on the next-song screen, in place of the game's center, stamina and accident readouts: the
    /// song's formation, the talk break's host place, and readouts like the game's saying how the song is expected to go.
    /// </summary>
    internal static class LineupPanel
    {
        private const string Name = "ConcertHype Lineup";
        private const float TriangleCenterX = 610f, TriangleTop = 135f, RowHeight = 76f, CellWidth = 70f;

        /// <summary>
        /// The readouts sit as the game's do (Center, Stamina Cost), in two columns left of the formation: the
        /// second row a readout's height below the first, the second column where the first's titles end.
        /// </summary>
        private const float SecondRow = 131f, SecondColumnX = 290f, HintTop = 470f, HintWidth = 470f;

        private static Concert_Popup owner;
        private static GameObject root;
        private static ReadoutWidget expected, changed, last;
        private static TextMeshProUGUI hint, talkTitle, formationNote;
        private static Button resetLineup;
        private static PortraitWidget hostPlace;
        private static GameObject cancelOverlay, talkBox;
        private static CanvasGroup talkBoxGroup;
        private static readonly List<PortraitWidget> Slots = new List<PortraitWidget>();
        private static readonly List<GameObject> Hidden = new List<GameObject>();
        private static TextMeshProUGUI startLabel;
        private static string gameStartText;
        private static bool subscribed;

        private static DragView drag;
        private static string message;

        private static bool Alive => root != null && owner != null;

        public static void Show(Concert_Popup popup, ShowSession session)
        {
            Ensure(popup);
            drag = null;
            message = null;
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
            changed = ReadoutWidget.Make(root.transform, container, "Changed", labelTemplate, popup.NextSong_Stamina, null, SecondColumnX, first.yMin, plain);
            last = ReadoutWidget.Make(root.transform, container, "Last", labelTemplate, popup.NextSong_Stamina, null, SecondColumnX, first.yMin + SecondRow, plain);
            // The talk break as the game shows the center: its title over its place
            talkBox = new GameObject("TalkBreak", typeof(RectTransform));
            talkBox.transform.SetParent(root.transform, false);
            Ui.Stretch(talkBox);
            talkBoxGroup = talkBox.AddComponent<CanvasGroup>();
            GameObject talkCopy = Ui.Copy(centerTitle, talkBox.transform, Shapes.Label, "label", "TalkTitle");
            Ui.MoveTo(talkCopy, container, first.xMin, first.yMin + SecondRow);
            talkTitle = talkCopy.GetComponent<TextMeshProUGUI>();
            talkTitle.raycastTarget = false;
            Rect center = popup.NextSong_Center.Length > 0 && popup.NextSong_Center[0] != null ? Ui.Placed(popup.NextSong_Center[0], container) : new Rect(first.xMin, first.yMin + 60f, 54f, 58f);
            hostPlace = PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, talkBox.transform, "Host", DropPoint.Host);
            Ui.Place(hostPlace.Root, center.xMin, center.yMin + SecondRow, 54f, 58f);
            cancelOverlay = MakeCancelOverlay(hostPlace);
            hint = Ui.Label(labelTemplate, root.transform, "Hint", first.xMin, HintTop, HintWidth, 64f, 14f);
            // Level with the game's Start button beside it, whose left edge leaves room for a narrower button
            Rect start = Ui.Placed(popup.NextSong_Button, container);
            bool level = start.height > 1f && start.yMin > 300f && start.yMax < 640f;
            resetLineup = Ui.Button(popup.NextSong_Button, root.transform, "ResetLineup", 30f, level ? start.yMin : 420f, 128f, level ? start.height : 39f, OnReset);
            Ui.SetButtonText(resetLineup, Texts.Get("CONCERTHYPE__RESET_LINEUP"));

            for (int j = 0; j < GameReader.SenbatsuPositions; j++)
                Slots.Add(PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, root.transform, "Slot " + j, DropPoint.AtPlace(j)));
            formationNote = Ui.Label(labelTemplate, root.transform, "AfterTalk", TriangleCenterX - 170f, TriangleTop, 340f, 22f, 15f);
            formationNote.alignment = TextAlignmentOptions.Top;

            startLabel = popup.NextSong_Button.GetComponentInChildren<TextMeshProUGUI>(true);
            gameStartText = null;

            if (!subscribed)
            {
                ShowSession.Changed += () =>
                {
                    if (Alive && root.activeInHierarchy && ShowSession.Current != null)
                        Render();
                    else
                        BenchColumn.Render(ShowSession.Current == null ? null : PanelView.Build(ShowSession.Current));
                };
                subscribed = true;
            }
        }

        public static void Render()
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !Alive)
                return;
            session.SyncHype();
            PanelView view = PanelView.Build(session, drag, message);
            expected.Set(view.Expected);
            changed.Set(view.Changed);
            last.Set(view.Last);
            hint.text = view.Hint;
            talkTitle.text = view.TalkTitle;
            SetStartLabel(view.StartLabel);

            // No talk break before the first song
            talkBox.SetActive(view.TalkShown);
            data_girls.girls hostGirl = view.Host < 0 ? null : session.Show.Girls[view.Host];
            hostPlace.SetGirl(hostGirl);
            Ui.SetEnergyBar(hostPlace.Bar, hostGirl is null ? 0 : session.State.Energy[view.Host]);
            hostPlace.SetHighlight(view.HostHighlight);
            if (cancelOverlay != null)
                cancelOverlay.SetActive(false);
            hostPlace.SetDimmed(false);
            ShowCancel();
            hostPlace.Icon.Set(IconKind.None, "");
            // Greyed out while no talk break can be called
            talkBoxGroup.alpha = view.TalkOpen || view.Host >= 0 ? 1f : 0.45f;

            int[] perRow = new int[5], placed = new int[5];
            int rows = 0;
            foreach (SlotView slot in view.Slots)
            {
                perRow[slot.Row]++;
                rows = Math.Max(rows, slot.Row + 1);
            }
            for (int j = 0; j < Slots.Count; j++)
            {
                PortraitWidget widget = Slots[j];
                if (j >= view.Slots.Count)
                {
                    widget.Root.SetActive(false);
                    continue;
                }
                SlotView slot = view.Slots[j];
                widget.Root.SetActive(true);
                float x = TriangleCenterX + (placed[slot.Row]++ - (perRow[slot.Row] - 1) / 2f) * CellWidth - 27f;
                Ui.Place(widget.Root, x, TriangleTop + slot.Row * RowHeight, 54f, 58f);
                // A place whose idol hosts the talk break shows empty until the song
                bool shown = slot.Member >= 0 && !slot.Hosting;
                widget.SetGirl(shown ? session.Show.Girls[slot.Member] : null);
                Ui.SetEnergyBar(widget.Bar, shown ? session.State.Energy[slot.Member] : 0);
                widget.SetDimmed(slot.Dragged);
                widget.SetHighlight(slot.Highlight);
                widget.Label.text = slot.Roll;
                widget.Icon.Set(slot.Icon, slot.Tip);
                // The roll, then the icon; the icon alone sits in the middle
                widget.Icon.PlaceAt(slot.Roll.Length > 0 ? 29f : 27f - IconWidget.Size / 2f, 59f);
                // With a talk break first, the formation waits for it
                widget.Group.alpha = view.Faded ? 0.45f : 1f;
            }
            formationNote.text = view.FormationNote;
            Ui.Place(formationNote.gameObject, TriangleCenterX - 170f, TriangleTop + rows * RowHeight, 340f, 22f);
            BenchColumn.Render(view);
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
        /// A place whose idol hosts the talk break shows empty, so nobody is picked up there.
        /// </summary>
        public static int MemberAt(DropPoint from, int benchMember)
        {
            ShowSession session = ShowSession.Current;
            if (session == null)
                return -1;
            switch (from.Spot)
            {
                case Spot.Place:
                    int member = from.Place >= 0 && from.Place < session.Lineup.Length ? session.Lineup[from.Place] : -1;
                    return member == session.Host ? -1 : member;
                case Spot.Host:
                    // The host is cancelled with a click, not dragged off
                    return -1;
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
            for (int j = 0; j < Slots.Count; j++)
            {
                if (Slots[j].Root.activeSelf && Slots[j].Hover != null && Slots[j].Hover.hovering)
                    return DropPoint.AtPlace(j);
            }
            if (hostPlace.Hover != null && hostPlace.Hover.hovering)
                return DropPoint.Host;
            return null;
        }

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
        /// While the mouse is over the talk break's host, the game's cancel icon over her says a click cancels it.
        /// </summary>
        private static void ShowCancel()
        {
            if (!Alive || cancelOverlay == null)
                return;
            ShowSession session = ShowSession.Current;
            bool show = session != null && session.Host >= 0 && !session.Playing && hostPlace.Hover != null && hostPlace.Hover.hovering;
            if (cancelOverlay.activeSelf == show)
                return;
            cancelOverlay.SetActive(show);
            hostPlace.SetDimmed(show);
        }

        /// <summary>
        /// A quick click: a bench idol takes the first empty place she can, otherwise the hint says to drag; the
        /// talk break's host is cancelled.
        /// </summary>
        public static void OnClick(int member, DropPoint from) => Safely(() =>
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !Alive || drag != null || session.Playing)
                return;
            if (from.Spot == Spot.Host)
            {
                if (session.Host >= 0 && session.Drop(session.Host, DropPoint.Host, DropPoint.Bench).Changed)
                {
                    message = null;
                    Render();
                }
                return;
            }
            if (from.Spot != Spot.Bench || member < 0)
                return;
            DropResult result = session.Click(member);
            message = result.Allowed ? null : result.Message;
            if (result.Allowed && result.Changed)
                Manager()?.OnInsert();
            Render();
        }, "clicking an idol");
    }

    /// <summary>
    /// The popup's column of idols becomes the show's bench: everyone in the agency who isn't on stage or hosting,
    /// with the show's energy, to drag onto a place or the host place. The column picks its columns and scale from
    /// how many it holds.
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
            Render(PanelView.Build(session));
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
