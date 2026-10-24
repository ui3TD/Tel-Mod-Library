using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;

namespace UnofficialPatch
{
	// Fixed Cafe so that money tooltip includes 7 days instead of 6
    [HarmonyPatch(typeof(Cafes), nameof(Cafes.GetLastWeekEarning))]
    public class Cafes_GetLastWeekEarning
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Cafes_GetLastWeekEarning>(ex);

        // Tooltip is intended to show a full week.
        private const int DaysInWeek = 7;

        // Last, so it sees what other mods' patches did.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            // A result that isn't the game's was already changed by another mod.
            if (__result != Vanilla())
            {
                PatchLog.WarnResultChanged<Cafes_GetLastWeekEarning>();
                return;
            }

            // Recount the week in 64 bits: the game's int total wraps past ~2.1 billion.
            long output = 0L;
            foreach (Cafes._cafe cafe in Cafes.Cafes_)
            {
                if (cafe == null || cafe.Stats == null)
                    continue;

                // The last 7 days of each cafe; the game counts only 6.
                for (int i = Math.Max(cafe.Stats.Count - DaysInWeek, 0); i < cafe.Stats.Count; i++)
                {
                    output += cafe.Stats[i].Profit;
                }
            }
            // The tooltip reads an int, so cap the total instead of wrapping.
            __result = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, output));
        }

        // The game's own GetLastWeekEarning, without any mod's patches.
        [HarmonyReversePatch]
        public static int Vanilla()
        {
            throw new NotImplementedException("Replaced by a reverse patch");
        }
    }

    // Fixes the novelty of an idol's new cafe dish. The game added the novelty her dishes had lost instead of
    // taking it away, so every new dish started at 100%; take it away, so a new dish starts lower when her other
    // dishes are stale.
    [HarmonyPatch(typeof(Cafes._cafe), nameof(Cafes._cafe.GetNovelty))]
    public class Cafes__cafe_GetNovelty
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Cafes__cafe_GetNovelty>(ex);

        // Full novelty of one dish.
        private const int FullNovelty = 100;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions);

            // (float)(dishes * 100) - total novelty
            matcher.MatchStartForward(
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, FullNovelty)),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(OpCodes.Conv_R4),
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(OpCodes.Sub));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<Cafes__cafe_GetNovelty>("lost novelty not found.");
                return instructions;
            }

            matcher.Advance(6);
            if (matcher.Instruction.opcode == OpCodes.Neg)
            {
                // Already negated: patched twice, or the game fixed it.
                return matcher.InstructionEnumeration();
            }

            // Negate the lost novelty before the formula adds 100.
            matcher.Insert(new CodeInstruction(OpCodes.Neg));
            return matcher.InstructionEnumeration();
        }
    }

    // Cafe workers get the "practice" status when a shift starts and "normal" when it ends. A depressed idol can work
    // (the game's rule is only that depressed idols can't practice and their stats are halved), but either change
    // replaced her depressed status, so one shift cured her depression. Leave a depressed idol's status alone.
    public static class CafeShiftStatus
    {
        private static readonly MethodInfo SetStatus = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.SetStatus));
        private static readonly MethodInfo SetUnlessDepressedMethod = AccessTools.Method(typeof(CafeShiftStatus), nameof(SetUnlessDepressed));

        public static void SetUnlessDepressed(data_girls.girls girl, data_girls._status status)
        {
            if (girl.status != data_girls._status.depressed)
                girl.SetStatus(status);
        }

        // Replaces each "girl.SetStatus(status)" with SetUnlessDepressed(girl, status), which takes the same stack.
        public static IEnumerable<CodeInstruction> Wrap<TPatch>(IEnumerable<CodeInstruction> instructions, data_girls._status status)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int wrapped = 0;
            for (int i = 1; i < codes.Count; i++)
            {
                if (IlHelpers.IsLdcI4(codes[i - 1], (int)status) && IlHelpers.IsCallTo(codes[i], SetStatus))
                {
                    codes[i] = new CodeInstruction(OpCodes.Call, SetUnlessDepressedMethod).MoveLabelsFrom(codes[i]).MoveBlocksFrom(codes[i]);
                    wrapped++;
                }
            }
            // Already wrapped: patched twice
            if (wrapped == 0 && !codes.Any(ci => IlHelpers.IsCallTo(ci, SetUnlessDepressedMethod)))
                PatchLog.WarnOncePerPatch<TPatch>(status + " status change not found.");
            return codes;
        }
    }

    // Shift start: a depressed idol keeps her status while she works.
    [HarmonyPatch(typeof(Cafes), nameof(Cafes.RenderCafe))]
    public class Cafes_RenderCafe
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Cafes_RenderCafe>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => CafeShiftStatus.Wrap<Cafes_RenderCafe>(instructions, data_girls._status.practice);
    }

    // Shift end: a depressed idol isn't set back to normal.
    [HarmonyPatch(typeof(Cafes._cafe), nameof(Cafes._cafe.StopWorking))]
    public class Cafes__cafe_StopWorking
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Cafes__cafe_StopWorking>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => CafeShiftStatus.Wrap<Cafes__cafe_StopWorking>(instructions, data_girls._status.normal);
    }
}
