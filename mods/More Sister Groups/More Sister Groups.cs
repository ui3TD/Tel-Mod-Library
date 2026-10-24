using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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


    // Removed minimum member limit for sister group creation. Only the game's own value (10 idols per active group) is
    // replaced, so another mod's requirement stands.
    [HarmonyPatch(typeof(Groups), nameof(Groups.GetIdolsNeededForNewGroup))]
    public class Groups_GetIdolsNeededForNewGroup
    {
        public static void Postfix(ref int __result)
        {
            int activeGroups = Groups.CountActiveGroups();
            if (__result == activeGroups * 10)
                __result = activeGroups + 1;
        }

    }

    // The single whose sales are being generated, kept for sister group new fans, which count its singers.
    [HarmonyPatch(typeof(singles), nameof(singles.GenerateSales))]
    public class singles_GenerateSales
    {
        public static void Prefix(singles._single single, out ReleaseContext __state)
        {
            __state = new ReleaseContext(singleSales);
            singleSales = single;
        }

        // Restores rather than clears, so a nested call can't clear the outer release's context
        public static void Finalizer(ReleaseContext __state)
        {
            singleSales = __state?.Single;
        }

        // When releasing a single, the fame penalty only considers past singles of the same group
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SameGroupHistory(instructions, nameof(singles.GenerateSales));
    }

    // When releasing a single, the appeal penalty only considers past singles of the same group
    [HarmonyPatch(typeof(singles), nameof(singles.AddOpinion))]
    public class singles_AddOpinion
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SameGroupHistory(instructions, nameof(singles.AddOpinion));
    }

    public class Utility
    {
        public const float FAN_PENALTY = 5;
        public const float MEMBER_PENALTY_THR = 10;

        // The single whose sales are being generated. Set by the GenerateSales prefix and restored by its finalizer,
        // so it's restored even if the step throws
        public static singles._single singleSales = null;

        public class ReleaseContext
        {
            public readonly singles._single Single;

            public ReleaseContext(singles._single single)
            {
                Single = single;
            }
        }

        // In a release step (instance method, single as its first argument), its one call to
        // singles.GetLatestReleasedSingles(count) becomes LatestReleasedSinglesOf(count, single). Other calls of
        // GetLatestReleasedSingles, the game's or other mods', keep every group's singles.
        public static IEnumerable<CodeInstruction> SameGroupHistory(IEnumerable<CodeInstruction> instructions, string step)
        {
            MethodInfo latest = AccessTools.Method(typeof(singles), nameof(singles.GetLatestReleasedSingles), new[] { typeof(int) });
            MethodInfo sameGroup = AccessTools.Method(typeof(Utility), nameof(LatestReleasedSinglesOf));
            List<CodeInstruction> codes = instructions.ToList();
            // Already calls the same-group history: patched twice
            if (codes.Any(code => code.Calls(sameGroup)))
                return codes;

            List<int> calls = Enumerable.Range(0, codes.Count).Where(i => codes[i].Calls(latest)).ToList();
            if (calls.Count != 1)
            {
                Debug.LogWarning("[More Sister Groups] Found " + calls.Count + " calls to the latest released singles in singles." + step
                    + " instead of 1, so its penalty counts every group's singles");
                return codes;
            }

            int call = calls[0];
            // single goes after count, between the argument and the call, so a jump to the call still lands before it
            CodeInstruction loadSingle = new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(codes[call]);
            codes[call] = new CodeInstruction(OpCodes.Call, sameGroup);
            codes.Insert(call, loadSingle);
            return codes;
        }

        // The latest released singles of the single's group
        public static List<singles._single> LatestReleasedSinglesOf(int count, singles._single single) =>
            GetLatestReleasedSingles(count, single?.GetGroup());

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
