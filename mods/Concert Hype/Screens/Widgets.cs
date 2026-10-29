using ConcertHype.Model;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ConcertHype
{
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
            GameObject root = Ui.NewChild(parent, name).gameObject;
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
            GameObject root = Ui.NewChild(parent, name).gameObject;
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
            TextMeshProUGUI text = Ui.CopyLabel(source, parent, name);
            Ui.MoveTo(text.gameObject, container, x, y);
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
    /// bar shows the show's energy over a grey track, with a short label and an icon. A place in the lineup also
    /// has a success bar under the energy bar: her chance of a success, green over red. Pressing and holding the
    /// picture drags her.
    /// </summary>
    internal sealed class PortraitWidget
    {
        /// <summary>
        /// A portrait's size, at scale 1, as the game's concert prefab is laid out.
        /// </summary>
        public const float Width = 54f, Height = 58f;

        /// <summary>
        /// The energy bar's empty part; the success bar's filled part (a green unlike the energy bar's) and its
        /// empty part.
        /// </summary>
        private static readonly Color EnergyTrack = new Color(0.67f, 0.67f, 0.66f, 0.55f);
        private static readonly Color SuccessFill = new Color32(96, 196, 176, 255);
        private static readonly Color SuccessTrack = mainScript.red32;

        /// <summary>
        /// The space between a place's bars and its icon beside them.
        /// </summary>
        private const float IconGap = 2f;

        /// <summary>
        /// A place's bars and their tracks and hover strips, each with its full-width size and position.
        /// </summary>
        private readonly List<BarPart> barParts = new List<BarPart>();

        private sealed class BarPart
        {
            public RectTransform Rect;
            public Vector2 Size, At;
        }

        public GameObject Root;
        public CanvasGroup Group;
        public Button Button;
        public ButtonDefault Hover;
        public DragSource Drag;
        public Image Picture;
        public Image Background;
        public Image Bar;
        public Image BarTrack;
        public Image Success;
        public Image SuccessBack;
        public IconTip EnergyHover;
        public IconTip SuccessHover;
        public TextMeshProUGUI Label;
        public IconWidget Icon;
        private GirlProfileOnHover profile;
        private Color backgroundColor;
        private Image.Type pictureType;
        private float pictureUnits = 1f;
        private bool hasGirl;
        private readonly List<GameObject> shadows = new List<GameObject>();

        /// <param name="iconOnPicture">The icon sits on the picture (the bench), else beside the label under it.</param>
        /// <param name="successBar">A place in the lineup: it shows her chance of a success under her energy.</param>
        /// <param name="draggable">Pressing and holding the picture drags her; not on stage, where she sings.</param>
        public static PortraitWidget Make(GameObject prefab, GameObject labelTemplate, Transform parent, string name, DropPoint from, bool iconOnPicture = false,
            bool successBar = false, bool draggable = true)
        {
            PortraitWidget widget = new PortraitWidget { Root = Ui.Copy(prefab, parent, Shapes.Portrait, "idol portrait", name) };
            widget.Group = widget.Root.AddComponent<CanvasGroup>();
            Transform center = widget.Root.transform.Find("Center");
            widget.Picture = center?.GetComponent<Image>();
            if (widget.Picture != null)
            {
                widget.pictureType = widget.Picture.type;
                widget.pictureUnits = widget.Picture.pixelsPerUnitMultiplier;
                widget.profile = widget.Picture.GetComponent<GirlProfileOnHover>();
            }
            widget.Button = center?.GetComponent<Button>();
            widget.Hover = center?.GetComponent<ButtonDefault>();
            widget.Background = widget.Root.transform.Find("BG")?.GetComponent<Image>();
            widget.Bar = widget.Root.transform.Find("Stamina")?.GetComponent<Image>();
            if (widget.Bar != null)
            {
                widget.BarTrack = BarLike(widget.Bar, "Stamina Track", EnergyTrack, 0f);
                float barHeight = ((RectTransform)widget.Bar.transform).rect.height;
                if (successBar)
                {
                    float below = barHeight + 1f;
                    widget.SuccessBack = BarLike(widget.Bar, "Success Track", SuccessTrack, below);
                    widget.Success = BarLike(widget.Bar, "Success", SuccessFill, below);
                    // The energy strip reaches up, the success strip down, so they don't overlap
                    widget.EnergyHover = HoverStrip(widget.Bar, "Energy Hover", 0f, HoverPad, 0.5f);
                    widget.SuccessHover = HoverStrip(widget.Bar, "Success Hover", below, 0.5f, HoverPad);
                }
                else
                {
                    widget.EnergyHover = HoverStrip(widget.Bar, "Energy Hover", 0f, HoverPad, HoverPad);
                }
                foreach (Component part in new Component[] { widget.Bar, widget.BarTrack, widget.SuccessBack, widget.Success, widget.EnergyHover, widget.SuccessHover })
                {
                    if (part != null)
                    {
                        RectTransform rect = (RectTransform)part.transform;
                        widget.barParts.Add(new BarPart { Rect = rect, Size = rect.sizeDelta, At = rect.anchoredPosition });
                    }
                }
            }
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
                if (draggable)
                {
                    widget.Drag = center.gameObject.AddComponent<DragSource>();
                    widget.Drag.From = from;
                }
            }
            widget.Label = Ui.Label(labelTemplate, widget.Root.transform, "Label", -20f, 58f, 46f, 18f, 13f);
            widget.Label.alignment = TextAlignmentOptions.TopRight;
            // On the picture, the icon is part of it: a press there still drags her
            widget.Icon = IconWidget.Make(labelTemplate, iconOnPicture && center != null ? center : widget.Root.transform, "Icon");
            widget.Icon.PlaceAt(iconOnPicture ? 27f : 29f, iconOnPicture ? 27f : 59f);
            return widget;
        }

        /// <summary>
        /// A plain bar where the game's energy bar sits, this far under it, at full length: the energy bar's track
        /// sits just behind it, a success bar's parts after it.
        /// </summary>
        private static Image BarLike(Image bar, string name, Color color, float below)
        {
            RectTransform from = (RectTransform)bar.transform;
            RectTransform rect = Ui.NewChild(from.parent, name);
            GameObject obj = rect.gameObject;
            Ui.MatchAnchors(rect, from);
            rect.sizeDelta = from.sizeDelta;
            rect.anchoredPosition = from.anchoredPosition - new Vector2(0f, below);
            Image image = obj.AddComponent<Image>();
            image.sprite = bar.sprite;
            image.type = bar.type;
            image.color = color;
            image.raycastTarget = false;
            obj.transform.SetSiblingIndex(below > 0f ? from.parent.childCount - 1 : from.GetSiblingIndex());
            return image;
        }

        /// <summary>
        /// How far a bar's hover strip reaches past the bar, in pixels.
        /// </summary>
        private const float HoverPad = 3f;

        /// <summary>
        /// An invisible strip over a bar (this far under the energy bar), reaching this far above and below it, that
        /// shows a tooltip while hovered. It takes the mouse only while it has a tip.
        /// </summary>
        private static IconTip HoverStrip(Image bar, string name, float below, float up, float down)
        {
            RectTransform from = (RectTransform)bar.transform;
            RectTransform rect = Ui.NewChild(from.parent, name);
            GameObject obj = rect.gameObject;
            Ui.MatchAnchors(rect, from);
            rect.sizeDelta = from.sizeDelta + new Vector2(0f, up + down);
            // The extra height grows around the pivot; the shift puts "up" above the bar and "down" below it
            rect.anchoredPosition = from.anchoredPosition + new Vector2(0f, up - (1f - from.pivot.y) * (up + down) - below);
            Image image = obj.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            obj.transform.SetAsLastSibling();
            return obj.AddComponent<IconTip>();
        }

        /// <summary>
        /// A place in the lineup: its icon sits right of its bars, as tall as the bars together, and the bars end
        /// short of it. With no icon the bars fill the full width.
        /// </summary>
        public void SetBarIcon(bool icon)
        {
            foreach (BarPart part in barParts)
            {
                part.Rect.sizeDelta = part.Size;
                part.Rect.anchoredPosition = part.At;
            }
            if (!icon || BarTrack == null)
                return;
            Rect top = Ui.Placed(BarTrack.gameObject, Root.transform), bottom = SuccessBack != null ? Ui.Placed(SuccessBack.gameObject, Root.transform) : top;
            float side = bottom.yMax - top.yMin, cut = side + IconGap;
            Ui.Place(Icon.Root, top.xMax - side, top.yMin, side, side);
            foreach (BarPart part in barParts)
            {
                // Shorter from the right, whatever the part's anchors and pivot
                part.Rect.sizeDelta = new Vector2(part.Size.x - cut, part.Size.y);
                part.Rect.anchoredPosition = new Vector2(part.At.x - cut * part.Rect.pivot.x, part.At.y);
            }
        }

        /// <summary>
        /// The bars' tooltips. While an idol is dragged the strips let the mouse through, so a drop still finds the
        /// place's button.
        /// </summary>
        public void SetBarTips(string energy, string success, bool dragging)
        {
            SetHover(EnergyHover, energy, dragging);
            SetHover(SuccessHover, success, dragging);
        }

        private static void SetHover(IconTip hover, string text, bool dragging)
        {
            if (hover == null)
                return;
            hover.Text = text ?? "";
            hover.GetComponent<Image>().raycastTarget = !dragging && hover.Text.Length > 0;
        }

        /// <summary>
        /// Her chance of a success in percent on the success bar; below 0 hides it (an empty place).
        /// </summary>
        public void SetSuccess(double chance)
        {
            bool shown = chance >= 0;
            if (Success != null)
            {
                Success.gameObject.SetActive(shown);
                Success.transform.localScale = new Vector3((float)(Math.Max(0, Math.Min(100, chance)) / 100.0), 1f, 1f);
            }
            if (SuccessBack != null)
                SuccessBack.gameObject.SetActive(shown);
        }

        /// <summary>
        /// Her energy on the energy bar, coloured as the game colours stamina.
        /// </summary>
        public void SetEnergy(double energy)
        {
            if (Bar == null)
                return;
            Bar.color = energy < 30 ? mainScript.red32 : energy >= 70 ? mainScript.green32 : mainScript.blue32;
            Bar.transform.localScale = new Vector3((float)(energy / 100.0), 1f, 1f);
        }

        /// <summary>
        /// Puts the portrait's top-left corner at x, y in the coordinates of <see cref="Ui.Place"/>, at this scale.
        /// </summary>
        public void PlaceAt(Vector2 at, float scale)
        {
            Ui.Place(Root, at.x, at.y, Width, Height);
            Root.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// An empty place looks like the game's empty senbatsu place: its framed box, with no shadows.
        /// </summary>
        public void SetGirl(data_girls.girls girl)
        {
            hasGirl = girl != null;
            foreach (GameObject shadow in shadows)
                shadow.SetActive(hasGirl);
            if (BarTrack != null)
                BarTrack.gameObject.SetActive(hasGirl);
            if (girl is null)
            {
                if (profile != null)
                    profile.enabled = false;
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
            if (profile != null)
            {
                profile.enabled = true;
                profile.Set(girl);
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
            Dim(BarTrack, EnergyTrack, dimmed);
            Dim(Success, SuccessFill, dimmed);
            Dim(SuccessBack, SuccessTrack, dimmed);
        }

        private static void Dim(Image image, Color color, bool dimmed)
        {
            if (image != null)
                image.color = new Color(color.r, color.g, color.b, color.a * (dimmed ? 0.45f : 1f));
        }

        public void SetHighlight(Highlight highlight)
        {
            if (Background != null)
                Background.color = highlight == Highlight.Allowed ? (Color)mainScript.green32 : highlight == Highlight.Refused ? (Color)mainScript.red32
                    : backgroundColor;
        }
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
            Ui.PlaceCentred(root, x, y, gameSize.x, gameSize.y);
            root.transform.localRotation = Quaternion.identity;
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
}
