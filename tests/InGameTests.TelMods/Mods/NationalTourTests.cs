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
    /// National Tour adds a tab to the Special Events popup and pins to the new tour map, copied from the game's
    /// own objects, found by object path. The unit tests stub Instantiate, Find, the click events and drawing.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class NationalTourTests
    {
        private const string HarmonyId = "com.tel.nationaltour";
        private const string Pictures = "NationalTour.Pictures";
        private const string NationalTabName = "National Tour";

        /// <summary>The three pictures loaded from the mod's folder when the game scene started.</summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator PicturesLoaded(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type pictures = mod.GetType(Pictures, true);
            foreach (string name in new[] { "TOUR_map_2", "World_tour_def", "World_tour_def_BG" })
                AssertPicture(ctx, name, Traverse.Create(pictures).Field(name).GetValue<Sprite>()?.texture);
            AssertPicture(ctx, "World_tour_def_tex", Traverse.Create(pictures).Field("World_tour_def_tex").GetValue<Texture>());
        }

        /// <summary>The special events popup's World Tour tab keeps the game's picture.</summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator SpecialEventsWorldTourTab(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Sprite national = Traverse.Create(mod.GetType(Pictures, true)).Field("World_tour_def").GetValue<Sprite>();
            SpecialEvents_Manager specialEvents = Game.Main.Data.GetComponent<SpecialEvents_Manager>();
            specialEvents.OpenSpecialEventsPopup();
            specialEvents.OpenTab_WorldTour();
            yield return null;

            Image bg = Path(PopupObject(PopupManager._type.special_events), "Container", "World Tour", "BG")?.GetComponent<Image>();
            ctx.Assert(bg != null, "special_events popup has no Container/World Tour/BG image");
            if (bg != null)
            {
                ctx.Record("World Tour picture", Describe(bg.sprite));
                ctx.Assert(bg.sprite != null && bg.sprite != national, "The World Tour tab shows " + Describe(bg.sprite) + ", not the game's picture");
            }
            yield return Game.CloseAllPopups(ctx);
        }

        /// <summary>
        /// The National Tour tab, a copy of the World Tour tab: a click at its centre lands on it (nothing drawn
        /// over it), the click goes through the copied button's real listeners and the game's TabButton to show
        /// its own panel, and the four tabs fit in the popup without overlapping. Only the engine's layout,
        /// raycasts and event system show these.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator NationalTab(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly _))
                yield break;

            SpecialEvents_Manager specialEvents = Game.Main.Data.GetComponent<SpecialEvents_Manager>();
            GameObject popup = PopupObject(PopupManager._type.special_events);
            specialEvents.OpenSpecialEventsPopup();
            specialEvents.OpenTab_WorldTour();
            yield return WaitForFullSize(popup.transform);
            Canvas.ForceUpdateCanvases();

            Transform types = Path(popup, "Container", "Types");
            Transform tab = types?.Find(NationalTabName);
            GameObject panel = Path(popup, "Container", NationalTabName)?.gameObject;
            GameObject worldPanel = Path(popup, "Container", "World Tour")?.gameObject;
            ctx.Assert(tab != null && panel != null, "The Special Events popup has no National Tour tab and panel");
            if (tab == null || panel == null)
            {
                yield return Game.CloseAllPopups(ctx);
                yield break;
            }

            // The tabs fit: each inside the popup, none overlapping another
            Rect popupRect = ScreenRect((RectTransform)popup.transform);
            Rect[] tabs = types.Cast<Transform>().Where(t => t.gameObject.activeSelf).Select(t => ScreenRect((RectTransform)t)).ToArray();
            ctx.Record("tabs", string.Join(", ", types.Cast<Transform>().Select(t => t.name + " " + ScreenRect((RectTransform)t))));
            ctx.Record("popup", popupRect.ToString());
            ctx.Assert(tabs.Length == 4, "The tab bar has " + tabs.Length + " tabs, not 4");
            foreach (Rect rect in tabs)
                ctx.Assert(popupRect.Contains(rect.min) && popupRect.Contains(rect.max), "A tab at " + rect + " sticks out of the popup at " + popupRect);
            for (int i = 0; i < tabs.Length; i++)
            {
                for (int j = i + 1; j < tabs.Length; j++)
                    ctx.Assert(!Shrink(tabs[i]).Overlaps(Shrink(tabs[j])), "Tabs " + i + " and " + j + " overlap: " + tabs[i] + ", " + tabs[j]);
            }

            // A click at the tab's centre lands on the tab
            PointerEventData pointer = new(EventSystem.current) { position = ScreenRect((RectTransform)tab).center };
            List<RaycastResult> hits = new();
            EventSystem.current.RaycastAll(pointer, hits);
            GameObject hit = hits.Count > 0 ? hits[0].gameObject : null;
            ctx.Record("click lands on", hit == null ? "nothing" : hit.name);
            ctx.Assert(hit != null && hit.transform.IsChildOf(tab), "A click on the National Tour tab lands on " + (hit == null ? "nothing" : hit.name));

            // The click through the event system, as the game gets it from the mouse
            ExecuteEvents.Execute(tab.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            ctx.Assert(panel.activeSelf, "Clicking the National Tour tab didn't show its panel");
            ctx.Assert(!worldPanel.activeSelf, "Clicking the National Tour tab left the World Tour panel shown");
            ctx.Assert((int)specialEvents.OpenTab == 1200, "Clicking the National Tour tab left the open tab at " + specialEvents.OpenTab);

            // And back: the World Tour tab still opens its own panel
            ExecuteEvents.Execute(types.Find("World Tour").gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            ctx.Assert(worldPanel.activeSelf && !panel.activeSelf, "Clicking the World Tour tab after the National Tour tab didn't switch back");
            ctx.Assert(specialEvents.OpenTab == SpecialEvents_Manager._type.WorldTour, "The World Tour tab left the open tab at " + specialEvents.OpenTab);
            yield return Game.CloseAllPopups(ctx);
        }

        /// <summary>
        /// The new tour popup, opened from the National Tour tab's Continue, shows the map of Japan with the 18
        /// prefecture pins, each drawn on the map once the popup is open, and the game's pins hidden. It's opened
        /// twice: the game resets the popup before showing it, the first time at full size and then at the 0.5
        /// its close animation leaves. Only the engine's copies, active filtering, popup animation and transform
        /// hierarchy show where the pins end up.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator NewTourMapAndMarkers(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Sprite japan = Traverse.Create(mod.GetType(Pictures, true)).Field("TOUR_map_2").GetValue<Sprite>();
            SpecialEvents_Manager specialEvents = Game.Main.Data.GetComponent<SpecialEvents_Manager>();
            GameObject popup = PopupObject(PopupManager._type.special_events);
            Tour_New_Popup newTour = PopupObject(PopupManager._type.sevent_tour_new).GetComponent<Tour_New_Popup>();
            for (int open = 1; open <= 2; open++)
            {
                ctx.Record("open " + open + " popup scale before Reset", newTour.transform.localScale.x);
                specialEvents.OpenSpecialEventsPopup();
                yield return null;
                Button tab = Path(popup, "Container", "Types", NationalTabName)?.GetComponent<Button>();
                ctx.Assert(tab != null, "The Special Events popup has no National Tour tab");
                if (tab == null)
                    yield break;
                tab.onClick.Invoke();
                specialEvents.Continue();
                yield return WaitForFullSize(newTour.transform);

                Image map = Path(newTour.gameObject, "Panel", "BG")?.GetComponent<Image>();
                ctx.Assert(map != null, "sevent_tour_new popup has no Panel/BG image");
                if (map == null)
                    yield break;
                ctx.Assert(map.sprite == japan, "The new tour popup shows " + Describe(map.sprite) + ", not National Tour's map of Japan");

                Canvas canvas = map.canvas.rootCanvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Tour_Country[] shown = newTour.CountriesContainer.GetComponentsInChildren<Tour_Country>();
                ctx.Record("open " + open + " pins shown", shown.Length);
                ctx.Assert(shown.Length == 18 && shown.All(p => (int)p.CountryType >= 1200), "The new tour popup shows " + shown.Length + " pins: "
                    + string.Join(", ", shown.Select(p => p.CountryType.ToString())) + ", not the 18 prefectures");
                foreach (Tour_Country pin in shown)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, pin.transform.position);
                    ctx.Assert(RectTransformUtility.RectangleContainsScreenPoint(map.rectTransform, point, camera),
                        "Open " + open + ": " + pin.CountryType + " is drawn at " + point + ", off the map");
                }
                yield return Game.CloseAllPopups(ctx);
            }
        }

        /// <summary>
        /// A world tour's results keep the game's background. The game resets the popup with the real
        /// PopupManager objects, which the unit tests stand in for.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator TourResultsBackground(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type pictures = mod.GetType(Pictures, true);
            PopupManager popups = Game.Main.Data.GetComponent<PopupManager>();
            Tour_Popup results = PopupObject(PopupManager._type.sevent_tour).GetComponent<Tour_Popup>();

            // Reset lowers the progress bar by a fixed step each call, for the animation that follows.
            Vector3 bar = results.Bar.transform.localPosition;
            using (TestTools.Restore(() => results.Bar.transform.localPosition = bar))
                results.Reset();

            ctx.Assert(popups.BGImage.GetComponent<RawImage>().texture != Traverse.Create(pictures).Field("World_tour_def_tex").GetValue<Texture>(),
                "A world tour's results show National Tour's picture");
            Image blurred = Path(results.gameObject, "BG")?.GetComponent<Image>();
            ctx.Assert(blurred != null, "sevent_tour popup has no BG image");
            if (blurred != null)
                ctx.Assert(blurred.sprite != Traverse.Create(pictures).Field("World_tour_def_BG").GetValue<Sprite>(), "A world tour's results show National Tour's blurred picture");
            yield break;
        }

        /// <summary>
        /// Until the popup's open animation has finished: it scales past full size before settling, so the
        /// scale has to stay at 1 for a few frames.
        /// </summary>
        private static IEnumerator WaitForFullSize(Transform popup)
        {
            float start = Time.realtimeSinceStartup;
            int settledFrames = 0;
            while (settledFrames < 3 && Time.realtimeSinceStartup - start < 3f)
            {
                yield return null;
                settledFrames = Mathf.Abs(popup.localScale.x - 1f) < 0.001f ? settledFrames + 1 : 0;
            }
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // Neighbouring tabs share an edge
        private static Rect Shrink(Rect rect) => Rect.MinMaxRect(rect.xMin + 1, rect.yMin + 1, rect.xMax - 1, rect.yMax - 1);

        private static void AssertPicture(TestContext ctx, string name, Texture texture)
        {
            // A picture that failed to load is missing or the 8x8 placeholder.
            ctx.Assert(texture != null && texture.width > 8 && texture.height > 8,
                name + " didn't load" + (texture == null ? "" : " (" + texture.width + "x" + texture.height + ")"));
            if (texture != null)
                ctx.Record(name, texture.width + "x" + texture.height);
        }

        private static GameObject PopupObject(PopupManager._type type)
        {
            return Game.Main.Data.GetComponent<PopupManager>().GetByType(type).obj;
        }

        private static Transform Path(GameObject root, params string[] names)
        {
            Transform t = root.transform;
            foreach (string name in names)
            {
                t = t.Find(name);
                if (t == null)
                    return null;
            }
            return t;
        }

        private static string Describe(Sprite sprite) => sprite == null ? "nothing" : "a picture named '" + sprite.name + "'";
    }
}
