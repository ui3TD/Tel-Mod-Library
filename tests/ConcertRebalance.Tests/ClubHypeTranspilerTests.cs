using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace ConcertRebalance.Tests
{
    /// <summary>
    /// The club hype change is one edit to the game's RecalcProjectedValues: "|| Venue == club" is gone,
    /// so above 100% hype a club takes the game's curve.
    /// </summary>
    public class ClubHypeTranspilerTests
    {
        private static readonly MethodInfo Recalc = AccessTools.Method(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.RecalcProjectedValues));
        private static readonly FieldInfo Venue = AccessTools.Field(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Venue));

        private static List<CodeInstruction> Patched(IEnumerable<CodeInstruction> code) =>
            SEvent_Concerts__concert_RecalcProjectedValues.Transpiler(code).ToList();

        [Fact]
        public void VenueCheck_BecomesAJumpToTheCurve()
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(Recalc);
            // The second read of Venue (the first sizes the audience): ldarg.0; ldfld Venue; brtrue curve
            int check = original.FindIndex(original.FindIndex(ci => ci.LoadsField(Venue)) + 1, ci => ci.LoadsField(Venue));
            Label curve = (Label)original[check + 1].operand;

            List<CodeInstruction> patched = Patched(PatchProcessor.GetOriginalInstructions(Recalc));

            Assert.Equal(original.Count - 2, patched.Count);
            Assert.Equal(OpCodes.Br, patched[check - 1].opcode);
            Assert.Equal(curve, patched[check - 1].operand);
            // Everything before and after the check is the game's
            Assert.Equal(original.Take(check - 1).Select(ci => ci.ToString()), patched.Take(check - 1).Select(ci => ci.ToString()));
            Assert.Equal(original.Skip(check + 2).Select(ci => ci.ToString()), patched.Skip(check).Select(ci => ci.ToString()));
        }

        [Fact]
        public void AppliedTwice_ChangesNothingMore_AndLogsNothing()
        {
            using LogRecorder log = new();
            List<CodeInstruction> once = Patched(PatchProcessor.GetOriginalInstructions(Recalc));
            List<string> afterOnce = once.Select(ci => ci.ToString()).ToList();

            List<CodeInstruction> twice = Patched(once);

            Assert.Equal(afterOnce, twice.Select(ci => ci.ToString()));
            Assert.Empty(log.Messages);
        }

        /// <summary>
        /// If another mod changed the check, the code is left as it is and the mod says why clubs keep the
        /// game's linear payout.
        /// </summary>
        [Fact]
        public void CheckNotFound_LeavesTheCodeAndLogs()
        {
            using LogRecorder log = new();
            List<CodeInstruction> code = new() { new CodeInstruction(OpCodes.Ldc_R4, 100f), new CodeInstruction(OpCodes.Ret) };

            List<CodeInstruction> patched = Patched(code);

            Assert.Equal(code.Select(ci => ci.ToString()), patched.Select(ci => ci.ToString()));
            Assert.Contains(log.Messages, m => m.StartsWith("[Concert Rebalance] Couldn't find"));
        }

        private sealed class LogRecorder : UnityEngine.ILogHandler, IDisposable
        {
            private readonly UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            public readonly List<string> Messages = new();
            public LogRecorder() => UnityEngine.Debug.unityLogger.logHandler = this;
            public void Dispose() => UnityEngine.Debug.unityLogger.logHandler = gameLog;
            public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args) => Messages.Add(string.Format(format, args));
            public void LogException(Exception exception, UnityEngine.Object context) => Messages.Add(exception.ToString());
        }
    }
}
