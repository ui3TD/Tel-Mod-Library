using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Concert Hype's show in the game's real concert popup. The unit tests cover the rules, the panel's text, the
    /// payout and each patch; this checks what only the engine does: real clicks reaching the panel's and the
    /// cast's buttons on screen, the popup's coroutines and tweens landing hype on the show's value after each
    /// song, the score line on the real results screen, and no crisis popup.
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

            Type sessionType = mod.GetType("ConcertHype.ShowSession", true);
            Type panelType = mod.GetType("ConcertHype.LineupPanel", true);
            object Session() => AccessTools.Property(sessionType, "Current").GetValue(null, null);

            SEvent_Concerts concerts = Game.Main.Data.GetComponent<SEvent_Concerts>();
            SEvent_Concerts._concert concert = concerts.Concert;
            if (concert == null || concert.SetListItems.Count == 0 || concert.SetListItems.Any(i => i.isMC()))
            {
                ctx.Fail("The fixture's current concert should have a setlist of songs only");
                yield break;
            }

            float timeScale = Time.timeScale;
            using (Game.Variable("ConcertHype_Enabled", "1"))
            using (TestTools.Restore(() => Time.timeScale = timeScale))
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

                float start = Time.realtimeSinceStartup;
                while (!popup.DrawCards.activeInHierarchy && Time.realtimeSinceStartup - start < 30f)
                    yield return null;
                yield return new WaitForSeconds(3f);
                popup.DrawCards.GetComponent<Concert_DrawCards>().OnClick();
                yield return WaitForNextSongScreen(popup);

                // Every place in the lineup and every idol in the cast column is a button a click reaches
                Transform panel = popup.NextSong_Container.transform.Find("ConcertHype Lineup");
                List<Button> buttons = panel.GetComponentsInChildren<Button>().Concat(popup.Girls.GetComponentsInChildren<Button>()).ToList();
                ctx.Record("buttons", buttons.Count);
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

                // A real click on the first place selects it
                Transform firstPlace = panel.Find("Slot 0/Center");
                Click(TopHit(ScreenRect(firstPlace).center));
                ctx.Record("selected after click", AccessTools.Field(panelType, "SelectedSlot").GetValue(null));
                if ((int)AccessTools.Field(panelType, "SelectedSlot").GetValue(null) != 0)
                    ctx.Fail("Clicking the first place didn't select it");
                Click(TopHit(ScreenRect(firstPlace).center));

                // Each song: the popup's tween ends exactly on the show's hype, with no crisis popup
                object state = AccessTools.Field(sessionType, "State").GetValue(session);
                IList<double> history = (IList<double>)AccessTools.Field(state.GetType(), "History").GetValue(state);
                int songs = concert.SetListItems.Count;
                for (int song = 0; song < songs; song++)
                {
                    Click(TopHit(ScreenRect(popup.NextSong_Button.transform).center));
                    float songStart = Time.realtimeSinceStartup;
                    // The next-song screen goes while the song plays, then comes back (or the results screen comes)
                    while (popup.NextSong_Container.activeSelf && Time.realtimeSinceStartup - songStart < 30f)
                        yield return null;
                    while (Time.realtimeSinceStartup - songStart < 30f
                        && !(history.Count > song && (song < songs - 1
                            ? popup.NextSong_Container.activeSelf && popup.NextSong_Container.GetComponent<CanvasGroup>().alpha > 0.99f
                            : popup.Final_Container.activeSelf)))
                    {
                        if (popup.CrisisPopup.activeSelf)
                        {
                            ctx.Fail("A crisis popup opened during song " + (song + 1));
                            yield break;
                        }
                        yield return null;
                    }
                    if (history.Count <= song)
                    {
                        ctx.Fail("Song " + (song + 1) + " didn't play within 30 s");
                        yield break;
                    }
                    // The game's hype animation can take its last step a frame after the song ends
                    if (song < songs - 1 && Math.Abs(concert.Hype - history[song]) > 0.1)
                        ctx.Fail($"After song {song + 1} the bar is at {concert.Hype}, the show at {history[song]}");
                }
                yield return new WaitForSeconds(2f);

                object score = AccessTools.Property(sessionType, "LastScore").GetValue(null, null);
                double total = (double)AccessTools.Field(score.GetType(), "Total").GetValue(score);
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
            }
            yield return Game.CloseAllPopups(ctx);
        }

        private static IEnumerator WaitForNextSongScreen(Concert_Popup popup)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 30f && !(popup.NextSong_Container.activeSelf && popup.NextSong_Container.GetComponent<CanvasGroup>().alpha > 0.99f))
                yield return null;
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
