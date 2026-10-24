using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Module = NationalTour.Readiness.Module;

namespace NationalTour
{
    /// <summary>
    /// What the mod's transpilers share: each finds its anchor calls, and only if each is found exactly as often as
    /// in the game's code does it change the method. Otherwise the method stays as it is and the module is off.
    /// </summary>
    public static class ILEdit
    {
        /// <summary>
        /// One change: each instruction the anchor matches (expected this many times) is replaced.
        /// </summary>
        public class Edit
        {
            public Func<CodeInstruction, bool> Anchor;
            public int Expected;
            public Func<CodeInstruction, IEnumerable<CodeInstruction>> Replace;

            public Edit(Func<CodeInstruction, bool> anchor, int expected, Func<CodeInstruction, IEnumerable<CodeInstruction>> replace)
            {
                Anchor = anchor;
                Expected = expected;
                Replace = replace;
            }
        }

        public static bool IsCall(CodeInstruction instruction, MethodInfo method) =>
            instruction.operand is MethodInfo called && called == method;

        /// <summary>
        /// A call of GetComponent&lt;SEvent_Tour&gt;(), on a GameObject or a Component.
        /// </summary>
        public static bool IsGetTourHandler(CodeInstruction instruction) =>
            instruction.operand is MethodInfo called && called.Name == "GetComponent" && called.IsGenericMethod
            && called.GetGenericArguments()[0] == typeof(SEvent_Tour);

        /// <summary>
        /// The instructions with each anchor rewritten, or unchanged if any anchor isn't found exactly as often as
        /// expected (another mod's transpiler, or a different game version).
        /// </summary>
        public static List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, Module module, string method,
            params Edit[] edits)
        {
            List<CodeInstruction> original = instructions.ToList();
            foreach (Edit edit in edits)
            {
                int found = original.Count(edit.Anchor);
                if (found != edit.Expected)
                {
                    Readiness.Fail(module, $"{method} has its anchor {found} times, not {edit.Expected}");
                    return original;
                }
            }

            List<CodeInstruction> rewritten = new();
            foreach (CodeInstruction instruction in original)
            {
                Edit edit = edits.FirstOrDefault(e => e.Anchor(instruction));
                if (edit == null)
                    rewritten.Add(instruction);
                else
                    rewritten.AddRange(edit.Replace(instruction));
            }
            return rewritten;
        }
    }
}
