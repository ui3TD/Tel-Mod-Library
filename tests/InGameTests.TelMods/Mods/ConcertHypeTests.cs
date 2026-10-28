using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Concert Hype's show in the game's real concert popup. The unit tests cover the rules, the drop rules, the
    /// panel's text, the payout and each patch; this checks what only the engine does: real clicks reaching the
    /// panel's and the bench's buttons on screen, a real drag through the game's input and drag-and-drop manager,
    /// the icons' pictures and the game's tooltip over one, the game's profile card placed at the host place, a
    /// talk break played by the popup's own coroutine, the transpiled song length, the popup's tweens landing hype
    /// on the show's value, the score line on the real results screen, and no crisis popup.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class ConcertHypeTests
    {
        private const string HarmonyId = "com.tel.concerthype";

        [InGameTest(Suite = ModTest.ConcertsSuite)]
        private static IEnumerator ShowPlaysThroughThePopup(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            VirtualMouse mouse = new VirtualMouse();
            if (!mouse.Works)
            {
                ctx.Fail("The drags need the runner's virtual mouse: run with input blocked (no --allow-input)");
                yield break;
            }

            Type sessionType = mod.GetType("ConcertHype.ShowSession", true);
            object Session() => AccessTools.Property(sessionType, "Current").GetValue(null, null);
            T Get<T>(object obj, string field) => (T)AccessTools.Field(obj.GetType(), field).GetValue(obj);

            SEvent_Concerts concerts = Game.Main.Data.GetComponent<SEvent_Concerts>();
            SEvent_Concerts._concert concert = concerts.Concert;
            if (concert == null || concert.SetListItems.Count < 2 || concert.SetListItems.Any(i => i.isMC()))
            {
                ctx.Fail("The fixture's current concert should have a setlist of two songs or more and no talk breaks");
                yield break;
            }

            float timeScale = Time.timeScale;
            // The mod logs one summary line per show; anything else it logs is a problem to look at
            List<string> modLog = new();
            Application.LogCallback record = (message, stack, type) =>
            {
                if (message.Contains("[Concert Hype]") && !message.Contains("[Concert Hype] show over:"))
                    modLog.Add(type + ": " + message);
            };
            Application.logMessageReceived += record;
            using (TestTools.Restore(() => Application.logMessageReceived -= record))
            using (Game.Variable("ConcertHype_Enabled", "1"))
            using (TestTools.Restore(() => Time.timeScale = timeScale))
            using (TestTools.Restore(mouse.Off))
            {
                // The setup has no Add Talk Break button: talk breaks are called during the show
                concerts.NewConcert();
                yield return new WaitForSecondsRealtime(1f);
                Concert_New_Popup setup = Game.Main.Data.GetComponent<PopupManager>().GetByType(PopupManager._type.sevent_concert_new).obj.GetComponent<Concert_New_Popup>();
                bool talkBreakShown = setup.GetComponentsInChildren<Button>().Any(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount())
                    .Any(i => b.onClick.GetPersistentMethodName(i) == nameof(Concert_New_Popup.OnAddMC)));
                if (talkBreakShown)
                    ctx.Fail("The concert setup still shows Add Talk Break");
                yield return Game.CloseAllPopups(ctx);
                concerts.Concert = concert;

                concerts.StartConcert();
                object session = Session();
                if (session == null)
                {
                    ctx.Fail("StartConcert didn't start the show");
                    yield break;
                }
                Concert_Popup popup = Game.Main.Data.GetComponent<PopupManager>().GetByType(PopupManager._type.sevent_concert).obj.GetComponent<Concert_Popup>();
                Time.timeScale = 4f;

                // The panel's copies leave out only other mods' parts: the game's own are all listed
                Type shapes = mod.GetType("ConcertHype.Shapes", true);
                VanillaShapes.AssertCovers(ctx, shapes, "Button", popup.NextSong_Button, "Start Next Song button");
                VanillaShapes.AssertCovers(ctx, shapes, "Label", popup.NextSong_Container.transform.Find("StaminaCostTitle").gameObject, "Stamina Cost title");
                VanillaShapes.AssertCovers(ctx, shapes, "Portrait", popup.prefab_concertGirl, "concert idol portrait");

                // No card draw: the opening goes straight to the first song, and the deck never shows
                float start = Time.realtimeSinceStartup, deckShown = 0f;
                CanvasGroup deck = popup.DrawCardsButton.GetComponent<CanvasGroup>(), deckTitle = popup.DrawCardsTitle.GetComponent<CanvasGroup>();
                while (Time.realtimeSinceStartup - start < 30f && !NextSongShown(popup))
                {
                    if (popup.DrawCards.activeInHierarchy)
                        deckShown = Mathf.Max(deckShown, deck.alpha, deckTitle.alpha);
                    yield return null;
                }
                if (!popup.NextSong_Container.activeSelf)
                {
                    ctx.Fail("The first song's screen didn't come within 30 s");
                    yield break;
                }
                if (deckShown > 0.01f)
                    ctx.Fail("The card draw showed (deck or title at alpha " + deckShown + ")");

                // Every place, the host place, Reset Lineup and every idol on the bench is a button a click reaches
                Transform panel = popup.NextSong_Container.transform.Find("ConcertHype Lineup");
                List<Button> buttons = panel.GetComponentsInChildren<Button>().Concat(popup.Girls.GetComponentsInChildren<Button>()).ToList();
                ctx.Record("buttons", buttons.Count);
                if (panel.Find("Host/Center") == null || !panel.Find("Host/Center").gameObject.activeInHierarchy)
                    ctx.Fail("The talk break's host place isn't shown");
                Rect screen = new(0, 0, Screen.width, Screen.height);
                foreach (Button button in buttons)
                {
                    Rect rect = ScreenRect(button.transform);
                    if (!screen.Contains(rect.min) || !screen.Contains(rect.max))
                        ctx.Fail(button.name + " is partly off screen: " + rect);
                    GameObject hit = TopHit(rect.center);
                    if (hit == null || !hit.transform.IsChildOf(button.transform))
                        ctx.Fail("A click on " + button.transform.parent.name + "/" + button.name + " lands on " + (hit == null ? "nothing" : hit.name));
                }

                object state = Get<object>(session, "State");
                object show = Get<object>(session, "Show");
                IList<data_girls.girls> girls = Get<IList<data_girls.girls>>(show, "Girls");
                IList<double> history = Get<IList<double>>(state, "History");
                double songSeconds = Get<double>(Get<object>(state, "Config"), "SongSeconds");
                int[] Lineup() => Get<int[]>(session, "Lineup");
                bool Available(int m) => (bool)AccessTools.Method(state.GetType(), "IsAvailable").Invoke(state, new object[] { m });
                Transform Bench(int m) => popup.Girls.transform.Find("Bench " + m + "/Center");
                DragAndDropManager manager = Game.Main.Data.GetComponent<DragAndDropManager>();

                // A real drag of a bench idol onto the center: the game's floating portrait follows the mouse, the
                // center finds the hover as the game's senbatsu places do, and the drop puts her there
                IList songsInShow = Get<IList>(Get<object>(show, "Setup"), "Songs");
                HashSet<int> originals = new(Get<IList>(songsInShow[0], "Slots").Cast<object>().Select(s => Get<int>(s, "Original")));
                int sub = Enumerable.Range(0, girls.Count).Where(m => !Lineup().Contains(m) && !originals.Contains(m) && Available(m)).DefaultIfEmpty(-1).First();
                if (sub < 0)
                {
                    ctx.Fail("The fixture's first song has nobody on the bench to bring in");
                    yield break;
                }
                Transform center = panel.Find("Slot 0/Center");
                bool portraitShown = false, hovered = false;
                yield return mouse.Drag(ScreenRect(Bench(sub)).center, ScreenRect(center).center, () =>
                {
                    portraitShown = DragAndDropManager.dragging && ReferenceEquals(manager.dragging_girl, girls[sub])
                        && UnityEngine.Object.FindObjectOfType<DragAndDropIconGirl>() != null;
                    hovered = center.GetComponent<ButtonDefault>().hovering;
                });
                if (!portraitShown)
                    ctx.Fail("The game's floating portrait didn't follow the drag");
                if (!hovered)
                    ctx.Fail("The center didn't take the hover during the drag");
                if (DragAndDropManager.dragging || UnityEngine.Object.FindObjectOfType<DragAndDropIconGirl>() != null)
                    ctx.Fail("The game's floating portrait stayed after the drop");
                if (Lineup()[0] != sub || Bench(sub).gameObject.activeInHierarchy || !ReferenceEquals(center.GetComponent<GirlProfileOnHover>().girl, girls[sub]))
                    ctx.Fail($"The dragged idol isn't in the center: lineup {string.Join(",", Lineup())}, still on the bench: {Bench(sub).gameObject.activeInHierarchy}");
                mouse.Off();

                // Every icon on screen has its picture, and the mouse over one shows its meaning in the game's tooltip
                List<Image> icons = panel.GetComponentsInChildren<Image>().Concat(popup.Girls.GetComponentsInChildren<Image>()).Where(i => i.name == "Icon").ToList();
                ctx.Record("icons", icons.Count);
                foreach (Image icon in icons.Where(i => i.sprite == null || !i.sprite.name.StartsWith("ConcertHype")).Take(3))
                    ctx.Fail("An icon has no picture: " + icon.transform.parent.name);
                object view = AccessTools.Method(mod.GetType("ConcertHype.PanelView", true), "Build").Invoke(null, new object[] { session, null, null });
                string tip = Get<string>(Get<IList>(view, "Slots")[1], "Tip");
                yield return mouse.Hover(ScreenRect(panel.Find("Slot 1/Icon")).center, 0.5f);
                string shown = AccessTools.Field(typeof(TooltipManager), "TooltipText").GetValue(Game.Main.Data.GetComponent<TooltipManager>()) as string;
                mouse.Off();
                if (shown != tip)
                    ctx.Fail($"Over the second place's icon the tooltip reads '{shown}', not '{tip}'");

                // Each song: the popup's tween ends exactly on the show's hype, with no crisis popup. Before the
                // second song, a talk break hosted by a bench idol dragged onto the host place.
                int songs = concert.SetListItems.Count;
                for (int song = 0; song < songs; song++)
                {
                    if (song == 1)
                    {
                        int host = Enumerable.Range(0, girls.Count).Where(m => !Lineup().Contains(m) && Available(m)).DefaultIfEmpty(-1).First();
                        if (host < 0)
                        {
                            ctx.Fail("Nobody on the bench to host a talk break");
                            yield break;
                        }
                        // Her profile card shows at her bench portrait first, then comes along to the host place
                        Transform hostPlace = panel.Find("Host/Center");
                        yield return mouse.Hover(ScreenRect(Bench(host)).center, 1f);
                        yield return mouse.Drag(ScreenRect(Bench(host)).center, ScreenRect(hostPlace).center, null);
                        yield return new WaitForSecondsRealtime(1f);
                        TooltipStatusButton card = UnityEngine.Object.FindObjectOfType<TooltipStatusButton>();
                        // Where the game places the card next to a portrait (TooltipStatusButton.SetPosition)
                        Vector2 expected = new(hostPlace.position.x + 0.45f, Mathf.Max(hostPlace.position.y + 0.37f, -3f));
                        if (expected.x > 6f)
                            expected.x -= 4f;
                        ctx.Record("profile card", card == null ? "not shown" : card.transform.position.ToString("F2") + ", expected " + expected.ToString("F2"));
                        if (card == null || Vector2.Distance(card.transform.position, expected) > 0.01f)
                            ctx.Fail("The host's profile card isn't at the host place");
                        mouse.Off();
                        if (Get<int>(session, "Host") != host)
                        {
                            ctx.Fail("The drag onto the host place didn't place her");
                            yield break;
                        }

                        Click(TopHit(ScreenRect(popup.NextSong_Button.transform).center));
                        SEvent_Concerts._concert._mc talk = concert.SetListItems[1] as SEvent_Concerts._concert._mc;
                        if (talk == null || !ReferenceEquals(popup.CurrentSong, talk))
                        {
                            ctx.Fail("Start didn't play a talk break item: the popup is on " + popup.CurrentSong?.GetTitle());
                            yield break;
                        }
                        ctx.Record("talk break", talk.GetTitle());
                        if (popup.SongTitle.GetComponent<TextMeshProUGUI>()?.text != talk.GetTitle() || !talk.GetTitle().EndsWith(" #1"))
                            ctx.Fail("The popup doesn't show the talk break's title: " + popup.SongTitle.GetComponent<TextMeshProUGUI>()?.text);
                        float talkLength = -1f;
                        yield return PlayOut(popup, false, length => talkLength = length);
                        ctx.Record("talk break seconds", talkLength);
                        if (Math.Abs(talkLength - songSeconds) > 0.5)
                            ctx.Fail($"The talk break played {talkLength} s, not about {songSeconds}");
                        object lastMc = Get<object>(session, "LastMc");
                        if (!NextSongShown(popup) || lastMc == null || Math.Abs(concert.Hype - Get<double>(lastMc, "HypeAfter")) > 0.01)
                            ctx.Fail($"After the talk break the bar is at {concert.Hype}, the show at {(lastMc == null ? "nothing" : Get<double>(lastMc, "HypeAfter").ToString())}");
                        if (concert.SetListItems.Count != songs + 1 || popup.NextSong_Title.GetComponent<TextMeshProUGUI>()?.text != concert.SetListItems[2].GetTitle())
                            ctx.Fail("After the talk break the next song isn't the second song: " + popup.NextSong_Title.GetComponent<TextMeshProUGUI>()?.text);
                    }

                    Click(TopHit(ScreenRect(popup.NextSong_Button.transform).center));
                    float songLength = -1f;
                    bool crisis = false;
                    yield return PlayOut(popup, song == songs - 1, length => songLength = length, () => crisis = true);
                    if (crisis)
                    {
                        ctx.Fail("A crisis popup opened during song " + (song + 1));
                        yield break;
                    }
                    if (history.Count <= song)
                    {
                        ctx.Fail("Song " + (song + 1) + " didn't play within 30 s");
                        yield break;
                    }
                    if (song == 0)
                    {
                        ctx.Record("song seconds", songLength);
                        if (Math.Abs(songLength - songSeconds) > 0.5)
                            ctx.Fail($"Song 1 played {songLength} s, not about {songSeconds}");
                    }
                    if (song < songs - 1 && Math.Abs(concert.Hype - history[song]) > 0.01)
                        ctx.Fail($"After song {song + 1} the bar is at {concert.Hype}, the show at {history[song]}");
                }
                yield return new WaitForSeconds(2f);

                object score = AccessTools.Property(sessionType, "LastScore").GetValue(null, null);
                double total = Get<double>(score, "Total");
                ctx.Record("score", total);
                Transform scoreLine = popup.Final_Container.transform.Find("ConcertHype Score");
                if (scoreLine == null || !scoreLine.gameObject.activeInHierarchy || !ScreenRectInside(scoreLine))
                    ctx.Fail("The results screen doesn't show the score on screen");
                if (Session() != null)
                    ctx.Fail("The show didn't end with the concert");

                // Closing the results with a real click leaves the concert's hype exactly on the score
                Transform finish = popup.Final_Container.transform.Find("Finish");
                Click(TopHit(ScreenRect(finish).center));
                yield return new WaitForSeconds(1f);
                ctx.Record("hype after Finish", concert.Hype);
                if (Math.Abs(concert.Hype - total) > 0.001 || concert.ProjectedValues.Actual_Hype != Mathf.RoundToInt((float)total))
                    ctx.Fail($"After Finish the concert's hype is {concert.Hype} (recorded {concert.ProjectedValues.Actual_Hype}), the score {total}");
                foreach (string line in modLog.Distinct().Take(5))
                    ctx.Fail("The mod logged: " + line);
            }
            yield return Game.CloseAllPopups(ctx);
        }

        private static bool NextSongShown(Concert_Popup popup) =>
            popup.NextSong_Container.activeSelf && popup.NextSong_Container.GetComponent<CanvasGroup>().alpha > 0.99f;

        /// <summary>
        /// Waits while the popup plays its item, until the next item's screen (or the results) is up, and gives the
        /// game seconds its progress bar ran for.
        /// </summary>
        private static IEnumerator PlayOut(Concert_Popup popup, bool last, Action<float> length, Action crisis = null)
        {
            // Start sets the bar to 0 before this runs; it fills over the item's length, and the next screen fades in after
            float start = Time.realtimeSinceStartup, barStart = -1f, barEnd = -1f;
            while (Time.realtimeSinceStartup - start < 30f && !(barEnd >= 0 && (last ? popup.Final_Container.activeSelf : NextSongShown(popup))))
            {
                float x = popup.SongProgressBar.transform.localScale.x;
                if (barStart < 0 && x > 0.001f)
                    barStart = Time.time;
                if (barStart >= 0 && barEnd < 0 && x >= 0.999f)
                    barEnd = Time.time;
                if (popup.CrisisPopup.activeSelf)
                {
                    crisis?.Invoke();
                    yield break;
                }
                yield return null;
            }
            length(barStart >= 0 && barEnd >= 0 ? barEnd - barStart : -1f);
        }

        /// <summary>
        /// The runner's virtual mouse, which the game's input reads while input is blocked.
        /// </summary>
        private sealed class VirtualMouse
        {
            private readonly FieldInfo position = AccessTools.Field(AccessTools.TypeByName("InGameTests.InputBlock_MousePosition"), "Virtual");
            private readonly Type buttons = AccessTools.TypeByName("InGameTests.InputBlock_Buttons");
            private bool held;

            public bool Works => position != null && buttons != null && AccessTools.Method(buttons, "SetLeftButton") != null
                && AccessTools.Property(buttons, "CanInject")?.GetValue(null, null) is bool can && can;

            private void Left(bool down)
            {
                held = down;
                AccessTools.Method(buttons, "SetLeftButton").Invoke(null, new object[] { down });
            }

            private void MoveTo(Vector2 point) => position.SetValue(null, new Vector3(point.x, point.y, 0f));

            /// <summary>
            /// Lets go if still held, and takes the mouse off screen.
            /// </summary>
            public void Off()
            {
                if (held)
                    Left(false);
                position.SetValue(null, null);
            }

            /// <summary>
            /// Rests the mouse on a point, without pressing.
            /// </summary>
            public IEnumerator Hover(Vector2 at, float seconds)
            {
                MoveTo(at);
                yield return new WaitForSecondsRealtime(seconds);
            }

            /// <summary>
            /// Presses, holds past the game's drag delay, moves to the target in small steps and lets go there.
            /// </summary>
            public IEnumerator Drag(Vector2 from, Vector2 to, Action beforeRelease)
            {
                MoveTo(from);
                yield return null;
                yield return null;
                Left(true);
                yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
                for (int i = 1; i <= 12; i++)
                {
                    MoveTo(Vector2.Lerp(from, to, i / 12f));
                    yield return new WaitForSecondsRealtime(0.04f);
                }
                yield return new WaitForSecondsRealtime(0.2f);
                beforeRelease?.Invoke();
                Left(false);
                // The game's own drop ends its floating portrait a frame after the mod's
                for (int i = 0; i < 4; i++)
                    yield return null;
            }
        }

        private static bool ScreenRectInside(Transform t)
        {
            Rect rect = ScreenRect(t);
            return rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= Screen.width && rect.yMax <= Screen.height;
        }

        private static Rect ScreenRect(Transform t)
        {
            Vector3[] c = new Vector3[4];
            ((RectTransform)t).GetWorldCorners(c);
            Camera cam = t.GetComponentInParent<Canvas>()?.rootCanvas.worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        private static GameObject TopHit(Vector2 point)
        {
            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        private static void Click(GameObject target)
        {
            if (target != null)
                ExecuteEvents.ExecuteHierarchy(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }
    }
}
