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
    /// the icons' pictures and the game's tooltip over one and over the Expected Hype info icon, the readouts as TMP
    /// draws them clear of the places, the empty host place showing the game's empty senbatsu place, the game's
    /// profile card placed at the host place, the
    /// game's cancel icon over the hovered host and a real click cancelling her, a
    /// talk break played by the popup's own coroutine, the transpiled song length, the popup's tweens landing hype
    /// on the show's value, the stage's copies of the marketing wheel and the rolls they land on during a song,
    /// the score line on the real results screen, and no crisis popup.
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
                Single_Marketing_Roll wheel = PopupManager.GetObject(PopupManager._type.single_release).GetComponent<Single_Release>()
                    .Marketing_Roll_Container.GetComponentInChildren<Single_Marketing_Roll>(true);
                VanillaShapes.AssertCovers(ctx, shapes, "Wheel", wheel.Spinner_Container.transform.parent.gameObject, "marketing wheel");
                VanillaShapes.AssertCovers(ctx, shapes, "Pointer", wheel.Spinner_Icon, "marketing wheel pointer");
                VanillaShapes.AssertCovers(ctx, shapes, "Tab", PopupManager.GetObject(PopupManager._type.group_appeal).GetComponent<Group_Appeal_Popup>().Tab_TotalFans, "group appeal tab");
                VanillaShapes.AssertCovers(ctx, shapes, "Scrollbar", PopupManager.GetObject(PopupManager._type.cafe).GetComponent<Cafe_Popup>().SelectGirl_ScrollBar, "café idol list scrollbar");
                Transform settingsScreen = PopupManager.GetObject(PopupManager._type.main_menu_settings).transform.Find("Panel").Cast<Transform>().First(t => t.name.Contains("Screen"));
                VanillaShapes.AssertCovers(ctx, shapes, "Dropdown", settingsScreen.Find("Quality/Dropdown").gameObject, "settings Quality dropdown");

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

                // Every place, both tabs, Reset Lineup and every idol on the bench is a button a click reaches; before
                // the first song the talk break's tab is disabled and its row hidden
                Transform panel = popup.NextSong_Container.transform.Find("ConcertHype Lineup");
                // A closed dropdown's list is there but invisible, and clicks go through it
                List<Button> buttons = panel.GetComponentsInChildren<Button>().Concat(popup.Girls.GetComponentsInChildren<Button>())
                    .Where(b => b.GetComponentsInParent<CanvasGroup>().All(g => g.alpha > 0.01f)).ToList();
                ctx.Record("buttons", buttons.Count);
                if (panel.Find("TalkTab").GetComponent<Button>().interactable || panel.Find("TalkBreak/Host 0/Center").gameObject.activeInHierarchy)
                    ctx.Fail("The talk break's tab works, or its row shows, before the first song");
                // Its reason shows in the game's tooltip while it's hovered
                string talkTip = Get<string>(AccessTools.Method(mod.GetType("ConcertHype.PanelView", true), "Build")
                    .Invoke(null, new object[] { session, null, null, Enum.ToObject(mod.GetType("ConcertHype.FormationTab", true), 0), -1 }), "TalkTip");
                yield return mouse.Hover(ScreenRect(panel.Find("TalkTab")).center, 0.5f);
                string talkShown = AccessTools.Field(typeof(TooltipManager), "TooltipText").GetValue(Game.Main.Data.GetComponent<TooltipManager>()) as string;
                mouse.Off();
                if (talkTip.Length == 0 || talkShown != talkTip)
                    ctx.Fail($"Over the disabled talk break tab the tooltip reads '{talkShown}', not '{talkTip}'");
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
                object showConfig = Get<object>(state, "Config");
                double songSeconds = Get<double>(showConfig, "SongSeconds");
                // No accidents: the rolls are unit-tested, and random injuries could leave nobody to bring in
                foreach (string chance in new[] { "PBase", "PRoll", "PTired" })
                    AccessTools.Field(showConfig.GetType(), chance).SetValue(showConfig, 0.0);
                int[] Lineup() => Get<int[]>(session, "Lineup");
                bool Available(int m) => (bool)AccessTools.Method(state.GetType(), "IsAvailable").Invoke(state, new object[] { m });
                Transform Bench(int m) => popup.Girls.transform.Find("Bench Scroll/Content/Bench " + m + "/Center");
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
                GameObject underBench = TopHit(ScreenRect(Bench(sub)).center), underCenter = TopHit(ScreenRect(center).center);
                ctx.Record("under the mouse before the first drag", (underBench == null ? "nothing" : underBench.transform.parent?.name + "/" + underBench.name)
                    + " -> " + (underCenter == null ? "nothing" : underCenter.transform.parent?.name + "/" + underCenter.name));
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
                object songTab = Enum.ToObject(mod.GetType("ConcertHype.FormationTab", true), 0);
                object view = AccessTools.Method(mod.GetType("ConcertHype.PanelView", true), "Build").Invoke(null, new object[] { session, null, null, songTab, -1 });
                string tip = Get<string>(Get<IList>(view, "Slots")[1], "Tip");
                yield return mouse.Hover(ScreenRect(panel.Find("Slot 1/Icon")).center, 0.5f);
                string shown = AccessTools.Field(typeof(TooltipManager), "TooltipText").GetValue(Game.Main.Data.GetComponent<TooltipManager>()) as string;
                mouse.Off();
                if (shown != tip)
                    ctx.Fail($"Over the second place's icon the tooltip reads '{shown}', not '{tip}'");
                // The game's info icon before Expected Hype shows the range and mood the same way
                string expectedTip = Get<string>(Get<object>(view, "Expected"), "Tip");
                yield return mouse.Hover(ScreenRect(panel.Find("Expected/Info")).center, 0.5f);
                shown = AccessTools.Field(typeof(TooltipManager), "TooltipText").GetValue(Game.Main.Data.GetComponent<TooltipManager>()) as string;
                mouse.Off();
                if (shown != expectedTip)
                    ctx.Fail($"Over the Expected Hype info icon the tooltip reads '{shown}', not '{expectedTip}'");

                // The group dropdown, with real clicks: the fixture's sister group leaves only its idols on the bench,
                // and All Groups brings everyone back
                Transform groupBox = popup.Girls.transform.Find("Bench Group");
                if (groupBox == null)
                {
                    ctx.Fail("No group dropdown, though the fixture has a sister group");
                }
                else
                {
                    var dropdown = groupBox.GetComponent<Michsky.UI.ModernUIPack.CustomDropdown>();
                    PropertyInfo benchGroup = AccessTools.Property(mod.GetType("ConcertHype.BenchColumn", true), "Group");
                    IList members = Get<IList>(Get<object>(state, "Setup"), "Members");
                    List<int> ShownBench() => popup.Girls.transform.Find("Bench Scroll/Content").Cast<Transform>().Where(t => t.gameObject.activeSelf)
                        .Select(t => int.Parse(t.name.Substring("Bench ".Length))).ToList();
                    int everyone = ShownBench().Count;
                    yield return mouse.Click(ScreenRect(groupBox.Find("Content/Main")).center);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Transform lastItem = dropdown.itemParent.GetChild(dropdown.itemParent.childCount - 1);
                    yield return mouse.Click(ScreenRect(lastItem).center);
                    yield return new WaitForSecondsRealtime(0.6f);
                    int group = (int)benchGroup.GetValue(null, null);
                    List<int> filtered = ShownBench();
                    ctx.Record("bench of the last group", group + ": " + filtered.Count + " of " + everyone);
                    if (group < 0 || filtered.Count == 0 || filtered.Any(m => Get<int>(members[m], "Group") != group))
                        ctx.Fail($"Choosing the last group didn't leave only its idols on the bench: group {group}, bench {string.Join(",", filtered)}");
                    yield return mouse.Click(ScreenRect(groupBox.Find("Content/Main")).center);
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return mouse.Click(ScreenRect(dropdown.itemParent.GetChild(0)).center);
                    yield return new WaitForSecondsRealtime(0.6f);
                    mouse.Off();
                    if ((int)benchGroup.GetValue(null, null) != -1 || ShownBench().Count != everyone)
                        ctx.Fail("All Groups didn't bring the whole bench back");
                }

                // The Backstage title, as TMP draws it, stays over the bench's panel, above the dropdown (or the panel)
                Transform benchTitle = popup.Girls.transform.Find("Bench Title"), benchPanel = popup.Girls.transform.Find("Bench Panel");
                if (benchTitle == null || benchPanel == null)
                {
                    ctx.Fail("The bench has no Backstage title or no panel");
                }
                else
                {
                    Rect drawnTitle = DrawnRect(benchTitle.GetComponent<TextMeshProUGUI>()), panelRect = WorldRect(benchPanel);
                    Rect under = groupBox != null ? WorldRect(groupBox.Find("Content/Main")) : panelRect;
                    ctx.Record("backstage title", drawnTitle.ToString("F2") + " over " + under.ToString("F2"));
                    if (drawnTitle.xMin < panelRect.xMin - 0.01f || drawnTitle.xMax > panelRect.xMax + 0.01f || drawnTitle.yMin < under.yMax - 0.01f)
                        ctx.Fail("The Backstage title runs past the bench's panel or into what's under it");
                }

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
                        Transform hostPlace = panel.Find("TalkBreak/Host 0/Center");
                        // The empty host place is the game's empty senbatsu place, found among the scene's objects
                        Image emptyPicture = hostPlace.GetComponent<Image>(), gamePlace = Resources.FindObjectsOfTypeAll<SenbatsuReciever>()
                            .Select(r => r.GetComponent<Image>()).FirstOrDefault(i => i != null && i.sprite != null);
                        ctx.Record("empty host place", emptyPicture.sprite?.name + " " + emptyPicture.type);
                        if (gamePlace == null || emptyPicture.sprite != gamePlace.sprite || emptyPicture.type != gamePlace.type)
                            ctx.Fail("The empty host place doesn't show the game's empty senbatsu place: " + emptyPicture.sprite?.name);
                        // Every readout, as TMP draws it, stays clear of the places, the tabs and the talk break's row
                        // (hidden on this tab), and of the other readouts (a readout's own title and value are as close
                        // as the game's, whose text boxes touch)
                        List<Rect> places = panel.Cast<Transform>().Where(t => t.name.StartsWith("Slot ") && t.gameObject.activeSelf)
                            .Concat(new[] { panel.Find("SongTab"), panel.Find("TalkTab") })
                            .Concat(panel.Find("TalkBreak").Cast<Transform>().Where(t => t.name.StartsWith("Host "))).Select(WorldRect).ToList();
                        Dictionary<string, Rect> texts = new[] { "Expected", "Changed", "Last" }.SelectMany(r => new[] { r + "/Title", r + "/Value" })
                            .ToDictionary(path => path, path => DrawnRect(panel.Find(path).GetComponent<TextMeshProUGUI>()));
                        foreach (KeyValuePair<string, Rect> text in texts)
                        {
                            List<string> hits = places.Where(p => p.Overlaps(text.Value)).Select(p => "place " + p.ToString("F2"))
                                .Concat(texts.Where(other => Readout(other.Key) != Readout(text.Key) && other.Value.Overlaps(text.Value)).Select(other => other.Key + " " + other.Value.ToString("F2"))).ToList();
                            if (hits.Count > 0)
                                ctx.Fail("The readout text " + text.Key + " " + text.Value.ToString("F2") + " overlaps " + string.Join(", ", hits.ToArray()));
                        }
                        // A real click on the talk break's tab shows its row
                        yield return mouse.Click(ScreenRect(panel.Find("TalkTab")).center);
                        if (!hostPlace.gameObject.activeInHierarchy)
                        {
                            ctx.Fail("A click on the talk break's tab didn't show its row");
                            yield break;
                        }
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
                        if (Get<int[]>(session, "Hosts")[0] != host)
                        {
                            ctx.Fail("The drag onto the host place didn't place her");
                            yield break;
                        }
                        // Over the host, the game's cancel icon; a quick click cancels, and she's placed again
                        Transform cancel = hostPlace.Find("Cancel");
                        bool cancelShown = cancel != null && cancel.gameObject.activeInHierarchy && cancel.GetComponent<Image>().sprite != null;
                        ctx.Record("cancel icon over the host", cancel == null ? "missing" : cancelShown + " " + cancel.GetComponent<Image>().sprite?.name);
                        if (!cancelShown)
                            ctx.Fail("Hovering the host doesn't show the cancel icon");
                        yield return mouse.Click(ScreenRect(hostPlace).center);
                        if (Get<int[]>(session, "Hosts")[0] != -1)
                            ctx.Fail("A click on the host didn't take her off");
                        yield return mouse.Drag(ScreenRect(Bench(host)).center, ScreenRect(hostPlace).center, null);
                        mouse.Off();
                        if (Get<int[]>(session, "Hosts")[0] != host)
                        {
                            ctx.Fail("The second drag onto the host place didn't place her");
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
                    bool stageChecked = false;
                    Action<float> stageCheck = song != 0 ? null : progress =>
                    {
                        if (stageChecked || progress < 0.95f)
                            return;
                        stageChecked = true;
                        CheckStage(ctx, popup, Get<object>(session, "Pending"));
                    };
                    yield return PlayOut(popup, song == songs - 1, length => songLength = length, () => crisis = true, stageCheck);
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
                        if (!stageChecked)
                            ctx.Fail("Song 1 ended before the stage was checked");
                        Transform stageAfter = popup.transform.Find("ConcertHype Stage");
                        if (stageAfter != null && stageAfter.gameObject.activeInHierarchy)
                            ctx.Fail("The stage still shows after song 1");
                        float benchAfter = BenchAlphas(popup).DefaultIfEmpty(1f).Min();
                        if (benchAfter < 0.99f)
                            ctx.Fail("The bench is still faded after song 1 (alpha " + benchAfter + ")");
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

        /// <summary>
        /// The fade of each bench portrait shown: each one's own group, not the game's column or the labels in it.
        /// </summary>
        private static IEnumerable<float> BenchAlphas(Concert_Popup popup) =>
            popup.Girls.transform.Find("Bench Scroll/Content").Cast<Transform>().Where(t => t.gameObject.activeSelf).Select(t => t.GetComponent<CanvasGroup>()).Where(g => g != null).Select(g => g.alpha);

        private static bool NextSongShown(Concert_Popup popup) =>
            popup.NextSong_Container.activeSelf && popup.NextSong_Container.GetComponent<CanvasGroup>().alpha > 0.99f;

        /// <summary>
        /// Waits while the popup plays its item, until the next item's screen (or the results) is up, and gives the
        /// game seconds its progress bar ran for.
        /// </summary>
        /// <param name="playing">Called each frame of the item with its progress bar's fill.</param>
        private static IEnumerator PlayOut(Concert_Popup popup, bool last, Action<float> length, Action crisis = null, Action<float> playing = null)
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
                if (barStart >= 0 && barEnd < 0)
                    playing?.Invoke(x);
                yield return null;
            }
            length(barStart >= 0 && barEnd >= 0 ? barEnd - barStart : -1f);
        }

        /// <summary>
        /// Near the end of a song, every filled place has its copy of the marketing wheel with its four rings
        /// drawn from the game's sprites, and her roll shows in the game's heavy font on its own material, landed by
        /// the stage's own clock. As drawn, the
        /// places are on screen, apart from each other and clear of the song's title and progress bar, and the
        /// bench is faded out.
        /// </summary>
        private static void CheckStage(TestContext ctx, Concert_Popup popup, object pending)
        {
            string fontName = (string)AccessTools.Field(pending.GetType().Assembly.GetType("ConcertHype.StageRolls", true), "NumberFontName").GetValue(null);
            int filled = ((Array)AccessTools.Field(pending.GetType(), "Slots").GetValue(pending)).Cast<object>()
                .Count(slot => (int)AccessTools.Field(slot.GetType(), "Member").GetValue(slot) >= 0);
            Transform stage = popup.transform.Find("ConcertHype Stage");
            if (stage == null || !stage.gameObject.activeInHierarchy)
            {
                ctx.Fail("Song 1 plays without the stage");
                return;
            }
            List<Transform> places = stage.Cast<Transform>().Where(place => place.gameObject.activeSelf && place.Find("Wheel") is Transform wheel && wheel.gameObject.activeSelf).ToList();
            ctx.Record("wheels on stage", places.Count);
            if (places.Count != filled)
                ctx.Fail($"{places.Count} wheels on stage for {filled} idols");
            foreach (Transform place in places)
            {
                Image[] rings = place.Find("Wheel/Spinny_Part").GetComponentsInChildren<Image>();
                if (rings.Length != 4 || rings.Any(ring => ring.sprite == null))
                    ctx.Fail(place.name + "'s wheel doesn't have the game's four rings");
                TextMeshProUGUI number = place.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (number == null || !number.gameObject.activeInHierarchy || number.text.Length == 0)
                {
                    ctx.Fail(place.name + " shows no roll by the end of the song");
                    continue;
                }
                // The game's heavy font, on the number's own material, so the game's other text keeps its look
                if (number.font == null || number.font.name != fontName)
                    ctx.Fail(place.name + "'s roll is drawn in " + (number.font == null ? "no font" : number.font.name) + ", not " + fontName);
                else if (number.fontSharedMaterial == number.font.material)
                    ctx.Fail(place.name + "'s roll styles the font's shared material");
            }

            // Each place as drawn: her picture and her number
            List<Rect> drawn = places.Select(place =>
            {
                Rect picture = ScreenRect(place.Find("Center")), number = ScreenRect(place.Find("Label"));
                return Rect.MinMaxRect(Mathf.Min(picture.xMin, number.xMin), Mathf.Min(picture.yMin, number.yMin),
                    Mathf.Max(picture.xMax, number.xMax), Mathf.Max(picture.yMax, number.yMax));
            }).ToList();
            Rect screen = new(0, 0, Screen.width, Screen.height);
            Rect title = ScreenRect(popup.SongTitle.transform), progress = ScreenRect(popup.SongProgress.transform);
            for (int a = 0; a < drawn.Count; a++)
            {
                if (!screen.Contains(drawn[a].min) || !screen.Contains(drawn[a].max))
                    ctx.Fail(places[a].name + " is partly off screen: " + drawn[a]);
                if (drawn[a].Overlaps(title) || drawn[a].Overlaps(progress))
                    ctx.Fail(places[a].name + " covers the song's title or progress bar");
                for (int b = a + 1; b < drawn.Count; b++)
                {
                    if (drawn[a].Overlaps(drawn[b]))
                        ctx.Fail(places[a].name + " overlaps " + places[b].name);
                }
            }
            float bench = BenchAlphas(popup).DefaultIfEmpty(0f).Max();
            ctx.Record("bench alpha during the song", bench);
            if (bench > 0.05f)
                ctx.Fail("The bench shows during the song (alpha " + bench + ")");
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
            /// A quick click, let go well before the game's drag delay.
            /// </summary>
            public IEnumerator Click(Vector2 at)
            {
                MoveTo(at);
                yield return null;
                yield return null;
                Left(true);
                yield return null;
                Left(false);
                for (int i = 0; i < 3; i++)
                    yield return null;
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

        /// <summary>
        /// The readout a text belongs to: "Expected" for "Expected/Title".
        /// </summary>
        private static string Readout(string path) => path.Substring(0, path.IndexOf('/'));

        private static Rect WorldRect(Transform t)
        {
            Vector3[] c = new Vector3[4];
            ((RectTransform)t).GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>
        /// Where the text's glyphs are, as TMP lays them out, in world space.
        /// </summary>
        private static Rect DrawnRect(TextMeshProUGUI text)
        {
            text.ForceMeshUpdate();
            Vector3 a = text.transform.TransformPoint(text.textBounds.min), b = text.transform.TransformPoint(text.textBounds.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
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
