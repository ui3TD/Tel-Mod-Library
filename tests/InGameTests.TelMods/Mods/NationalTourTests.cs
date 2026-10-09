using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// National Tour swaps the world tour's pictures and moves the country markers in the game's
    /// own popups, found by object path. The unit tests stub Find and LoadTexture.
    /// </summary>
    internal static class NationalTourTests
    {
        private const string HarmonyId = "com.tel.nationaltour";
        private const string Utility = "NationalTour.Utility";

        /// <summary>The three replacement pictures loaded from the mod's folder when the game scene started.</summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator PicturesLoaded(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type utility = mod.GetType(Utility, true);
            foreach (string name in new[] { "TOUR_map_2", "World_tour_def", "World_tour_def_BG" })
                AssertPicture(ctx, name, Traverse.Create(utility).Field(name).GetValue<Sprite>()?.texture);
            AssertPicture(ctx, "World_tour_def_tex", Traverse.Create(utility).Field("World_tour_def_tex").GetValue<Texture>());
        }

        /// <summary>The special events popup shows the national tour picture on its World Tour tab.</summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator SpecialEventsWorldTourTab(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Sprite expected = Traverse.Create(mod.GetType(Utility, true)).Field("World_tour_def").GetValue<Sprite>();
            SpecialEvents_Manager specialEvents = Game.Main.Data.GetComponent<SpecialEvents_Manager>();
            specialEvents.OpenSpecialEventsPopup();
            specialEvents.OpenTab_WorldTour();
            yield return null;

            Image bg = Path(PopupObject(PopupManager._type.special_events), "Container", "World Tour", "BG")?.GetComponent<Image>();
            ctx.Assert(bg != null, "special_events popup has no Container/World Tour/BG image");
            if (bg != null)
                ctx.Assert(bg.sprite == expected, "The World Tour tab shows " + Describe(bg.sprite) + ", not National Tour's World_tour_def");
            yield return Game.CloseAllPopups(ctx);
        }

        /// <summary>
        /// The new tour popup shows the map of Japan, and every country marker sits on its
        /// prefecture (a country the mod has no prefecture for would throw on Reset).
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator NewTourMapAndMarkers(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type utility = mod.GetType(Utility, true);
            PopupManager popups = Game.Main.Data.GetComponent<PopupManager>();
            Tour_New_Popup newTour = PopupObject(PopupManager._type.sevent_tour_new).GetComponent<Tour_New_Popup>();
            popups.Open(PopupManager._type.sevent_tour_new);
            yield return null;
            newTour.Reset();

            Image map = Path(newTour.gameObject, "Panel", "BG")?.GetComponent<Image>();
            ctx.Assert(map != null, "sevent_tour_new popup has no Panel/BG image");
            if (map != null)
                ctx.Assert(map.sprite == Traverse.Create(utility).Field("TOUR_map_2").GetValue<Sprite>(), "The new tour popup shows " + Describe(map.sprite) + ", not National Tour's map of Japan");

            IDictionary prefectures = Traverse.Create(utility).Field("tourLocations").GetValue<IDictionary>();
            IDictionary positions = Traverse.Create(utility).Field("locationDict").GetValue<IDictionary>();
            Tour_Country[] countries = newTour.CountriesContainer.GetComponentsInChildren<Tour_Country>();
            ctx.Record("countries", countries.Length);
            ctx.Assert(countries.Length > 0, "The new tour popup shows no countries");
            foreach (Tour_Country country in countries)
            {
                object prefecture = prefectures.Contains(country.CountryType) ? prefectures[country.CountryType] : null;
                if (prefecture == null || !positions.Contains(prefecture))
                {
                    ctx.Fail(country.CountryType + " has no prefecture position in National Tour");
                    continue;
                }
                Vector3 expected = (Vector3)positions[prefecture];
                ctx.Assert(Vector3.Distance(country.transform.position, expected) < 0.01f,
                    country.CountryType + " is at " + country.transform.position + ", expected " + prefecture + " " + expected);
            }
            yield return Game.CloseAllPopups(ctx);
        }

        /// <summary>The tour results popup shows the national tour background and its blurred copy.</summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator TourResultsBackground(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type utility = mod.GetType(Utility, true);
            PopupManager popups = Game.Main.Data.GetComponent<PopupManager>();
            Tour_Popup results = PopupObject(PopupManager._type.sevent_tour).GetComponent<Tour_Popup>();

            // Reset lowers the progress bar by a fixed step each call, for the animation that follows.
            Vector3 bar = results.Bar.transform.localPosition;
            using (TestTools.Restore(() => results.Bar.transform.localPosition = bar))
                results.Reset();

            ctx.Assert(popups.BGImage.GetComponent<RawImage>().texture == Traverse.Create(utility).Field("World_tour_def_tex").GetValue<Texture>(),
                "The popup background isn't National Tour's World_tour_def");
            Image blurred = Path(results.gameObject, "BG")?.GetComponent<Image>();
            ctx.Assert(blurred != null, "sevent_tour popup has no BG image");
            if (blurred != null)
                ctx.Assert(blurred.sprite == Traverse.Create(utility).Field("World_tour_def_BG").GetValue<Sprite>(), "The results show " + Describe(blurred.sprite) + ", not National Tour's World_tour_def_BG");
            yield break;
        }

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
