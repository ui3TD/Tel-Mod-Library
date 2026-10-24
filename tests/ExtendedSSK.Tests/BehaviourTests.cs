using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;
using static ExtendedSSK.ExtendedSSK;

namespace ExtendedSSK.Tests
{
    /// <summary>
    /// Tests that touch the game's static lists (idols, variables) share one collection so they never run in parallel.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class GameStateCollection
    {
        public const string Name = "Game state";
    }

    [Collection(GameStateCollection.Name)]
    public class FameBonusTests : IDisposable
    {
        public FameBonusTests()
        {
            data_girls.girl.Clear();
            variables.variable.Clear();
        }

        public void Dispose()
        {
            data_girls.girl.Clear();
            variables.variable.Clear();
        }

        private static void AddIdols(int count, data_girls._status status = data_girls._status.normal)
        {
            for (int i = 0; i < count; i++)
                data_girls.girl.Add(new data_girls.girls { status = status });
        }

        private static void SetLimit(int limit)
        {
            variables.variable.Add(new variables._variable { name = varID, value = limit.ToString() });
        }

        private static List<int> RunRecalc(SEvent_SSK._broadcast broadcast)
        {
            SEvent_SSK._SSK ssk = new() { Broadcast = broadcast };
            ssk.RecalcFameBonus();
            SSK_RecalcFameBonusPatch.Postfix(ssk);
            return ssk.FameBonus;
        }

        /// <summary>
        /// Rank 11's bonus: 5.6% of the broadcast's base fame (game formula 6404·L² − 2084·L).
        /// </summary>
        private static int ExpectedRank11(SEvent_SSK._broadcast broadcast)
        {
            int level = broadcast switch
            {
                SEvent_SSK._broadcast.liveBlog => 2,
                SEvent_SSK._broadcast.webStream => 3,
                SEvent_SSK._broadcast.localTV => 4,
                _ => 5,
            };
            int baseVal = Mathf.RoundToInt(resources.FameLevelToPoints(level));
            return Mathf.RoundToInt(baseVal * 0.056f);
        }

        [Theory]
        [InlineData(SEvent_SSK._broadcast.liveBlog, 1201)]
        [InlineData(SEvent_SSK._broadcast.webStream, 2878)]
        [InlineData(SEvent_SSK._broadcast.localTV, 5271)]
        [InlineData(SEvent_SSK._broadcast.nationalTV, 8382)]
        public void Rank11_IsScaledFromBroadcastTier(SEvent_SSK._broadcast broadcast, int expected)
        {
            AddIdols(20);
            Assert.Equal(expected, ExpectedRank11(broadcast));
            Assert.Equal(expected, RunRecalc(broadcast)[10]);
        }

        /// <summary>
        /// Regression for PR #4: the base value used to be cached from the first election of the session.
        /// </summary>
        [Fact]
        public void LaterElections_UseTheirOwnBroadcastTier()
        {
            AddIdols(20);
            List<int> first = RunRecalc(SEvent_SSK._broadcast.liveBlog);
            List<int> second = RunRecalc(SEvent_SSK._broadcast.nationalTV);
            List<int> third = RunRecalc(SEvent_SSK._broadcast.liveBlog);

            Assert.Equal(ExpectedRank11(SEvent_SSK._broadcast.liveBlog), first[10]);
            Assert.Equal(ExpectedRank11(SEvent_SSK._broadcast.nationalTV), second[10]);
            Assert.Equal(first, third);
        }

        [Fact]
        public void EachExtraRank_Gets75PercentOfThePrevious()
        {
            AddIdols(20);
            List<int> bonus = RunRecalc(SEvent_SSK._broadcast.nationalTV);
            for (int i = 11; i < bonus.Count; i++)
                Assert.Equal(Mathf.RoundToInt(bonus[i - 1] * 0.75f), bonus[i]);
        }

        [Theory]
        [InlineData(5, 10)]    // vanilla always pays 10 ranks
        [InlineData(10, 10)]
        [InlineData(11, 11)]
        [InlineData(30, 30)]
        [InlineData(64, 64)]
        [InlineData(100, 64)]  // default limit
        public void RanksPaid_MatchesIdolCountUpToLimit(int idols, int expectedRanks)
        {
            AddIdols(idols);
            Assert.Equal(expectedRanks, RunRecalc(SEvent_SSK._broadcast.localTV).Count);
        }

        [Fact]
        public void ConfiguredLimit_CapsRanksPaid()
        {
            AddIdols(40);
            SetLimit(16);
            Assert.Equal(16, RunRecalc(SEvent_SSK._broadcast.localTV).Count);
        }

        /// <summary>
        /// An unreadable limit (e.g. a hand-edited save) uses the default instead of failing the election.
        /// </summary>
        [Fact]
        public void UnreadableLimit_UsesTheDefault()
        {
            AddIdols(100);
            variables.variable.Add(new variables._variable { name = varID, value = "abc" });
            Assert.Equal(64, RunRecalc(SEvent_SSK._broadcast.localTV).Count);
        }

