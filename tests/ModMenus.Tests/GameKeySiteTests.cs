using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;
using Object = UnityEngine.Object;

using ModMenus.GameKeys;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.3.0: every key the game reads in the methods ModMenus changes has a key row, except those listed
    /// as not managed, and the transpilers find each one in the game's own code.
    /// </summary>
    public class GameKeySiteTests
    {
        /// <summary>
        /// Reads left to the game: the demo's end screen (F1), which the full game never shows.
        /// </summary>
        private static readonly (Type Type, CallKind Kind, KeyCode Key)[] NotManaged =
        {
            (typeof(Controls), CallKind.KeyDown, KeyCode.F1),
        };

        public static IEnumerable<object[]> Methods() =>
            Rows.Sites.Select(site => (site.Type, site.Method)).Distinct().Select(m => new object[] { m.Type, m.Method });

        public GameKeySiteTests()
        {
            TestGame.Reset();
            SiteTranspiler.Reset();
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public void EveryKeyReadIsASiteOrListedAsNotManaged(Type type, string method)
        {
            List<(CallKind Kind, KeyCode Key)> reads = KeyReads(Original(type, method));
            List<(CallKind Kind, KeyCode Key)> sites = Rows.Sites.Where(s => s.Type == type && s.Method == method).Select(s => (s.Kind, s.Key)).ToList();
            List<(CallKind Kind, KeyCode Key)> notManaged = NotManaged.Where(n => n.Type == type).Select(n => (n.Kind, n.Key)).ToList();

            Assert.Equal(reads.OrderBy(r => r.ToString()), sites.Concat(notManaged).OrderBy(r => r.ToString()));
        }

        [Theory]
        [MemberData(nameof(Methods))]
        public void TheTranspilerChangesEverySite(Type type, string method)
        {
            List<CodeInstruction> code = SiteTranspiler.Apply(type, method, Original(type, method)).ToList();

            Assert.Empty(Log.Messages);
            foreach (Site site in Rows.Sites.Where(s => s.Type == type && s.Method == method))
            {
                Assert.True(SiteTranspiler.IsAvailable(site.Row), site.ToString());
            }

            // Each site is now: its key, its row's varID, a call to Sites; no managed read is left
            List<(KeyCode Key, string VarID, MethodInfo Called)> changed = new();
            for (int i = 2; i < code.Count; i++)
            {
                if (code[i].operand is MethodInfo called && called.DeclaringType == typeof(Sites))
                {
                    Assert.Equal(OpCodes.Ldstr, code[i - 1].opcode);
                    Assert.True(SiteTranspiler.TryReadKey(code[i - 2], KindOf(called), out KeyCode key));
                    changed.Add((key, (string)code[i - 1].operand, called));
                }
            }
            List<Site> expected = Rows.Sites.Where(s => s.Type == type && s.Method == method).ToList();
            Assert.Equal(expected.Select(s => (s.Key, s.Row.VarID)).OrderBy(s => s.ToString()), changed.Select(c => (c.Key, c.VarID)).OrderBy(s => s.ToString()));
            Assert.Equal(NotManaged.Count(n => n.Type == type), KeyReads(code).Count);
        }

        /// <summary>
        /// Another mod changed a key the game reads, here pause from Space to P, which a cheat also reads. Counting
        /// reads would send the pause read to the cheat's row, so the whole method is left as it is and its rows
        /// are hidden; other methods' keys still work.
        /// </summary>
        [Theory]
        [InlineData(KeyCode.P)]
        [InlineData(KeyCode.L)]
        public void AMethodAnotherModChangedIsLeftAlone(KeyCode changedTo)
        {
            List<CodeInstruction> original = Original(typeof(Controls), nameof(Controls.Update)).ToList();
            int pause = original.FindIndex(c => c.opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(c.operand) == (int)KeyCode.Space);
            original[pause].operand = (sbyte)changedTo;
            List<string> before = original.Select(c => c.ToString()).ToList();

            List<CodeInstruction> code = SiteTranspiler.Apply(typeof(Controls), nameof(Controls.Update), original).ToList();
            SiteTranspiler.Apply(typeof(Scenes), nameof(Scenes.Update), Original(typeof(Scenes), nameof(Scenes.Update))).ToList();

            Assert.Equal(before, code.Select(c => c.ToString()));
            Assert.False(SiteTranspiler.IsAvailable(Rows.ByVarID["ModMenus_Key_Pause"]));
            Assert.False(SiteTranspiler.IsAvailable(Rows.ByVarID["ModMenus_Key_CheatResearch"]));
            Assert.True(SiteTranspiler.IsAvailable(Rows.ByVarID["ModMenus_Key_ZoomOut"]));
            Assert.Contains(Log.Messages, m => m.Contains("Controls.Update doesn't read the keys it used to"));
        }

        [Fact]
        public void EveryRowsSitesReadItsDefaultKey()
        {
            foreach (Row row in Rows.All)
            {
                foreach (Site site in row.Sites)
                {
                    Assert.Equal(row.Default, site.Key);
                }
            }
        }

        [Fact]
        public void RowsHaveDifferentVarIDsAndEverySiteIsOnce()
        {
            Assert.Equal(Rows.All.Length, Rows.All.Select(r => r.VarID).Distinct().Count());
            Assert.Equal(Rows.Sites.Count(), Rows.Sites.Select(s => s.ToString()).Distinct().Count());
        }

        private static IEnumerable<CodeInstruction> Original(Type type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.Method(type, method));

        /// <summary>
        /// Every Input call in the code that reads a constant key.
        /// </summary>
        private static List<(CallKind Kind, KeyCode Key)> KeyReads(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            List<(CallKind, KeyCode)> reads = new();
            for (int i = 1; i < code.Count; i++)
            {
                if (code[i].operand is MethodInfo called && called.DeclaringType == typeof(Input)
                    && (called.Name.StartsWith("GetKey") || called.Name.StartsWith("GetMouseButton")))
                {
                    CallKind kind = KindOf(called);
                    Assert.True(SiteTranspiler.TryReadKey(code[i - 1], kind, out KeyCode key), "A key read without a constant key: " + called);
                    reads.Add((kind, key));
                }
            }
            return reads;
        }

        private static CallKind KindOf(MethodInfo called)
        {
            Type parameter = called.GetParameters().Last().ParameterType;
            if (called.DeclaringType == typeof(Sites))
                parameter = called.GetParameters()[0].ParameterType;
            return called.Name switch
            {
                "GetKeyDown" => CallKind.KeyDown,
                "GetKey" => CallKind.Key,
                "GetKeyUp" when parameter == typeof(string) => CallKind.KeyUpByName,
                "GetKeyUp" => CallKind.KeyUp,
                "GetKeyUpByName" => CallKind.KeyUpByName,
                "GetMouseButtonDown" => CallKind.MouseButtonDown,
                _ => throw new InvalidOperationException("Not a key read: " + called),
            };
        }
    }
}
