using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace EffortlessTraining.Tests
{
    /// <summary>
    /// The transpiler finds the game's "3 stamina per day" in <c>3f / Mathf.Floor(1440f / ...)</c> and changes only that.
    /// </summary>
    public class TranspilerTests
    {
        private static readonly MethodInfo Original = AccessTools.Method(typeof(agency._room), "DoGirlTraining");

        private static List<CodeInstruction> OriginalIL() => PatchProcessor.GetOriginalInstructions(Original);

        private static List<CodeInstruction> PatchedIL() => agency__room_DoGirlTraining.Transpiler(OriginalIL()).ToList();

        private static bool IsFloat(CodeInstruction instruction, float value) =>
            instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == value;

        private static List<int> DailyCostIndexes(List<CodeInstruction> il) =>
            Enumerable.Range(0, il.Count - 1).Where(i => il[i].opcode == OpCodes.Ldc_R4 && IsFloat(il[i + 1], 1440f)).ToList();

        /// <summary>
        /// The game's training tick has exactly one daily cost followed by the minutes in a day, and it's 3.
        /// </summary>
        [Fact]
        public void Game_ChargesThreeStaminaPerDay()
        {
            List<CodeInstruction> il = OriginalIL();
            int index = Assert.Single(DailyCostIndexes(il));
            Assert.True(IsFloat(il[index], 3f));
        }

        [Fact]
        public void Mod_ChargesOneStaminaPerDay()
        {
            List<CodeInstruction> il = PatchedIL();
            int index = Assert.Single(DailyCostIndexes(il));
            Assert.True(IsFloat(il[index], 1f));
            Assert.DoesNotContain(il, i => IsFloat(i, 3f));
        }

        /// <summary>
        /// The multipliers that follow (Quality policy 1.3, Moonlighter 5) and every other instruction are untouched.
        /// </summary>
        [Fact]
        public void OnlyTheDailyCostChanges()
        {
            // The transpiler edits instructions in place, so snapshot them first
            List<CodeInstruction> original = OriginalIL();
            List<(OpCode opcode, object operand)> before = original.Select(i => (i.opcode, i.operand)).ToList();
            List<CodeInstruction> patched = agency__room_DoGirlTraining.Transpiler(original).ToList();
            Assert.Equal(before.Count, patched.Count);

            List<int> changed = Enumerable.Range(0, before.Count)
                .Where(i => before[i].opcode != patched[i].opcode || !Equals(before[i].operand, patched[i].operand))
                .ToList();
            Assert.Equal(DailyCostIndexes(OriginalIL()), changed);

            Assert.Contains(patched, i => IsFloat(i, 1.3f));
            Assert.Contains(patched, i => IsFloat(i, 5f));
        }

        /// <summary>
        /// If the cost isn't there (another mod changed it), the mod leaves the method as it is rather than
        /// breaking it, and logs why training costs aren't reduced.
        /// </summary>
        [Fact]
        public void NoDailyCost_LeavesMethodUnchanged_AndLogs()
        {
            UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            LogRecorder log = new();
            UnityEngine.Debug.unityLogger.logHandler = log;
            try
            {
            List<CodeInstruction> il = new()
            {
                new CodeInstruction(OpCodes.Ldc_R4, 3f),
                new CodeInstruction(OpCodes.Ldc_R4, 60f),
                new CodeInstruction(OpCodes.Ldc_R4, 1440f),
                new CodeInstruction(OpCodes.Ret),
            };

            List<CodeInstruction> patched = agency__room_DoGirlTraining.Transpiler(il).ToList();

            Assert.Equal(new object[] { 3f, 60f, 1440f, null }, patched.Select(i => i.operand));
            Assert.Contains(log.Messages, m => m.StartsWith("[Effortless Training] Couldn't find"));
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logHandler = gameLog;
            }
        }

        /// <summary>
        /// The game's own method has the cost, so applying the mod logs nothing.
        /// </summary>
        [Fact]
        public void GameMethod_PatchesWithoutLogging()
        {
            UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            LogRecorder log = new();
            UnityEngine.Debug.unityLogger.logHandler = log;
            try
            {
                PatchedIL();
                Assert.Empty(log.Messages);
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logHandler = gameLog;
            }
        }

        /// <summary>
        /// Applied a second time, the cost is already 1 per day: nothing changes and nothing is logged.
        /// </summary>
        [Fact]
        public void AppliedTwice_ChangesNothingMore_AndLogsNothing()
        {
            UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            LogRecorder log = new();
            UnityEngine.Debug.unityLogger.logHandler = log;
            try
            {
                List<CodeInstruction> once = PatchedIL();
                List<string> afterOnce = once.Select(i => i.ToString()).ToList();
                List<CodeInstruction> twice = agency__room_DoGirlTraining.Transpiler(once).ToList();

                Assert.Equal(afterOnce, twice.Select(i => i.ToString()));
                Assert.Empty(log.Messages);
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logHandler = gameLog;
            }
        }

        private class LogRecorder : UnityEngine.ILogHandler
        {
            public readonly List<string> Messages = new();
            public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args) => Messages.Add(string.Format(format, args));
            public void LogException(System.Exception exception, UnityEngine.Object context) => Messages.Add(exception.ToString());
        }
    }
}
