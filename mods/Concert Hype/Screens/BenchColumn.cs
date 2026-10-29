using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ConcertHype
{
    /// <summary>
    /// The popup's column of idols becomes the show's bench: everyone in the agency who isn't in the shown tab's
    /// set-up (on stage, or hosting the talk break), with the show's energy, to drag onto its places. Two columns
    /// fill a strip at the column's right edge, from its top (under the group dropdown, when there is one) to the
    /// bottom of the screen; a longer bench scrolls, with a scrollbar left of it.
    /// </summary>
    internal static class BenchColumn
    {
        private const float CellWidth = 62f, CellHeight = 66f;

        /// <summary>
        /// The bench's width, between the game's column (about 200) and two portraits at full size (124).
        /// </summary>
        public const float BenchWidth = 160f;

        private const float WheelStep = 30f, BarGap = 4f, DropdownGap = 6f, DefaultBarWidth = 20f, TitleGap = 4f;

        /// <summary>
        /// The panel behind the idols: its inner margin, and its tint, dark and see-through.
        /// </summary>
        private const float PanelPad = 6f;

        private static readonly Color PanelTint = new Color(0f, 0f, 0f, 0.35f);

        /// <summary>
        /// The idols' width inside the panel: the two columns fill it.
        /// </summary>
        private const float InnerWidth = BenchWidth - 2 * PanelPad;

        /// <summary>
        /// The bench reaches down to this far above the popup's bottom edge.
        /// </summary>
        private const float BottomMargin = 12f;

        private static RectTransform content;
        private static GameObject scrollbar;
        private static GameObject dropdown;
        private static TextMeshProUGUI title;
        private static RectTransform panel;

        /// <summary>
        /// The bench's title, dropdown, panel and scrollbar, which fade with the portraits while a song plays.
        /// </summary>
        private static readonly List<CanvasGroup> Frame = new List<CanvasGroup>();
        private static float barWidth = DefaultBarWidth;
        private static List<GroupChoice> groups = new List<GroupChoice>();

        private static Concert_Popup owner;
        private static readonly List<PortraitWidget> Widgets = new List<PortraitWidget>();
        private static GridLayoutGroup grid;
        private static bool gridWasEnabled;
        private static bool saved;

        /// <summary>
        /// The group the bench shows, by ID; -1 for everyone. Each show starts with everyone.
        /// </summary>
        public static int Group { get; private set; } = -1;

        public static void Build(Concert_Popup popup, ShowSession session)
        {
            owner = popup;
            Widgets.Clear();
            Group = -1;
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

            panel = MakePanel(column);
            title = MakeTitle(popup, column);
            content = MakeScroll(column, out WheelScroll scroll);
            scrollbar = MakeScrollbar(column, scroll);
            groups = session.Show.Groups;
            dropdown = MakeDropdown(column, PanelView.GroupChoices(groups));
            Frame.Clear();
            foreach (Component part in new Component[] { title, panel, dropdown?.transform, scrollbar?.transform })
            {
                if (part == null)
                    continue;
                CanvasGroup group = part.GetComponent<CanvasGroup>() ?? part.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                Frame.Add(group);
            }
            GameObject labelTemplate = Ui.LabelTemplate(popup);
            for (int m = 0; m < session.Show.Girls.Count; m++)
            {
                // Why she can't perform is an icon on her picture
                PortraitWidget widget = PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, content, "Bench " + m, DropPoint.Bench, iconOnPicture: true);
                widget.SetGirl(session.Show.Girls[m]);
                if (widget.Drag != null)
                    widget.Drag.BenchMember = m;
                widget.Label.gameObject.SetActive(false);
                Widgets.Add(widget);
            }
            Render(PanelView.Build(session, tab: LineupPanel.Tab, group: Group));
        }

        /// <summary>
        /// The left edge of the bench and its scrollbar's strip, in the coordinates of <see cref="Ui.Place"/> for
        /// this container: the formation stays left of it.
        /// </summary>
        public static float LeftEdge(Concert_Popup popup, Transform container) =>
            Ui.Placed(popup.Girls, container).xMax - BenchWidth - BarGap - barWidth;

        /// <summary>
        /// Lays out, from the column's top: the Backstage title, the group dropdown, and the panel holding the bench and
        /// its scrollbar down to the bottom of the screen, which the window's shape moves. Gives the bench's height.
        /// </summary>
        private static float Arrange()
        {
            float extra = Math.Max(0f, ((RectTransform)owner.transform).rect.height - BottomMargin - Ui.Placed(owner.Girls, owner.transform).yMax);
            float strip = BenchWidth + BarGap + barWidth;
            float top = 0f;
            if (title != null)
            {
                float height = title.GetPreferredValues(title.text).y;
                Fit(title.transform, -strip, 0f, 0f, 0f);
                RectTransform rect = title.rectTransform;
                rect.anchorMin = new Vector2(1f, 1f);
                rect.offsetMin = new Vector2(-strip, -height);
                top = height + TitleGap;
            }
            if (dropdown != null)
            {
                RectTransform box = (RectTransform)dropdown.transform;
                // The game's dropdown is built at a larger size and scaled down: its size is set in its own units, so
                // on screen it spans the bench and its scrollbar
                float scale = box.localScale.x > 0.01f ? box.localScale.x : 1f;
                if (box.parent == owner.Girls.transform)
                {
                    box.anchorMin = box.anchorMax = box.pivot = new Vector2(1f, 1f);
                    box.anchoredPosition = new Vector2(0f, -top);
                    box.sizeDelta = new Vector2(strip / scale, box.sizeDelta.y);
                }
                // Its drawn box (Main) is taller than its own rect: the bench starts under what's drawn
                Transform drawn = box.Find("Content/Main") ?? box;
                top = Math.Max(Ui.Placed(drawn.gameObject, owner.Girls.transform).yMax, box.sizeDelta.y * box.localScale.y) + DropdownGap;
            }
            // The panel takes the whole strip; the bench and its scrollbar sit inside its margin
            if (panel != null)
                Fit(panel, -strip, 0f, top, extra);
            float inside = top + PanelPad, below = extra - PanelPad;
            Fit(content.parent, -PanelPad - InnerWidth, -PanelPad, inside, below);
            if (scrollbar != null)
                Fit(scrollbar.transform, -strip + PanelPad, -strip + PanelPad + barWidth, inside, below);
            return ((RectTransform)owner.Girls.transform).rect.height + below - inside;
        }

        /// <summary>
        /// The game's title label (the next-song screen's, as the readouts' titles), reading Backstage on one line.
        /// </summary>
        private static TextMeshProUGUI MakeTitle(Concert_Popup popup, Transform column)
        {
            GameObject source = popup.NextSong_Container.transform.Find("CenterTitle")?.gameObject;
            if (source == null || source.GetComponent<TextMeshProUGUI>() == null)
            {
                Log.WarnOnce("bench title", "couldn't find the game's title label; the bench has no title");
                return null;
            }
            TextMeshProUGUI text = Ui.CopyLabel(source, column, "Bench Title");
            text.transform.localScale = Vector3.one;
            text.alignment = TextAlignmentOptions.BottomLeft;
            text.text = Texts.Get("CONCERTHYPE__BACKSTAGE");
            FitOneLine(text);
            return text;
        }

        /// <summary>
        /// A dark, see-through rounded box behind the bench, in the game's rounded sprite (the café scrollbar's
        /// track); without it, a plain box (logged once). It takes no clicks.
        /// </summary>
        private static RectTransform MakePanel(Transform column)
        {
            GameObject box = Ui.NewChild(column, "Bench Panel").gameObject;
            Image image = box.AddComponent<Image>();
            Image rounded = PopupManager.GetObject(PopupManager._type.cafe)?.GetComponent<Cafe_Popup>()?.SelectGirl_ScrollBar?.transform.Find("Background")?.GetComponent<Image>();
            if (rounded != null && rounded.sprite != null)
            {
                image.sprite = rounded.sprite;
                image.type = rounded.type;
                image.pixelsPerUnitMultiplier = rounded.pixelsPerUnitMultiplier;
            }
            else
            {
                Log.WarnOnce("bench panel", "couldn't find the game's rounded sprite; the bench's panel is a plain box");
            }
            image.color = PanelTint;
            image.raycastTarget = false;
            return (RectTransform)box.transform;
        }

        /// <summary>
        /// A strip of the column, from x left to x right of its right edge, top down from its top, and reaching extra
        /// below its bottom.
        /// </summary>
        private static void Fit(Transform obj, float left, float right, float top, float extra)
        {
            RectTransform rect = (RectTransform)obj;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, -extra);
            rect.offsetMax = new Vector2(right, -top);
        }

        /// <summary>
        /// A view that clips the bench and scrolls it with the mouse wheel when it's longer than it shows. Its
        /// transparent box takes the wheel between portraits too.
        /// </summary>
        private static RectTransform MakeScroll(Transform column, out WheelScroll scroll)
        {
            GameObject view = Ui.NewChild(column, "Bench Scroll").gameObject;
            Ui.Stretch(view);
            view.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            view.AddComponent<RectMask2D>();
            RectTransform rect = Ui.NewChild(view.transform, "Content");
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            scroll = view.AddComponent<WheelScroll>();
            scroll.viewport = (RectTransform)view.transform;
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = WheelStep;
            return rect;
        }

        /// <summary>
        /// A copy of the café popup's idol list scrollbar, wired to the bench as the game wires its own: the list
        /// moves the slider and the slider moves the list. Null (logged once) without the game's.
        /// </summary>
        private static GameObject MakeScrollbar(Transform column, WheelScroll scroll)
        {
            GameObject template = PopupManager.GetObject(PopupManager._type.cafe)?.GetComponent<Cafe_Popup>()?.SelectGirl_ScrollBar;
            if (template == null || template.GetComponent<Slider>() == null || template.GetComponent<SliderDefault>() == null)
            {
                Log.WarnOnce("scrollbar", "couldn't find the game's scrollbar; a long bench scrolls with the mouse wheel only");
                barWidth = DefaultBarWidth;
                return null;
            }
            GameObject bar = Ui.Copy(template, column, Shapes.Scrollbar, "scrollbar", "Bench Scrollbar");
            float width = ((RectTransform)template.transform).rect.width;
            barWidth = width > 1f ? width : DefaultBarWidth;
            // The café's is scaled up a little; at that scale it would stick out past the bench
            bar.transform.localScale = Vector3.one;
            Slider slider = bar.GetComponent<Slider>();
            SliderDefault follow = bar.GetComponent<SliderDefault>();
            // New events drop the game's listeners, which move the café's list
            slider.onValueChanged = new Slider.SliderEvent();
            slider.onValueChanged.AddListener(value => scroll.verticalNormalizedPosition = value);
            scroll.onValueChanged = new ScrollRect.ScrollRectEvent();
            scroll.onValueChanged.AddListener(follow.setValueFromVector);
            slider.value = 1f;
            bar.SetActive(false);
            return bar;
        }

        /// <summary>
        /// A copy of the game's dropdown (the settings' Quality one, as ModMenus copies it) listing the groups to
        /// show. None with fewer than two groups, or without the game's (logged once).
        /// </summary>
        private static GameObject MakeDropdown(Transform column, List<string> choices)
        {
            if (choices.Count == 0)
                return null;
            Transform panel = PopupManager.GetObject(PopupManager._type.main_menu_settings)?.transform.Find("Panel");
            Transform screen = panel?.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Screen"));
            GameObject template = screen?.Find("Quality/Dropdown")?.gameObject;
            if (template == null || template.GetComponent<Michsky.UI.ModernUIPack.CustomDropdown>() == null)
            {
                Log.WarnOnce("dropdown", "couldn't find the game's dropdown; the bench shows every group");
                return null;
            }
            GameObject box = Ui.Copy(template, column, Shapes.Dropdown, "dropdown", "Bench Group");
            Michsky.UI.ModernUIPack.CustomDropdown list = box.GetComponent<Michsky.UI.ModernUIPack.CustomDropdown>();
            list.dropdownItems.Clear();
            foreach (string choice in choices)
                list.CreateNewItemFast(choice, null);
            // A new event drops the game's listeners, which set the graphics quality
            list.dropdownEvent = new Michsky.UI.ModernUIPack.CustomDropdown.DropdownEvent();
            list.dropdownEvent.AddListener(OnGroup);
            list.saveSelected = false;
            list.invokeAtStart = false;
            list.isListItem = false;
            list.SetupDropdown();
            list.ChangeDropdownInfo(0);
            // The game's box is far wider: its texts stay on one line here, shrinking to fit
            foreach (TextMeshProUGUI text in box.GetComponentsInChildren<TextMeshProUGUI>(true))
                FitOneLine(text);
            return box;
        }

        private static void FitOneLine(TextMeshProUGUI text)
        {
            float size = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.75f;
        }

        private static void OnGroup(int item) => Ui.Safely(() =>
        {
            Group = PanelView.GroupAt(groups, item);
            LineupPanel.Render();
        }, "filtering the bench");

        /// <summary>
        /// The bench fades while a song plays, its title, dropdown, panel and scrollbar with it, and takes no clicks
        /// until it's back: they're the show's own, so the game's column is untouched.
        /// </summary>
        public static void Fade(float alpha)
        {
            foreach (PortraitWidget widget in Widgets)
            {
                if (widget.Group != null)
                    widget.Group.alpha = alpha;
            }
            foreach (CanvasGroup group in Frame)
            {
                if (group == null)
                    continue;
                group.alpha = alpha;
                group.blocksRaycasts = group.interactable = alpha > 0.99f;
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
            content = null;
            scrollbar = null;
            dropdown = null;
            title = null;
            panel = null;
            Frame.Clear();
            groups = new List<GroupChoice>();
            Group = -1;
            owner = null;
        }

        public static void Render(PanelView view)
        {
            ShowSession session = ShowSession.Current;
            if (view == null || session == null || owner == null || content == null || Widgets.Count != session.Show.Girls.Count)
                return;
            float shown = Arrange();
            float scale = PanelView.TwoColumnBench(view.Bench.Count, InnerWidth, shown, CellWidth, CellHeight, out float height, out bool scrolls);
            content.sizeDelta = new Vector2(0f, height);
            // Only while there's more than it shows
            if (scrollbar != null)
                scrollbar.SetActive(scrolls);
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
                // Read left to right, two to a row
                float x = ((i % 2) * CellWidth + (CellWidth - PortraitWidget.Width) / 2f) * scale;
                float y = ((i / 2) * CellHeight + (CellHeight - PortraitWidget.Height) / 2f) * scale;
                widget.PlaceAt(new Vector2(x, y), scale);
                widget.SetEnergy(bench.Energy);
                widget.Icon.Set(bench.Icon, bench.Tip, bench.Count);
                widget.SetBarTips(bench.EnergyTip, null, view.Dragging);
                // An idol who can't perform is dimmed and can't be dragged; the icon saying why stays bright
                widget.SetDimmed(!bench.Available);
            }
        }
    }
}
