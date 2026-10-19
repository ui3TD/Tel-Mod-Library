using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FanAttrition
{
    /// <summary>
    /// New fans drift toward each idol's natural fan mix, so targeting still pays but no fan type dies out.
    /// </summary>
    /// <remarks>
    /// Her natural mix is each segment's opinion + stat appeal (the game's own split weight), with hardcore
    /// segments counted at 2/3, so a neutral idol's natural mix is 40% hardcore. After any gain:
    /// - while her hardcore share is above its natural share, 20% of every hardcore gain moves to the casual
    ///   segment of the same gender and age;
    /// - a segment whose share was above its natural share keeps gain x (natural / share)^0.1 and the rest is
    ///   spread over her segments by her natural mix.
    /// The total gained never changes, and losses are left alone. What each segment actually gained is
    /// measured after the game's method, so other mods' changes to gains are counted as they are.
    /// </remarks>
    public static class GainSpill
    {
        public const double GAIN_PULL = 0.1;
        public const double HARDCORE_CONVERSION = 0.2;
        public const double HARDCORE_RARITY = 2.0 / 3.0;
        public const double MIN_SEGMENT_WEIGHT = 0.05;

        /// <summary>
        /// Rounds a fraction of a fan up with that chance, so small gains spill as much as big ones on average.
        /// Replaced by the tests.
        /// </summary>
        public static Func<double> Chance = new System.Random().NextDouble;

        private static bool failed;

        /// <summary>
        /// Each segment's people, before a gain.
        /// </summary>
        public static Dictionary<resources._fan, long> Snapshot(data_girls.girls girl)
        {
            if (girl?.Fans == null || girl.Fans.Count == 0)
                return null;
            Dictionary<resources._fan, long> people = new Dictionary<resources._fan, long>();
            foreach (resources._fan fan in girl.Fans)
            {
                if (fan != null && !people.ContainsKey(fan))
                    people[fan] = fan.people;
            }
            return people;
        }

        /// <summary>
        /// The share of an idol's fans each segment would hold on its own: opinion + stat appeal, hardcore at 2/3.
        /// </summary>
        public static Dictionary<resources._fan, double> NaturalMix(IEnumerable<resources._fan> fans)
        {
            Dictionary<resources._fan, double> weights = new Dictionary<resources._fan, double>();
            foreach (resources._fan fan in fans)
            {
                float ratio = fan.GetOpinion();
                // A segment with no votes reads as neutral opinion
                if (float.IsNaN(ratio))
                    ratio = 0.5f;
                double weight = Math.Max(MIN_SEGMENT_WEIGHT, ratio + fan.appeal);
                if (fan.hardcoreness == resources.fanType.hardcore)
                    weight *= HARDCORE_RARITY;
                weights[fan] = weight;
            }
            double total = weights.Values.Sum();
            return weights.ToDictionary(entry => entry.Key, entry => entry.Value / total);
        }

        /// <summary>
        /// Moves part of what the idol's segments gained since the snapshot toward her natural mix.
        /// </summary>
        public static void Apply(data_girls.girls girl, Dictionary<resources._fan, long> before)
        {
            if (before == null || failed)
                return;
            try
            {
                Spill(girl, before);
            }
            catch (Exception ex)
            {
                // Leave the game's gains as they are from now on rather than fail every gain
                failed = true;
                Debug.LogError("[Fan Attrition] New fans couldn't be moved toward the idols' natural fan mix, so that change is off: " + ex);
            }
        }

        private static void Spill(data_girls.girls girl, Dictionary<resources._fan, long> before)
        {
            List<resources._fan> fans = before.Keys.Where(fan => girl.Fans.Contains(fan)).ToList();
            long beforeTotal = fans.Sum(fan => before[fan]);
            if (beforeTotal <= 0)
                return;

            Dictionary<resources._fan, long> gained = fans.ToDictionary(fan => fan, fan => Math.Max(0, fan.people - before[fan]));
            if (gained.Values.All(g => g == 0))
                return;

            Dictionary<resources._fan, double> natural = NaturalMix(fans);
            Dictionary<resources._fan, double> share = fans.ToDictionary(fan => fan, fan => (double)before[fan] / beforeTotal);

            // Hardcore fans are rarer: while she has more than her natural share, some arrive as casual fans
            double hardcoreShare = fans.Where(IsHardcore).Sum(fan => share[fan]);
            double hardcoreNatural = fans.Where(IsHardcore).Sum(fan => natural[fan]);
            if (hardcoreShare > hardcoreNatural)
            {
                foreach (resources._fan fan in fans.Where(IsHardcore).ToList())
                {
                    resources._fan casual = fans.FirstOrDefault(other => other.hardcoreness == resources.fanType.casual
                        && other.gender == fan.gender && other.age == fan.age);
                    long moved = Round(gained[fan] * HARDCORE_CONVERSION);
                    if (casual == null || moved <= 0)
                        continue;
                    fan.AddPeople(-moved);
                    casual.AddPeople(moved);
                    gained[fan] -= moved;
                    gained[casual] += moved;
                }
            }

            // A segment already above its natural share keeps part of its gain; the rest spreads by her natural mix
            long spilled = 0;
            foreach (resources._fan fan in fans)
            {
                if (gained[fan] <= 0 || share[fan] <= natural[fan])
                    continue;
                long spill = Math.Min(gained[fan], Round(gained[fan] * (1 - Math.Pow(natural[fan] / share[fan], GAIN_PULL))));
                fan.AddPeople(-spill);
                spilled += spill;
            }
            Spread(fans, natural, spilled);
        }

        /// <summary>
        /// Adds fans to the segments in proportion to their natural share; the rounding remainder goes to the largest.
        /// </summary>
        private static void Spread(List<resources._fan> fans, Dictionary<resources._fan, double> natural, long amount)
        {
            if (amount <= 0)
                return;
            long given = 0;
            foreach (resources._fan fan in fans)
            {
                long part = (long)Math.Floor(amount * natural[fan]);
                fan.AddPeople(part);
                given += part;
            }
            fans.OrderByDescending(fan => natural[fan]).First().AddPeople(amount - given);
        }

        private static bool IsHardcore(resources._fan fan) => fan.hardcoreness == resources.fanType.hardcore;

        private static long Round(double value)
        {
            long whole = (long)Math.Floor(value);
            return whole + (Chance() < value - whole ? 1 : 0);
        }
    }

    /// <summary>
    /// Singles (and sister groups' singles) add fans to one segment of each idol.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.AddFans),
        new Type[] { typeof(resources.fanType), typeof(resources.fanType), typeof(resources.fanType), typeof(long) })]
    public class data_girls_girls_AddFans_Segment
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(data_girls.girls __instance, long val, out Dictionary<resources._fan, long> __state)
        {
            __state = val > 0 ? GainSpill.Snapshot(__instance) : null;
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(data_girls.girls __instance, Dictionary<resources._fan, long> __state)
        {
            GainSpill.Apply(__instance, __state);
        }
    }

    /// <summary>
    /// Tours, promotions, contracts, business deals, cafes and fans switching idols add fans across an idol's segments.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.AddFans), new Type[] { typeof(long), typeof(resources.fanType?) })]
    public class data_girls_girls_AddFans_Split
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(data_girls.girls __instance, long val, out Dictionary<resources._fan, long> __state)
        {
            __state = val > 0 ? GainSpill.Snapshot(__instance) : null;
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(data_girls.girls __instance, Dictionary<resources._fan, long> __state)
        {
            GainSpill.Apply(__instance, __state);
        }
    }

    /// <summary>
    /// Shows add fans to one segment of each idol in the cast.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.AddFans_Equally), new Type[] { typeof(long), typeof(resources._fan), typeof(List<data_girls.girls>) })]
    public class data_girls_AddFans_Equally_Segment
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(long total_fans, List<data_girls.girls> Girls, out Dictionary<data_girls.girls, Dictionary<resources._fan, long>> __state)
        {
            __state = null;
            if (total_fans <= 0)
                return;
            __state = new Dictionary<data_girls.girls, Dictionary<resources._fan, long>>();
            foreach (data_girls.girls girl in Girls ?? data_girls.girl ?? new List<data_girls.girls>())
            {
                if (girl != null && girl.status != data_girls._status.graduated && !__state.ContainsKey(girl))
                    __state[girl] = GainSpill.Snapshot(girl);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Dictionary<data_girls.girls, Dictionary<resources._fan, long>> __state)
        {
            if (__state == null)
                return;
            foreach (KeyValuePair<data_girls.girls, Dictionary<resources._fan, long>> entry in __state)
                GainSpill.Apply(entry.Key, entry.Value);
        }
    }
}
