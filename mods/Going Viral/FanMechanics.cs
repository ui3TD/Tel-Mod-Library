using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static GoingViral.TrendingManager;

namespace GoingViral
{
    // Replace appeal weights only while an idol is actually distributing a fan change.
    [HarmonyPatch(typeof(data_girls.girls), "AddFans", new Type[] { typeof(long), typeof(resources.fanType?) })]
    public class data_girls_girls_AddFans
    {
        internal sealed class Context
        {
            public data_girls.girls Girl;
            public long Value;
        }

        private static readonly Stack<Context> contexts = new Stack<Context>();
        internal static Context Current { get { return contexts.Count > 0 ? contexts.Peek() : null; } }

        [HarmonyPriority(Priority.First)]
        public static void Prefix(data_girls.girls __instance, long val)
        {
            contexts.Push(new Context { Girl = __instance, Value = val });
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (contexts.Count > 0) contexts.Pop();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(resources._fan), "GetTotalAppeal")]
    public class resources__fan_GetTotalAppeal_TrendingWeights
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(resources._fan __instance, ref float __result)
        {
            data_girls_girls_AddFans.Context context = data_girls_girls_AddFans.Current;
            if (context == null || context.Girl == null || __instance == null)
                return;

            float weight;
            if (context.Value < 0)
                weight = GetFanChurn(__instance.appeal, __instance.Ratio, __instance.hardcoreness);
            else if (context.Value > 0)
                weight = GetFanAcquisition(context.Girl, __instance.appeal, __instance.hardcoreness);
            else
                return;

            if (!float.IsNaN(weight) && !float.IsInfinity(weight))
                __result = Math.Max(0f, weight);
        }
    }

    // While the global AddFans routine allocates losses among idols, substitute churn weights
    // for fame points. The original allocator and its rounding remain intact for compatibility.
    [HarmonyPatch(typeof(data_girls), "AddFans", new Type[] { typeof(long), typeof(resources.fanType?), typeof(List<data_girls.girls>), typeof(data_girls.girls) })]
    public class data_girls_AddFans
    {
        internal sealed class Context
        {
            public long Value;
            public resources.fanType? FanType;
            public List<data_girls.girls> Girls;
            public data_girls.girls ExceptionGirl;
        }

        private static readonly Stack<Context> contexts = new Stack<Context>();
        internal static Context Current { get { return contexts.Count > 0 ? contexts.Peek() : null; } }

        [HarmonyPriority(Priority.First)]
        public static void Prefix(long total_fans, resources.fanType? fanType, List<data_girls.girls> Girls, data_girls.girls ExceptionGirl)
        {
            contexts.Push(new Context
            {
                Value = total_fans,
                FanType = fanType,
                Girls = Girls,
                ExceptionGirl = ExceptionGirl
            });
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (contexts.Count > 0) contexts.Pop();
            return __exception;
        }

        internal static bool IsEligible(Context context, data_girls.girls girl)
        {
            if (context == null || girl == null || girl == context.ExceptionGirl || girl.status == data_girls._status.graduated)
                return false;
            List<data_girls.girls> pool = context.Girls ?? data_girls.girl;
            return pool != null && pool.Contains(girl);
        }

        internal static float GetGirlChurnWeight(Context context, data_girls.girls girl)
        {
            if (!IsEligible(context, girl) || girl.Fans == null)
                return 0f;

            float total = 0f;
            foreach (resources._fan fan in girl.Fans)
            {
                if (fan != null && fan.IsType(context.FanType))
                    total += GetFanChurn(fan.appeal, fan.Ratio, fan.hardcoreness);
            }
            return total;
        }
    }

    [HarmonyPatch(typeof(data_girls.girls), "GetFamePoints")]
    public class data_girls_girls_GetFamePoints_TrendingChurn
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(data_girls.girls __instance, ref float __result)
        {
            data_girls_AddFans.Context context = data_girls_AddFans.Current;
            if (context == null || context.Value >= 0 || !data_girls_AddFans.IsEligible(context, __instance))
                return;

            float weight = data_girls_AddFans.GetGirlChurnWeight(context, __instance);
            if (!float.IsNaN(weight) && !float.IsInfinity(weight) && weight >= 0f)
                __result = weight * 1000f; // avoids vanilla's <1 fame fallback while preserving proportions
        }
    }

    [HarmonyPatch(typeof(resources), "OnNewWeek")]
    public class resources_OnNewWeek
    {
        public static void Postfix()
        {
            if (Theaters.Theaters_ == null)
                return;

            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                if (theater == null || theater.Stats == null || theater.Stats.Count < 7 || theater.GetGroup() == null)
                    continue;

                foreach (resources.fanType type in Enum.GetValues(typeof(resources.fanType)))
                {
                    int thisWeek = CountScheduledDays(theater, type, 1, 7);
                    if (theater.Stats.Count < 14)
                    {
                        if (thisWeek > 0)
                            ApplyTheaterOpinion(theater, type, 1f);
                        continue;
                    }

                    int pastWeek = CountScheduledDays(theater, type, 8, 14);
                    if (thisWeek > pastWeek)
                        ApplyTheaterOpinion(theater, type, 1f);
                    else if (thisWeek < pastWeek)
                        ApplyTheaterOpinion(theater, type, -1f);
                }
            }
        }

        private static int CountScheduledDays(Theaters._theater theater, resources.fanType type, int fromLatest, int toLatest)
        {
            int count = 0;
            if (theater == null || theater.Stats == null)
                return 0;

            for (int offset = fromLatest; offset <= toLatest; offset++)
            {
                int index = theater.Stats.Count - offset;
                if (index < 0 || index >= theater.Stats.Count)
                    continue;
                Theaters._theater._stat stat = theater.Stats[index];
                if (stat != null && stat.Schedule != null && stat.Schedule.FanType == type)
                    count++;
            }
            return count;
        }

        private static void ApplyTheaterOpinion(Theaters._theater theater, resources.fanType type, float value)
        {
            Groups._group group = theater != null ? theater.GetGroup() : null;
            if (group == null || group.GetGirls() == null)
                return;

            foreach (data_girls.girls girl in group.GetGirls())
            {
                if (girl != null && girl.status != data_girls._status.graduated)
                    girl.AddAppeal(type, value);
            }

            string key = value > 0 ? "THEATER__FANS_LIKE" : "THEATER__FANS_DISLIKE";
            string color = value > 0 ? mainScript.green : mainScript.red;
            Color32 color32 = value > 0 ? mainScript.green32 : mainScript.red32;
            NotificationManager.AddNotification(
                Language.Insert(key, new string[] { ExtensionMethods.color(resources.GetFanTitle(type), color), group.Title }),
                color32,
                NotificationManager._notification._type.fans_opinion_change);
        }
    }

    [HarmonyPatch(typeof(resources._fanOpinion), "Add")]
    public class resources__fanOpinion_Add
    {
        public static void Postfix(resources._fanOpinion __instance, float val)
        {
            if (Harmony.HasAnyPatches("com.tel.unofficialpatch") || __instance == null || data_girls.girl == null)
                return;

            foreach (data_girls.girls girl in data_girls.girl)
            {
                if (girl != null && girl.status != data_girls._status.graduated)
                    girl.AddAppeal(__instance.type, val);
            }
        }
    }
}
