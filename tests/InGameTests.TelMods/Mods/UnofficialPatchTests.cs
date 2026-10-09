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
    /// Unofficial Patch's audition portrait fixes on a real audition: a portrait that arrives after
    /// its card was opened reaches the real opened card and stats panel, and closing the audition
    /// while portraits still render leaves no error and no stuck render. The unit tests run these
    /// patches on fake cards; the real prefabs, Unity's destroyed objects and the real portrait
    /// renderer only exist in game.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class UnofficialPatchTests
    {
        private const string HarmonyId = "com.tel.unofficialpatch";

        // Each portrait job starts this much later, as on a slow PC or with nothing cached yet, so
        // the cards unlock before their portraits arrive and the close finds portraits still to come.
        private const float RenderDelay = 3f;
        private static readonly MethodInfo CachePortrait = AccessTools.Method(typeof(data_girls_textures), "NEW_Cache_Portrait");

        /// <summary>
        /// With every portrait late, the cards unlock showing the placeholder. Opens the card whose
        /// portrait comes first, waits for it to reach the opened card and the stats panel, then
        /// closes the audition while the other portraits are still to come.
        /// </summary>
        [InGameTest(Suite = ModTest.AuditionsSuite, Order = 10)]
        private static IEnumerator LatePortraitsLandAndCloseIsClean(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            yield return Game.CloseAllPopups(ctx);

            Type loading = mod.GetType("UnofficialPatch.PortraitLoading", true);
            using (TestTools.Spy(AccessTools.Method(typeof(data_girls_textures), "DoTheThing"),
                       prefix: AccessTools.Method(typeof(UnofficialPatchTests), nameof(DelayRender))))
            {
                yield return Game.OpenAudition(ctx, Auditions.type.local);
                Popup_Audition popup = Game.AuditionPopup;
                List<Auditions.data._girl> girls = Game.Main.Data.GetComponent<Auditions>().Get(Auditions.type.local).Girls;
                // The game alone would wait for every portrait before revealing the cards.
                yield return TestTools.WaitFor(ctx, () => popup.Button_Finish.activeSelf, 60f, "the audition to reveal its cards");

                Sprite placeholder = Traverse.Create(loading).Field("placeholder").GetValue<Sprite>();
                List<Auditions.data._girl> late = girls
                    .Where(g => g.CardObject != null && IsPlaceholder(g.CardObject.GetComponent<Audition_Closed_Card>().Portrait, placeholder))
                    .OrderBy(g => QueuePosition(g.girl))
                    .ToList();
                ctx.Record("candidates", girls.Count);
                ctx.Record("lateAtReveal", late.Count);
                ctx.Assert(late.Count > 0, "No card was revealed showing the placeholder, though every portrait was late");

                if (late.Count > 0)
                {
                    Auditions.data._girl girl = late[0];
                    girl.CardObject.GetComponent<Audition_Closed_Card>().OnClick();
                    yield return TestTools.WaitFor(ctx, () => girl.CardObject != null && girl.CardObject.GetComponent<Audition_Golden_Card>() != null,
                        5f, "the clicked card to open");
                    Audition_Golden_Card opened = girl.CardObject.GetComponent<Audition_Golden_Card>();
                    Audition_Data_Card panel = popup.Card_Container.GetComponent<Audition_Data_Card>();
                    yield return TestTools.WaitFor(ctx,
                        () => IsReal(opened.Portrait, placeholder) && IsReal(opened.PortraitShadow, placeholder)
                              && IsReal(panel.Portrait, placeholder) && IsReal(panel.Portrait_Shadow, placeholder),
                        60f, "the late portrait to reach the opened card and the stats panel");
                    // The panel's show animation starts coroutines when it ends; the game can't close under it.
                    yield return TestTools.WaitFor(ctx, () => !panel.Animating, 10f, "the stats panel to finish showing");
                }

                int toCome = Queue().Count + RunningJobs();
                ctx.Record("portraitsToComeAtClose", toCome);
                ctx.Assert(toCome > 0, "Every portrait had arrived before the close, so it couldn't drop any");
                popup.Close();
                yield return Game.CloseAllPopups(ctx);
                yield return TestTools.WaitFor(ctx, () => Queue().Count == 0 && RunningJobs() == 0 && Renderers() == 0, 60f,
                    "the portrait queue to empty and every render to finish after the close");
            }
            // Loaders whose cards the close destroyed end here; any error fails the test.
            yield return new WaitForSecondsRealtime(2f);
        }

        /// <summary>Starts the game's portrait render job (data_girls_textures.DoTheThing) RenderDelay seconds late.</summary>
        private static bool DelayRender(data_girls_textures __instance, data_girls.girls Girl, GameObject Target_Object)
        {
            if (Target_Object != null)
                return true;
            __instance.StartCoroutine(Delayed(__instance, Girl));
            return false;
        }

        private static IEnumerator Delayed(data_girls_textures textures, data_girls.girls girl)
        {
            yield return new WaitForSecondsRealtime(RenderDelay);
            yield return textures.StartCoroutine((IEnumerator)CachePortrait.Invoke(textures, new object[] { girl }));
        }

        /// <summary>Render jobs started and not yet finished (data_girls_textures.ActiveQueue).</summary>
        private static int RunningJobs() => Traverse.Create(typeof(data_girls_textures)).Field("ActiveQueue").GetValue<int>();

        private static bool IsPlaceholder(GameObject portrait, Sprite placeholder)
        {
            Sprite sprite = portrait.GetComponent<Image>().sprite;
            return !ReferenceEquals(placeholder, null) && ReferenceEquals(sprite, placeholder);
        }

        private static bool IsReal(GameObject portrait, Sprite placeholder)
        {
            Sprite sprite = portrait != null ? portrait.GetComponent<Image>().sprite : null;
            return sprite != null && !ReferenceEquals(sprite, placeholder);
        }

        /// <summary>The game's portrait render queue (data_girls_textures.Queue); the render in progress isn't in it.</summary>
        private static IList Queue() => (IList)AccessTools.Field(typeof(data_girls_textures), "Queue").GetValue(null);

        /// <summary>Her place in the render queue; -1 while hers is the render in progress or done.</summary>
        private static int QueuePosition(data_girls.girls girl)
        {
            IList queue = Queue();
            for (int i = 0; i < queue.Count; i++)
            {
                if (ReferenceEquals(Traverse.Create(queue[i]).Field("Girl").GetValue(), girl))
                    return i;
            }
            return -1;
        }

        private static int Renderers() => UnityEngine.Object.FindObjectsOfType<Portrait_Renderer>().Length;
    }
}