        [Fact]
        public void GraduatedIdols_DoNotCount()
        {
            AddIdols(12);
            AddIdols(10, data_girls._status.graduated);
            Assert.Equal(12, RunRecalc(SEvent_SSK._broadcast.localTV).Count);
        }

        [Fact]
        public void TopTen_AreUnchangedFromVanilla()
        {
            SEvent_SSK._SSK vanilla = new() { Broadcast = SEvent_SSK._broadcast.webStream };
            vanilla.RecalcFameBonus();

            AddIdols(30);
            List<int> modded = RunRecalc(SEvent_SSK._broadcast.webStream);
            Assert.Equal(vanilla.FameBonus, modded.Take(10));
        }

        /// <summary>
        /// The 1.3.2 postfix, kept as it was: the mod alone must still give exactly these values.
        /// </summary>
        private static List<int> Version132(SEvent_SSK._broadcast broadcast, int girlCount, int limit)
        {
            SEvent_SSK._SSK ssk = new() { Broadcast = broadcast };
            ssk.RecalcFameBonus();
            List<int> list = ssk.FameBonus;
            if (girlCount > 10)
            {
                int baseVal = (int)AccessTools.Method(typeof(SEvent_SSK._SSK), nameof(SEvent_SSK._SSK.GetFameBaseVal)).Invoke(ssk, null);
                int num = Mathf.RoundToInt(baseVal * 0.056f);
                for (int i = 10; i < Math.Min(girlCount, limit); i++)
                {
                    list.Add(num);
                    num = Mathf.RoundToInt(num * 0.75f);
                }
            }
            return list;
        }

        [Theory]
        [InlineData(5)]
        [InlineData(11)]
        [InlineData(30)]
        [InlineData(64)]
        [InlineData(100)]
        public void ModAlone_GivesThe132Values(int idols)
        {
            AddIdols(idols);
            foreach (SEvent_SSK._broadcast broadcast in Enum.GetValues(typeof(SEvent_SSK._broadcast)))
                Assert.Equal(Version132(broadcast, idols, defaultRankings), RunRecalc(broadcast));
        }

        private static SEvent_SSK._SSK GameRecalc(SEvent_SSK._broadcast broadcast)
        {
            SEvent_SSK._SSK ssk = new() { Broadcast = broadcast };
            ssk.RecalcFameBonus();
            return ssk;
        }

        /// <summary>
        /// Another mod already paid 20 places: those stay, and only places 21 and up are added.
        /// </summary>
        [Fact]
        public void AlreadyExtendedList_OnlyGetsTheMissingPlaces()
        {
            AddIdols(30);
            SEvent_SSK._SSK ssk = GameRecalc(SEvent_SSK._broadcast.localTV);
            for (int i = 10; i < 20; i++)
                ssk.FameBonus.Add(1000 - i);
            List<int> theirs = ssk.FameBonus.ToList();

            SSK_RecalcFameBonusPatch.Postfix(ssk);

            Assert.Equal(30, ssk.FameBonus.Count);
            Assert.Equal(theirs, ssk.FameBonus.Take(20));
            Assert.Equal(Mathf.RoundToInt(theirs[19] * 0.75f), ssk.FameBonus[20]);
        }

        /// <summary>
        /// Another mod left only 5 places: the rest are filled with the game's rule, so the results never run past the list.
        /// </summary>
        [Fact]
        public void ShortenedList_IsFilledToTheTarget()
        {
            AddIdols(30);
            SEvent_SSK._SSK vanilla = GameRecalc(SEvent_SSK._broadcast.nationalTV);
            SEvent_SSK._SSK ssk = GameRecalc(SEvent_SSK._broadcast.nationalTV);
            ssk.FameBonus.RemoveRange(5, 5);

            SSK_RecalcFameBonusPatch.Postfix(ssk);

            Assert.Equal(30, ssk.FameBonus.Count);
            Assert.Equal(vanilla.FameBonus, ssk.FameBonus.Take(10));
            Assert.Equal(ExpectedRank11(SEvent_SSK._broadcast.nationalTV), ssk.FameBonus[10]);
        }

        [Fact]
        public void EmptyList_StartsFromTheBaseValue()
        {
            AddIdols(30);
            SEvent_SSK._SSK vanilla = GameRecalc(SEvent_SSK._broadcast.webStream);
            SEvent_SSK._SSK ssk = new() { Broadcast = SEvent_SSK._broadcast.webStream, FameBonus = null };

            SSK_RecalcFameBonusPatch.Postfix(ssk);

            Assert.Equal(vanilla.FameBonus, ssk.FameBonus.Take(10));
            Assert.Equal(30, ssk.FameBonus.Count);
        }

