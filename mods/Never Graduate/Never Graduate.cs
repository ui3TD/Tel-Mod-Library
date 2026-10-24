using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace NeverGraduate
{
    // Never check for graduations unless the girl is fired: the game's weekly check runs without its two graduation
    // steps (giving an idol her graduation date, and moving it or announcing her graduation). The rest of the method,
    // and anything other mods add to it, still runs.
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.UpdateGraduationDates))]
    public class data_girls_UpdateGraduationDates
    {
        // Whether the transpiler took the two steps out. If it couldn't, the prefix skips the whole check instead.
        public static bool Removed = false;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = RemoveGraduationSteps(instructions, out bool removed, out bool alreadyRemoved);
            if (removed)
                Removed = true;
            else if (!alreadyRemoved)
                Debug.LogWarning("[Never Graduate] Couldn't find the graduation steps in the weekly check, so the whole check is skipped");
            return codes;
        }

        // Fallback: skip the weekly check when its graduation steps are still in it
        public static bool Prefix() => Removed;

        // Each idol's call to Graduation_Set_Default_Date becomes pop (drops the idol), and to Graduation_Date_Update
        // becomes pop; ldc.i4.0 ("didn't announce", so the loop goes on). Both calls must be there exactly once;
        // otherwise the code is returned as it is. With neither left, the method was already patched.
        public static List<CodeInstruction> RemoveGraduationSteps(IEnumerable<CodeInstruction> instructions, out bool removed, out bool alreadyRemoved)
        {
            MethodInfo setDate = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Set_Default_Date));
            MethodInfo updateDate = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Date_Update));
            List<CodeInstruction> codes = instructions.ToList();
            List<int> setCalls = Enumerable.Range(0, codes.Count).Where(i => codes[i].Calls(setDate)).ToList();
            List<int> updateCalls = Enumerable.Range(0, codes.Count).Where(i => codes[i].Calls(updateDate)).ToList();

            removed = false;
            alreadyRemoved = setCalls.Count == 0 && updateCalls.Count == 0;
            if (setCalls.Count != 1 || updateCalls.Count != 1)
                return codes;

            // The later call first, so the earlier index stays right
            foreach (int call in new[] { setCalls[0], updateCalls[0] }.OrderByDescending(i => i))
            {
                bool returnsAnnounced = call == updateCalls[0];
                codes[call] = new CodeInstruction(OpCodes.Pop).MoveLabelsFrom(codes[call]);
                if (returnsAnnounced)
                    codes.Insert(call + 1, new CodeInstruction(OpCodes.Ldc_I4_0));
            }
            removed = true;
            return codes;
        }
    }

}
