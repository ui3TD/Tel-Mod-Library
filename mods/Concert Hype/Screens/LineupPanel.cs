using ConcertHype.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConcertHype
{
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
            root = Ui.NewChild(container, Name).gameObject;
            Ui.Stretch(root);
            root.AddComponent<PanelDriver>();

            GameObject labelTemplate = Ui.LabelTemplate(popup);
            // Titles and values copied from the game's, in its places: the first column where Center is, the
            // second row where the center's portrait would end
            GameObject centerTitle = container.Find("CenterTitle")?.gameObject ?? labelTemplate;
            Rect first = Ui.Placed(centerTitle, container), value = Ui.Placed(popup.NextSong_Stamina, container), title = Ui.Placed(labelTemplate, container);
            float columnX = first.xMin + value.xMin - title.xMin;
            Color plain = Ui.PlainColor(popup);
            InfoIcon info = InfoIcon.From(popup);
            expected = ReadoutWidget.Make(root.transform, container, "Expected", labelTemplate, popup.NextSong_Stamina, info, columnX, first.yMin, plain);
            changed = ReadoutWidget.Make(root.transform, container, "Changed", labelTemplate, popup.NextSong_Stamina, info, SecondColumnX, first.yMin, plain);
            last = ReadoutWidget.Make(root.transform, container, "Last", labelTemplate, popup.NextSong_Stamina, null, SecondColumnX, first.yMin + SecondRow, plain);
            // The talk break's tab: its hosts in a row where the formation's top would be
            talkBox = Ui.NewChild(root.transform, "TalkBreak").gameObject;
            Ui.Stretch(talkBox);
            for (int k = 0; k < Engine.MaxHosts; k++)
            {
                PortraitWidget host = PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, talkBox.transform, "Host " + k, DropPoint.AtHost(k));
                host.PlaceAt(HostCorner(k), frame.Scale);
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
                Slots.Add(PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, root.transform, "Slot " + j, DropPoint.AtPlace(j), successBar: true));

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
                        BenchColumn.Render(ShowSession.Current == null ? null : PanelView.Build(ShowSession.Current, tab: Tab, group: BenchColumn.Group));
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
            PanelView view = PanelView.Build(session, drag, message, Tab, BenchColumn.Group);
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
                widget.SetEnergy(girl is null ? 0 : session.State.Energy[host.Member]);
                widget.SetHighlight(host.Highlight);
                widget.SetBarTips(host.EnergyTip, null, view.Dragging);
                widget.SetDimmed(host.Dragged);
                if (CancelOverlays[k] != null)
                    CancelOverlays[k].SetActive(false);
                widget.PlaceAt(HostCorner(k), frame.Scale);
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
                widget.PlaceAt(places[j], frame.Scale);
                widget.SetGirl(slot.Member >= 0 ? session.Show.Girls[slot.Member] : null);
                widget.SetEnergy(slot.Member >= 0 ? session.State.Energy[slot.Member] : 0);
                widget.SetDimmed(slot.Dragged);
                widget.SetHighlight(slot.Highlight);
                widget.Label.text = "";
                widget.SetSuccess(slot.Success);
                widget.SetBarTips(slot.EnergyTip, slot.SuccessTip, view.Dragging);
                widget.Icon.Set(slot.Icon, slot.Tip);
                widget.SetBarIcon(slot.Icon != IconKind.None);
            }
            BenchColumn.Render(view);
        }

        private static void OnTab(FormationTab tab) => Ui.Safely(() =>
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
        /// The top-left corner of a host place's portrait on the talk break's tab, as Render places it.
        /// </summary>
        public static Vector2 HostCorner(int place) =>
            new Vector2(frame.CenterX + ((place - (Engine.MaxHosts - 1) / 2f) * CellWidth - PortraitWidget.Width / 2f) * frame.Scale, TriangleTop);

        /// <summary>
        /// The top-left corner of each of these host places' portraits.
        /// </summary>
        public static Vector2[] HostCorners(IList<int> places) => places.Select(HostCorner).ToArray();

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
                places[j] = new Vector2(frame.CenterX + ((placed[row]++ - (perRow[row] - 1) / 2f) * CellWidth - PortraitWidget.Width / 2f) * scale, TriangleTop + row * RowHeight * scale);
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
                BenchLeft = BenchColumn.LeftEdge(owner, container) - Gap,
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

        private static void OnReset() => Ui.Safely(() =>
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
            Ui.Safely(() =>
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
                Ui.Safely(Render, "showing the drop");
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
            Ui.Safely(() =>
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
            RectTransform rect = Ui.NewChild(center, "Cancel");
            GameObject overlay = rect.gameObject;
            Image image = overlay.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
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
        public static void OnClick(int member, DropPoint from) => Ui.Safely(() =>
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
}
