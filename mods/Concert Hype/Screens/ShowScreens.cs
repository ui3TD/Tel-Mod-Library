using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
    /// A copy of the concert's idol portrait (the picture is a game button, with its hover profile) whose energy
    /// bar shows the show's energy, with a short label.
    /// </summary>
    internal sealed class PortraitWidget
    {
        public GameObject Root;
        public Button Button;
        public Image Picture;
        public Image Background;
        public Image Bar;
        public TextMeshProUGUI Label;
        private Color backgroundColor;

        public static PortraitWidget Make(GameObject prefab, GameObject labelTemplate, Transform parent, string name, UnityAction onClick)
        {
            PortraitWidget widget = new PortraitWidget { Root = Ui.Copy(prefab, parent, Shapes.Portrait, "idol portrait", name) };
            Transform center = widget.Root.transform.Find("Center");
            widget.Picture = center?.GetComponent<Image>();
            widget.Button = center?.GetComponent<Button>();
            widget.Background = widget.Root.transform.Find("BG")?.GetComponent<Image>();
            widget.Bar = widget.Root.transform.Find("Stamina")?.GetComponent<Image>();
            if (widget.Background != null)
                widget.backgroundColor = widget.Background.color;
            if (widget.Button != null)
            {
                // The game's portrait is a button only for its hover profile: its Button starts disabled
                widget.Button.onClick = new Button.ButtonClickedEvent();
                widget.Button.onClick.AddListener(onClick);
                widget.Button.enabled = true;
                Ui.Enable(widget.Button, true);
            }
            widget.Label = Ui.Label(labelTemplate, widget.Root.transform, "Label", -8f, 58f, 70f, 18f, 13f);
            widget.Label.alignment = TextAlignmentOptions.Top;
            return widget;
        }

        public void SetGirl(data_girls.girls girl)
        {
            GirlProfileOnHover hover = Picture?.GetComponent<GirlProfileOnHover>();
            if (girl is null)
            {
                if (hover != null)
                    hover.enabled = false;
                if (Picture != null)
                {
                    Picture.sprite = null;
                    Picture.color = new Color(1f, 1f, 1f, 0f);
                }
                return;
            }
            if (Picture != null)
                Picture.color = Color.white;
            if (hover != null)
            {
                hover.enabled = true;
                hover.Set(girl);
            }
        }

        public void SetDimmed(bool dimmed)
        {
            if (Picture != null)
                Picture.color = dimmed ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
            if (Bar != null)
            {
                Color bar = Bar.color;
                Bar.color = new Color(bar.r, bar.g, bar.b, dimmed ? 0.45f : 1f);
            }
        }

        public void SetSelected(bool selected, bool empty = false)
        {
            if (Background != null)
                Background.color = selected ? (Color)mainScript.green32 : empty ? new Color(0.2f, 0.2f, 0.25f, 0.6f) : backgroundColor;
        }
    }

    /// <summary>
    /// The lineup panel on the next-song screen, in place of the game's center, stamina and accident readouts.
    /// </summary>
    internal static class LineupPanel
    {
        private const string Name = "ConcertHype Lineup";
        private const float TriangleCenterX = 610f, TriangleTop = 135f, RowHeight = 76f, CellWidth = 70f;

        private static Concert_Popup owner;
        private static GameObject root;
        private static TextMeshProUGUI hype, expected, last, hint;
        private static Button talkBreak, resetLineup;
        private static readonly List<PortraitWidget> Slots = new List<PortraitWidget>();
        private static readonly List<GameObject> Hidden = new List<GameObject>();
        private static bool subscribed;

        internal static int SelectedSlot = -1;
        internal static bool PickingHost;

        private static bool Alive => root != null && owner != null;

        public static void Show(Concert_Popup popup, ShowSession session)
        {
            Ensure(popup);
            SelectedSlot = -1;
            PickingHost = false;
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
                root.SetActive(false);
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

            GameObject labelTemplate = container.Find("StaminaCostTitle")?.gameObject ?? popup.NextSong_Stamina;
            hype = Ui.Label(labelTemplate, root.transform, "Hype", 30f, 148f, 470f, 28f, 18f);
            expected = Ui.Label(labelTemplate, root.transform, "Expected", 30f, 178f, 470f, 24f, 15f);
            last = Ui.Label(labelTemplate, root.transform, "Last", 30f, 206f, 470f, 48f, 15f);
            hint = Ui.Label(labelTemplate, root.transform, "Hint", 30f, 262f, 440f, 80f, 14f);
            talkBreak = Ui.Button(popup.NextSong_Button, root.transform, "TalkBreak", 30f, 360f, 175f, 39f, OnTalkBreak);
            resetLineup = Ui.Button(popup.NextSong_Button, root.transform, "ResetLineup", 215f, 360f, 150f, 39f, OnReset);
            Ui.SetButtonText(resetLineup, Texts.Get("CONCERTHYPE__RESET_LINEUP"));

            for (int j = 0; j < GameReader.SenbatsuPositions; j++)
            {
                int slot = j;
                Slots.Add(PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, root.transform, "Slot " + j, () => OnSlot(slot)));
            }

            if (!subscribed)
            {
                ShowSession.Changed += () =>
                {
                    if (Alive && root.activeInHierarchy && ShowSession.Current != null)
                        Render();
                    CastRow.Render();
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
            PanelView view = PanelView.Build(session, SelectedSlot, PickingHost);
            hype.text = view.Hype;
            expected.text = view.Expected;
            last.text = view.Last;
            hint.text = view.Hint;
            Ui.SetButtonText(talkBreak, view.TalkBreak);
            Ui.Enable(talkBreak, view.TalkBreakEnabled);

            int[] perRow = new int[5], placed = new int[5];
            foreach (SlotView slot in view.Slots)
                perRow[slot.Row]++;
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
                widget.SetGirl(slot.Member < 0 ? null : session.Show.Girls[slot.Member]);
                Ui.SetEnergyBar(widget.Bar, slot.Member < 0 ? 0 : session.State.Energy[slot.Member]);
                string badge = PanelView.BadgeText(slot.Badge);
                widget.Label.text = slot.Roll + (badge.Length > 0 && slot.Roll.Length > 0 ? " " : "") + badge;
                widget.SetSelected(slot.Selected, slot.Member < 0);
            }
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

        private static void OnSlot(int slot) => Safely(() =>
        {
            PickingHost = false;
            SelectedSlot = SelectedSlot == slot ? -1 : slot;
            Render();
        }, "choosing a place");

        private static void OnTalkBreak() => Safely(() =>
        {
            SelectedSlot = -1;
            PickingHost = !PickingHost;
            Render();
        }, "the talk break button");

        private static void OnReset() => Safely(() =>
        {
            SelectedSlot = -1;
            PickingHost = false;
            ShowSession.Current?.ResetLineup();
            Render();
        }, "resetting the lineup");

        /// <summary>
        /// An idol in the cast column was clicked: she fills the chosen place, or hosts the talk break.
        /// </summary>
        public static void OnCast(int member) => Safely(() =>
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !Alive)
                return;
            if (PickingHost)
            {
                if (session.State.IsAvailable(member))
                {
                    PickingHost = false;
                    session.CallMc(member, new PopupStage(owner));
                }
            }
            else if (SelectedSlot >= 0 && session.Assign(SelectedSlot, member))
            {
                SelectedSlot = -1;
            }
            Render();
        }, "choosing an idol");
    }

    /// <summary>
    /// The popup's column of idols becomes the show's cast, with the show's energy, to click when changing the
    /// lineup or picking a talk break host.
    /// </summary>
    internal static class CastRow
    {
        private const int RowsPerColumn = 7, MaxColumns = 3;

        private static Concert_Popup owner;
        private static readonly List<PortraitWidget> Widgets = new List<PortraitWidget>();
        private static GridLayoutGroup grid;
        private static GridLayoutGroup.Constraint savedConstraint;
        private static int savedCount;
        private static bool saved;

        public static void Build(Concert_Popup popup, ShowSession session)
        {
            owner = popup;
            Widgets.Clear();
            Transform column = popup.Girls.transform;
            for (int i = column.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(column.GetChild(i).gameObject);

            GameObject labelTemplate = popup.NextSong_Container.transform.Find("StaminaCostTitle")?.gameObject ?? popup.NextSong_Stamina;
            int count = session.Show.Girls.Count;
            float scale = Mathf.Min(1f, (float)(RowsPerColumn * MaxColumns) / Mathf.Max(1, count));
            for (int m = 0; m < count; m++)
            {
                int member = m;
                PortraitWidget widget = PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, column, "Cast " + m, () => LineupPanel.OnCast(member));
                widget.SetGirl(session.Show.Girls[m]);
                widget.Root.transform.localScale = new Vector3(scale, scale, 1f);
                // The column's cells are tight: a small label over the bottom of the picture
                Ui.Place(widget.Label.gameObject, -8f, 34f, 70f, 14f);
                widget.Label.fontSize = 12f;
                widget.Label.color = Color.white;
                widget.Label.fontStyle = FontStyles.Bold;
                Widgets.Add(widget);
            }

            grid = column.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                if (!saved)
                {
                    savedConstraint = grid.constraint;
                    savedCount = grid.constraintCount;
                    saved = true;
                }
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Clamp(Mathf.CeilToInt(count / (float)RowsPerColumn), 1, MaxColumns);
            }
            Render();
        }

        /// <summary>
        /// Puts the column's layout back for the game's own show.
        /// </summary>
        public static void Restore()
        {
            if (grid != null && saved)
            {
                grid.constraint = savedConstraint;
                grid.constraintCount = savedCount;
            }
            Widgets.Clear();
            owner = null;
        }

        public static void Render()
        {
            ShowSession session = ShowSession.Current;
            if (session == null || owner == null || Widgets.Count != session.Show.Girls.Count)
                return;
            PanelView view = PanelView.Build(session, LineupPanel.SelectedSlot, LineupPanel.PickingHost);
            for (int m = 0; m < Widgets.Count; m++)
            {
                PortraitWidget widget = Widgets[m];
                if (widget.Root == null)
                    continue;
                CastView cast = view.Cast[m];
                Ui.SetEnergyBar(widget.Bar, cast.Energy);
                widget.Label.text = cast.State;
                // An idol who can't perform is dimmed; the label saying why stays readable
                widget.SetDimmed(!cast.Available);
            }
        }
    }

    /// <summary>
    /// The show's score under the profit on the results screen.
    /// </summary>
    internal static class ScoreLine
    {
        private const string Name = "ConcertHype Score";

        public static void Show(Concert_Popup popup, string text)
        {
            Transform final = popup.Final_Container.transform;
            Transform line = final.Find(Name);
            if (line == null)
            {
                GameObject template = final.Find("ProfitTitle")?.gameObject;
                if (template == null)
                    return;
                TextMeshProUGUI label = Ui.Label(template, final, Name, 35f, 430f, 900f, 40f, 24f);
                line = label.transform;
            }
            line.gameObject.SetActive(!string.IsNullOrEmpty(text));
            line.GetComponent<TextMeshProUGUI>().text = text ?? "";
        }
    }
}
