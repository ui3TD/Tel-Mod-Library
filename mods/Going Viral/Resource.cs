using HarmonyLib;
using System;
using UnityEngine;

namespace GoingViral
{
    [HarmonyPatch(typeof(resources), "OnNewDay")]
    public class resources_OnNewDay
    {
        [HarmonyAfter("com.tel.fanattrition")]
        public static void Postfix()
        {
            if (TrendingManager.trending < 0)
            {
                // Apply today's crisis before consuming a day, including the final day.
                resources.FansChange = TrendingManager.ScaleLong(resources.FansChange, TrendingManager.GetTrendingCoeff());
                TrendingManager.trending = Math.Min(0, TrendingManager.trending + 1);
            }
            else if (TrendingManager.trending > 0)
            {
                TrendingManager.trending = Math.Max(0, TrendingManager.trending - 1);
            }

            TrendingManager.UpdateFanCount();
        }
    }

    [HarmonyPatch(typeof(resources), "SaveFunction")]
    public class resources_SaveFunction
    {
        private const long SAVE_MARKER = 1000000L;
        private const long SAVE_OFFSET = 100L;

        public static void Postfix()
        {
            if (Camera.main == null)
                return;
            mainScript main = Camera.main.GetComponent<mainScript>();
            if (main == null || main.GetSavedData() == null || main.GetSavedData().resources__Resources == null)
                return;

            main.GetSavedData().resources__Resources.Add(new resources.ResourceData
            {
                Type = resources.type.buzz,
                Val = SAVE_MARKER + SAVE_OFFSET + TrendingManager.trending
            });
        }
    }

    [HarmonyPatch(typeof(resources), "Set")]
    public class resources_Set
    {
        private const long SAVE_MARKER = 1000000L;
        private const long SAVE_OFFSET = 100L;

        public static bool Prefix(resources.type _type, long val)
        {
            // Tagged Buzz values are private save records. Never reinterpret a runtime Buzz Set.
            if (!resources_LoadFunction.Loading || _type != resources.type.buzz)
                return true;

            // New marker. Consume it instead of allowing vanilla Set() to overwrite real Buzz.
            if (val >= SAVE_MARKER && val <= SAVE_MARKER + 200L)
            {
                long loaded = Math.Max(TrendingManager.MIN_TREND_DAYS,
                    Math.Min(TrendingManager.MAX_TREND_DAYS, val - SAVE_MARKER - SAVE_OFFSET));
                TrendingManager.trending = loaded < 0 && !Harmony.HasAnyPatches("com.tel.fanattrition") ? 0 : loaded;
                return false;
            }

            // Backward compatibility with the old 10000 + trending marker, including negative trends.
            // Legitimate game Buzz is capped far below this range.
            if (val >= 9900L && val <= 10100L)
            {
                long loaded = Math.Max(TrendingManager.MIN_TREND_DAYS,
                    Math.Min(TrendingManager.MAX_TREND_DAYS, val - 10000L));
                TrendingManager.trending = loaded < 0 && !Harmony.HasAnyPatches("com.tel.fanattrition") ? 0 : loaded;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(resources), "LoadFunction")]
    public class resources_LoadFunction
    {
        private static int loadDepth;
        internal static bool Loading { get { return loadDepth > 0; } }

        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            loadDepth++;
            // Prevent a save without a marker from inheriting static trend state from a prior loaded game.
            TrendingManager.trending = 0;
        }

        public static void Postfix()
        {
            TrendingManager.UpdateFanCount();
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (loadDepth > 0) loadDepth--;
            return __exception;
        }
    }
}
