using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FanAttrition
{
    /// <summary>
    /// Which of an idol's fan groups (gender x casual/hardcore x age) lose the day's fans, and how new fans are
    /// split between them.
    /// </summary>
    /// <remarks>
    /// An idol's weight for a fan group is the group's opinion of her (0 to 1, 0.5 with no votes) plus her stat appeal
    /// to it, as the game uses for its own split of untyped fans (data_girls.girls.AddFans, GetTotalAppeal).
    /// Losses: each group loses in proportion to its fans, times 1 + LOSS_PULL x (1 - weight), and what a group with
    /// no fans left can't pay is taken from the others, so the whole loss is taken.
    /// Gains: a single's and a show's new fans keep the game's split by the release's appeal, times the idol's weight;
    /// every source's new fans give hardcore groups HARDCORE_JOIN of their slice. Each keeps its total.
    /// </remarks>
    public static class FanSplit
    {
        public const double LOSS_PULL = 0.5;
        public const double MAX_WEIGHT = 2.0;
        public const double HARDCORE_JOIN = 0.75;
        public const float NO_VOTES_OPINION = 0.5f;

        /// <summary>Set after an error in a gain split, which then leaves the game's split alone; logged once.</summary>
        public static bool GainSplitFailed;

        /// <summary>
        /// The idol's stat appeal to a fan group, worked out from her stats the way data_girls.girls.RecalcFanAppeal()
        /// does, without changing her saved appeal.
        /// </summary>
        public static float StatAppeal(data_girls.girls girl, resources._fan fan)
        {
            float sum = TypeAppeal(girl, fan.gender) + TypeAppeal(girl, fan.hardcoreness) + TypeAppeal(girl, fan.age);
            return fan.age == resources.fanType.adult ? sum / 3.37f : sum / 3.66f;
        }

        private static float TypeAppeal(data_girls.girls girl, resources.fanType type)
        {
            float ratio = 0f;
            if (girl.parameters != null)
                foreach (data_girls.girls.param param in girl.parameters)
                    if (param != null)
                        ratio += girl.GetAppealOfStat(param.type, type);
            return ratio;
        }

        /// <summary>
        /// Opinion (0 to 1) plus stat appeal; a group with no votes counts as neutral.
        /// </summary>
        public static double Weight(data_girls.girls girl, resources._fan fan)
        {
            float opinion = float.IsNaN(fan.Ratio) ? NO_VOTES_OPINION : fan.Ratio;
            return opinion + StatAppeal(girl, fan);
        }

        // ---------------------------------------------------------------- losses

        /// <summary>
        /// Takes this many fans from the idol's fan groups. Returns the number taken, short of loss only when she
        /// runs out of fans.
        /// </summary>
        public static long TakeFromSegments(data_girls.girls girl, long loss)
        {
            if (girl?.Fans == null || loss <= 0)
                return 0;
            List<resources._fan> fans = girl.Fans.Where(fan => fan != null).ToList();
            // Weighed once, before any group loses fans
            Dictionary<resources._fan, double> factor = fans.ToDictionary(fan => fan,
                fan => 1.0 + LOSS_PULL * (1.0 - Math.Min(MAX_WEIGHT, Math.Max(0.0, Weight(girl, fan)))));

            long taken = 0;
            while (taken < loss)
            {
                List<resources._fan> paying = fans.Where(fan => fan.people > 0).ToList();
                if (paying.Count == 0)
                    break;
                long remaining = loss - taken;
                double[] pull = paying.Select(fan => fan.people * factor[fan]).ToArray();
                double total = pull.Sum();
                long round = 0;
                for (int i = 0; i < paying.Count; i++)
                {
                    long share = Math.Min(paying[i].people, (long)Math.Floor(remaining * pull[i] / total));
                    paying[i].AddPeople(-share);
                    round += share;
                }
                // Rounding left a few fans: one each from the groups that pull hardest
                if (round == 0)
                {
                    foreach (int i in Enumerable.Range(0, paying.Count).OrderByDescending(i => pull[i]))
                    {
                        if (round == remaining)
                            break;
                        paying[i].AddPeople(-1);
                        round++;
                    }
                }
                taken += round;
            }
            return taken;
        }

        // ---------------------------------------------------------------- gains

        /// <summary>
        /// One idol's fans per group before a gain.
        /// </summary>
        public class Before
        {
            public data_girls.girls Girl;
            public Dictionary<resources._fan, long> People;
        }

        /// <summary>
        /// Records the fans of these idols before the game adds new ones.
        /// </summary>
        public static List<Before> Capture(IEnumerable<data_girls.girls> girls)
        {
            if (GainSplitFailed || girls == null)
                return null;
            try
            {
                List<Before> before = new();
                foreach (data_girls.girls girl in girls.Distinct())
                {
                    if (girl?.Fans == null)
                        continue;
                    Dictionary<resources._fan, long> people = new();
                    foreach (resources._fan fan in girl.Fans)
                        if (fan != null && !people.ContainsKey(fan))
                            people[fan] = fan.people;
                    before.Add(new Before { Girl = girl, People = people });
                }
                return before;
            }
            catch (Exception e)
            {
                GainSplitFailed = true;
                Debug.LogError("[Fan Attrition] New fans are split the game's way from now on: " + e);
                return null;
            }
        }

        /// <summary>
        /// Re-splits what each idol gained since Capture: each group's gain x the idol's weight for it (when weighted)
        /// x HARDCORE_JOIN for hardcore groups, keeping her total. Groups that gained nothing still gain nothing.
        /// </summary>
        public static void Respread(List<Before> before, bool weighted)
        {
            if (before == null || GainSplitFailed)
                return;
            try
            {
                foreach (Before idol in before)
                    Respread(idol, weighted);
            }
            catch (Exception e)
            {
                GainSplitFailed = true;
                Debug.LogError("[Fan Attrition] New fans are split the game's way from now on: " + e);
            }
        }

        private static void Respread(Before idol, bool weighted)
        {
            List<resources._fan> gained = idol.People.Keys.Where(fan => fan.people > idol.People[fan]).ToList();
            if (gained.Count == 0)
                return;
            long[] gain = gained.Select(fan => fan.people - idol.People[fan]).ToArray();
            long total = gain.Sum();
            double[] share = new double[gained.Count];
            for (int i = 0; i < gained.Count; i++)
            {
                double m = weighted ? Math.Max(0.0, Weight(idol.Girl, gained[i])) : 1.0;
                if (gained[i].hardcoreness == resources.fanType.hardcore)
                    m *= HARDCORE_JOIN;
                share[i] = gain[i] * m;
            }
            double sum = share.Sum();
            if (!(sum > 0))
                return;

            long[] target = new long[gained.Count];
            double[] rest = new double[gained.Count];
            long placed = 0;
            for (int i = 0; i < gained.Count; i++)
            {
                double exact = total * share[i] / sum;
                target[i] = (long)Math.Floor(exact);
                rest[i] = exact - target[i];
                placed += target[i];
            }
            // The rounding remainder goes to the largest fractions, so the total is kept
            foreach (int i in Enumerable.Range(0, gained.Count).OrderByDescending(i => rest[i]).Take((int)(total - placed)))
                target[i]++;

            for (int i = 0; i < gained.Count; i++)
                if (target[i] != gain[i])
                    gained[i].AddPeople(target[i] - gain[i]);
        }
    }

    /// <summary>
    /// Every untyped or typed fan gain of one idol (promotion, tours, business deals, the cafe, story events, fans
    /// switching idols): hardcore groups get HARDCORE_JOIN of their slice. The game's split by weight is kept.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.AddFans), new[] { typeof(long), typeof(resources.fanType?) })]
    public class data_girls_girls_AddFans_Split
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(data_girls.girls __instance, long val, out List<FanSplit.Before> __state) =>
            __state = val > 0 ? FanSplit.Capture(new[] { __instance }) : null;

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(List<FanSplit.Before> __state) => FanSplit.Respread(__state, weighted: false);
    }

    /// <summary>
    /// A main- or sister-group single's new fans: the game's split by the single's appeal, times each senbatsu idol's
    /// weight, with hardcore groups at HARDCORE_JOIN.
    /// </summary>
    [HarmonyPatch(typeof(singles), nameof(singles.AddNewFans))]
    public class singles_AddNewFans_Split
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(singles._single single, out List<FanSplit.Before> __state) =>
            __state = FanSplit.Capture(single?.girls);

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(List<FanSplit.Before> __state) => FanSplit.Respread(__state, weighted: true);
    }

    /// <summary>
    /// A show episode's new fans: the game's split by the show's appeal, times each cast idol's weight, with
    /// hardcore groups at HARDCORE_JOIN.
    /// </summary>
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.SetSales))]
    public class Shows__show_SetSales_Split
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Shows._show __instance, out List<FanSplit.Before> __state) =>
            __state = FanSplit.Capture(__instance?.GetCast());

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(List<FanSplit.Before> __state) => FanSplit.Respread(__state, weighted: true);
    }
}