        /// <summary>
        /// Another mod doubled the top 10: place 11 is doubled too, so it stays below place 10.
        /// </summary>
        [Fact]
        public void ScaledTopTen_ScalesPlace11()
        {
            AddIdols(30);
            SEvent_SSK._SSK ssk = GameRecalc(SEvent_SSK._broadcast.nationalTV);
            ssk.FameBonus = ssk.FameBonus.Select(v => v * 2).ToList();

            SSK_RecalcFameBonusPatch.Postfix(ssk);

            int rank11 = ExpectedRank11(SEvent_SSK._broadcast.nationalTV);
            Assert.Equal(rank11 * 2, ssk.FameBonus[10]);
            Assert.True(ssk.FameBonus[10] < ssk.FameBonus[9]);
        }

        [Fact]
        public void Postfix_RunsAfterOtherMods()
        {
            HarmonyPriority priority = AccessTools.Method(typeof(SSK_RecalcFameBonusPatch), nameof(SSK_RecalcFameBonusPatch.Postfix))
                .GetCustomAttribute<HarmonyPriority>();
            Assert.Equal(Priority.Last, priority.info.priority);
        }
    }

    public class ResultsLimitTests
    {
        /// <summary>
        /// The transpiler silently does nothing if it can't find vanilla's "10" rank limit.
        /// </summary>
        [Fact]
        public void Transpiler_FindsVanillaRankLimit()
        {
            MethodInfo original = AccessTools.Method(typeof(SEvent_SSK._SSK), "GenerateResults");
            MethodInfo infix = AccessTools.Method(typeof(SSK_GenerateResultsPatch), nameof(SSK_GenerateResultsPatch.Infix));

            List<CodeInstruction> patched = SSK_GenerateResultsPatch
                .Transpiler(PatchProcessor.GetOriginalInstructions(original))
                .ToList();

            Assert.Single(patched, c => c.Calls(infix));
        }

        /// <summary>
        /// Applied a second time, the limit is already replaced: nothing changes and nothing is logged.
        /// </summary>
        [Fact]
        public void Transpiler_AppliedTwice_ChangesNothingMore()
        {
            MethodInfo original = AccessTools.Method(typeof(SEvent_SSK._SSK), "GenerateResults");
            using LogRecorder log = new();

            List<CodeInstruction> once = SSK_GenerateResultsPatch.Transpiler(PatchProcessor.GetOriginalInstructions(original)).ToList();
            List<string> afterOnce = once.Select(c => c.ToString()).ToList();
            List<CodeInstruction> twice = SSK_GenerateResultsPatch.Transpiler(once).ToList();

            Assert.Equal(afterOnce, twice.Select(c => c.ToString()));
            Assert.Empty(log.Messages);
        }

        /// <summary>
        /// If another mod already changed the limit, the code is left as it is and the mod says why
        /// elections keep that limit.
        /// </summary>
        [Fact]
        public void Transpiler_LimitNotFound_LeavesTheCodeAndLogs()
        {
            using LogRecorder log = new();
            List<CodeInstruction> il = new()
            {
                new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)20),
                new CodeInstruction(OpCodes.Ret),
            };

            List<CodeInstruction> patched = SSK_GenerateResultsPatch.Transpiler(il).ToList();

            Assert.Equal(il.Select(c => c.ToString()), patched.Select(c => c.ToString()));
            Assert.Contains(log.Messages, m => m.StartsWith("[Extended SSK] Couldn't find"));
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

    [Collection(GameStateCollection.Name)]
    public class InfixTests : IDisposable
    {
        public InfixTests() => variables.variable.Clear();

        public void Dispose() => variables.variable.Clear();

        [Fact]
        public void Infix_DefaultsTo64()
        {
            Assert.Equal(64, SSK_GenerateResultsPatch.Infix(10));
        }

        [Fact]
        public void Infix_UsesConfiguredLimit()
        {
            variables.variable.Add(new variables._variable { name = varID, value = "32" });
            Assert.Equal(32, SSK_GenerateResultsPatch.Infix(10));
        }

        /// <summary>
        /// An unreadable limit (e.g. a hand-edited save) uses the default instead of stopping the results.
        /// </summary>
        [Theory]
        [InlineData("abc")]
        [InlineData("14.5")]
        [InlineData("14,5")]
        [InlineData("99999999999")]
        public void Infix_UnreadableLimit_DefaultsTo64(string value)
        {
            variables.variable.Add(new variables._variable { name = varID, value = value });
            Assert.Equal(64, SSK_GenerateResultsPatch.Infix(10));
        }
    }

    public class WishTests
    {
        private static data_girls.girls Wish(girl_wishes._type type, string formula)
        {
            data_girls.girls girl = new() { Wish_Type = type, Wish_Formula = formula };
            girl_wishes_GenerateWish.Postfix(girl);
            return girl;
        }

        [Theory]
        [InlineData("17", "16")]
        [InlineData("64", "16")]
        [InlineData("16", "16")]
        [InlineData("3", "3")]
        public void RankWish_IsCappedAt16(string formula, string expected)
        {
            Assert.Equal(expected, Wish(girl_wishes._type.ssk_rank, formula).Wish_Formula);
        }

        [Fact]
        public void OtherWishes_AreUntouched()
        {
            Assert.Equal("64", Wish(girl_wishes._type.NONE, "64").Wish_Formula);
        }
    }
}
