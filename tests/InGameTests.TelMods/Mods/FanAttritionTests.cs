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
    /// Fan Attrition replaces the fan tooltip's Render and fills its lines by position. The unit
    /// tests use a fake tooltip with 15 lines; here it's the game's own tooltip object.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class FanAttritionTests
    {
        private const string HarmonyId = "com.tel.fanattrition";
        private const int Lines = 15;

        /// <summary>
        /// The game's fan tooltip renders exactly the 15 lines the mod fills, ending with the
        /// weekly churn, and its total line is the mod's.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator FanTooltipShowsChurn(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            tooltip_fans tooltip = Resources.FindObjectsOfTypeAll<tooltip_fans>().FirstOrDefault(t => t.gameObject.scene.IsValid());
            if (tooltip == null)
            {
                ctx.Fail("No fan tooltip (tooltip_fans) in the scene");
                yield break;
            }

            // The tooltip is built the first time it's shown (Start); show it if it hasn't been.
            var shown = new List<GameObject>();
            if (!tooltip.gameObject.activeInHierarchy)
            {
                ctx.Note("The fan tooltip wasn't active; activating it for the check");
                for (Transform t = tooltip.transform; t != null; t = t.parent)
                {
                    if (!t.gameObject.activeSelf)
                    {
                        shown.Add(t.gameObject);
                        t.gameObject.SetActive(true);
                    }
                }
                yield return null;
            }

            try
            {
                AccessTools.Method(typeof(tooltip_fans), "Render").Invoke(tooltip, null);
                Text[] texts = tooltip.GetComponentsInChildren<Text>();
                ctx.Record("lines", texts.Length);
                ctx.Assert(texts.Length == Lines, "The fan tooltip has " + texts.Length + " text lines; Fan Attrition fills " + Lines + " by position");
                if (texts.Length >= Lines)
                {
                    string churn = texts[Lines - 1].text;
                    ctx.Record("churnLine", churn);
                    ctx.Assert(churn.StartsWith(Language.Data["CHURN"] + ": "), "Line " + Lines + " isn't the churn line: " + churn);
                }
                string total = tooltip.fan_change.GetComponentInChildren<TMPro.TMP_Text>()?.text
                               ?? tooltip.fan_change.GetComponentInChildren<Text>()?.text;
                ctx.Record("totalLine", total);
                ctx.Assert(total != null && total.StartsWith(Language.Data["TOTAL"] + ": ") && total.Contains(Language.Data["PER_WEEK"]),
                    "The total line isn't Fan Attrition's weekly total: " + total);
            }
            finally
            {
                foreach (GameObject obj in shown)
                    obj.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- fan groups

        /// <summary>Every idol's fan groups as they were, restored after a check.</summary>
        private sealed class FanState
        {
            private readonly List<Action> restore = new List<Action>();

            public FanState()
            {
                foreach (data_girls.girls g in data_girls.girl)
                {
                    if (g?.Fans == null) continue;
                    data_girls.girls girl = g;
                    var order = new List<resources._fan>(g.Fans);
                    restore.Add(() => { girl.Fans.Clear(); girl.Fans.AddRange(order); });
                    foreach (resources._fan f in g.Fans)
                    {
                        resources._fan fan = f;
                        if (fan == null) continue;
                        long people = fan.people;
                        restore.Add(() => fan.people = people);
                    }
                }
            }

            public void Restore()
            {
                foreach (Action a in restore) a();
                resources.RecalcFans();
            }
        }

        private static List<data_girls.girls> ChurnIdols() =>
            data_girls.girl.Where(g => g?.Fans != null && g.status != data_girls._status.graduated).ToList();

        /// <summary>
        /// With every casual group emptied, the day's loss still comes off in full: the groups with fans pay for
        /// the empty ones. Only the game runs the real fan objects with every mod's fan patches loaded.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator DailyChurnTakesTheWholeLoss(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly assembly))
                yield break;
            Type utility = assembly.GetType("FanAttrition.Utility");
            var state = new FanState();
            try
            {
                foreach (data_girls.girls g in ChurnIdols())
                    foreach (resources._fan f in g.Fans)
                        if (f != null && f.hardcoreness == resources.fanType.casual)
                            f.people = 0;
                resources.RecalcFans();
                long nominal = (long)utility.GetMethod("GetDailyChurn").Invoke(null, null);
                long before = resources.GetFansTotal();
                utility.GetMethod("TakeFansByFame").Invoke(null, new object[] { nominal });
                long lost = before - resources.GetFansTotal();
                ctx.Record("lostOfNominal", lost + "/" + nominal);
                ctx.Assert(nominal > 0 && lost == nominal, "Lost " + lost + " of the day's " + nominal + " fans with every casual group empty");
            }
            finally { state.Restore(); }
        }

        // What each watched idol held after the game added a release's fans, before Fan Attrition re-split them
        private static Dictionary<resources._fan, long> added;
        private static List<data_girls.girls> watched;

        private static void RecordAdded()
        {
            added = new Dictionary<resources._fan, long>();
            foreach (data_girls.girls g in watched)
                foreach (resources._fan f in g.Fans)
                    if (f != null) added[f] = f.people;
        }

        /// <summary>
        /// Each idol keeps the new fans the game gave her, re-split between her fan groups. Returns whether anything
        /// was re-split.
        /// </summary>
        private static bool CheckResplit(TestContext ctx, string what, Dictionary<resources._fan, long> start)
        {
            bool moved = false;
            long gained = 0;
            foreach (data_girls.girls g in watched)
            {
                List<resources._fan> fans = g.Fans.Where(f => f != null).ToList();
                long game = fans.Sum(f => added[f]), now = fans.Sum(f => f.people);
                gained += now - fans.Sum(f => start[f]);
                ctx.Assert(game == now, what + ": idol " + g.id + " holds " + now + " fans, the game gave her " + game);
                moved |= fans.Any(f => f.people != added[f]);
            }
            ctx.Record(what + ".newFans", gained);
            ctx.Record(what + ".resplit", moved);
            return moved;
        }

        /// <summary>
        /// A show episode's and a single's new fans are re-split by the idols' opinion and stat appeal, with fewer
        /// hardcore fans, and every idol keeps her total. Only the game runs its own SetSales and AddNewFans (with
        /// their random rolls and every mod's patches) on real idols.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator ReleaseFansAreResplit(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            // The fixture's airing TV show (fixtures/README.md); its cancelled TV shows are worn out and give no fans
            Shows._show show = Shows.shows.FirstOrDefault(s => s != null && s.id == 5);
            singles._single single = singles.GetSingleByID(32);
            singles mgr = Game.Main.Data.GetComponent<singles>();
            ctx.Assert(show != null && single != null && mgr != null, "The fixture's TV show 5 and single 32 are expected");
            if (show == null || single == null || mgr == null)
                yield break;

            var state = new FanState();
            var showSales = new List<singles._single._sales>(show.sales);
            int audience = show.audience.Count, showFans = show.fans.Count;
            long peak = show.peakAudience;
            var showAppeal = show.FanAppeal.Select(a => new KeyValuePair<resources.fanType, float>(a.type, a.ratio)).ToList();
            var castParams = new List<data_girls.girls.param>(show.girlParams);
            var singleSales = new List<singles._single._sales>(single.sales);
            var singleAppeal = single.FanAppeal.Select(a => new KeyValuePair<resources.fanType, float>(a.type, a.ratio)).ToList();
            MethodInfo record = AccessTools.Method(typeof(FanAttritionTests), nameof(RecordAdded));
            try
            {
                // A show episode, as NewEpisode runs it: cast stats, the show's appeal, then the audience and new fans
                watched = show.GetCast().Where(g => g?.Fans != null).Distinct().ToList();
                Dictionary<resources._fan, long> start = watched.SelectMany(g => g.Fans).Where(f => f != null).Distinct().ToDictionary(f => f, f => f.people);
                AccessTools.Method(typeof(Shows._show), "UpdateCastParams").Invoke(show, null);
                AccessTools.Method(typeof(Shows._show), "RecalcFanAppeal").Invoke(show, null);
                using (TestTools.Spy(AccessTools.Method(typeof(Shows._show), "SetSales"), postfix: record, priority: Priority.VeryLow))
                    AccessTools.Method(typeof(Shows._show), "SetSales").Invoke(show, null);
                ctx.Assert(CheckResplit(ctx, "show", start), "The show episode's new fans weren't re-split");

                // A single's new fans
                watched = single.girls.Where(g => g?.Fans != null).Distinct().ToList();
                start = watched.SelectMany(g => g.Fans).Where(f => f != null).Distinct().ToDictionary(f => f, f => f.people);
                mgr.GenerateSales(single);
                using (TestTools.Spy(AccessTools.Method(typeof(singles), "AddNewFans"), postfix: record, priority: Priority.VeryLow))
                    AccessTools.Method(typeof(singles), "AddNewFans").Invoke(mgr, new object[] { single });
                ctx.Assert(CheckResplit(ctx, "single", start), "The single's new fans weren't re-split");
            }
            finally
            {
                show.sales.Clear();
                show.sales.AddRange(showSales);
                if (show.audience.Count > audience) show.audience.RemoveRange(audience, show.audience.Count - audience);
                if (show.fans.Count > showFans) show.fans.RemoveRange(showFans, show.fans.Count - showFans);
                show.peakAudience = peak;
                if (showAppeal.Count == 0) show.FanAppeal.Clear();
                else foreach (var kv in showAppeal) show.FanAppeal.First(a => a.type == kv.Key).ratio = kv.Value;
                show.girlParams.Clear();
                show.girlParams.AddRange(castParams);
                single.sales.Clear();
                single.sales.AddRange(singleSales);
                foreach (var kv in singleAppeal) single.FanAppeal.First(a => a.type == kv.Key).ratio = kv.Value;
                state.Restore();
                watched = null;
                added = null;
            }
        }
    }
}
