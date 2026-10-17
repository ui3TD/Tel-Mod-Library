using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using static SisterGroups.Utility;

namespace SisterGroups
{

    // In Unfair, fan gain from sister groups is reduced 5x, then scaled by:
    // - group size squared (members / 10, capped at 1),
    // - the share of the group's members singing on the single being released,
    // - the group's own release frequency, as the game does for sales (1 / (1 + its singles released in the past month)).
    // Outside a release (the group appeal popup), every member counts as singing.
    [HarmonyPatch(typeof(Groups._group), nameof(Groups._group.GetNewFansPerSingle))]
    public class Groups__group_GetNewFansPerSingle
    {
        private static bool loggedBadFrequency = false;

        public static void Postfix(ref int __result, Groups._group __instance)
        {
            if (!staticVars.IsHard())
                return;

            int members = __instance.GetGirls().Count;
            if (members == 0)
            {
                __result = 0;
                return;
            }

            float size = Mathf.Min(MEMBER_PENALTY_THR, members) / MEMBER_PENALTY_THR;
            float participation = 1f;
            if (singleSales != null && singleSales.GetGroup() == __instance && singleSales.girls != null)
            {
                int singers = 0;
                foreach (data_girls.girls girl in singleSales.girls)
                {
                    if (girl != null && girl.GetGroup() == __instance)
                        singers++;
                }
                participation = Mathf.Min(1f, (float)singers / members);
            }

            float frequency = singles.GetSaturationCoeff(__instance);
            if (float.IsNaN(frequency) || frequency < 0f)
            {
                if (!loggedBadFrequency)
                {
                    Debug.LogWarning("[More Sister Groups] The release frequency coefficient is " + frequency + ", so sister group new fans ignore it.");
                    loggedBadFrequency = true;
                }
                frequency = 1f;
            }

            __result = Mathf.RoundToInt(__result / FAN_PENALTY * size * size * participation * frequency);
        }
    }


    // Removed minimum member limit for sister group creation
    [HarmonyPatch(typeof(Groups), nameof(Groups.GetIdolsNeededForNewGroup))]
    public class Groups_GetIdolsNeededForNewGroup
    {
        public static void Postfix(ref int __result)
        {
            __result = Groups.CountActiveGroups() + 1;
        }

    }

    // When releasing a single, the penalty for a decrease in fame and appeal now only considers past singles of the same group
    [HarmonyPatch(typeof(singles), nameof(singles.AddOpinion))]
    public class singles_AddOpinion
    {
        public static void Prefix(singles._single single)
        {
            groupSales = single.GetGroup();
        }

        public static void Finalizer()
        {
            groupSales = null;
        }
    }

    // When releasing a single, the penalty for a decrease in fame and appeal now only considers past singles of the same group.
    // The single is also kept for sister group new fans, which count its singers.
    [HarmonyPatch(typeof(singles), nameof(singles.GenerateSales))]
    public class singles_GenerateSales
    {
        public static void Prefix(singles._single single, out ReleaseContext __state)
        {
            __state = new ReleaseContext(groupSales, singleSales);
            groupSales = single.GetGroup();
            singleSales = single;
        }

        // Restores rather than clears, so a nested call can't clear the outer release's context
        public static void Finalizer(ReleaseContext __state)
        {
            groupSales = __state?.Group;
            singleSales = __state?.Single;
        }
    }

    // When releasing a single, the penalty for a decrease in fame and appeal now only considers past singles of the same group
    [HarmonyPatch(typeof(singles), nameof(singles.GetLatestReleasedSingles))]
    public class singles_GetLatestReleasedSingles
    {
        public static void Postfix(int count, ref List<singles._single> __result)
        {
            if (groupSales == null)
                return;

            __result = GetLatestReleasedSingles(count, groupSales);
        }
    }

    public class Utility
    {
        public const float FAN_PENALTY = 5;
        public const float MEMBER_PENALTY_THR = 10;

        // Set by the release steps' prefixes and cleared by their finalizers, so it's cleared even if the step throws
        public static Groups._group groupSales = null;

        // The single whose sales are being generated. Set and restored like groupSales, by the GenerateSales patch only
        public static singles._single singleSales = null;

        public class ReleaseContext
        {
            public readonly Groups._group Group;
            public readonly singles._single Single;

            public ReleaseContext(Groups._group group, singles._single single)
            {
                Group = group;
                Single = single;
            }
        }

        // This method gets latest released singles of a given group. It is based on singles.GetReleasedSingles(int count)
        // which does not take group as a parameter.
        public static List<singles._single> GetLatestReleasedSingles(int count, Groups._group group = null)
        {
            if (group == null)
                return singles.GetLatestReleasedSingles(count);

            List<singles._single> latestReleasedSingles = new();
            for (int i = singles.Singles.Count - 1; i >= 0; i--)
            {
                if (singles.Singles[i].status == singles._single._status.released && singles.Singles[i].GetGroup() == group)
                {
                    latestReleasedSingles.Add(singles.Singles[i]);
                    if (latestReleasedSingles.Count >= count)
                    {
                        break;
                    }
                }
            }
            return latestReleasedSingles;
        }

        // This method compares fan appeals in order to sort them. This is a duplicate of an internal method
        // in the class singles so that it can be referenced publicly.
        public static int CompareAppeal(singles._fanAppeal x, singles._fanAppeal y)
        {
            return y.ratio.CompareTo(x.ratio);
        }
    }


}
