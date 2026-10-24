using HarmonyLib;
using SisterGroups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;
using static singles._single._status;

namespace MoreSisterGroups.Tests
{
    /// <summary>
    /// "When releasing a single, the penalty for a decrease in fame and appeal now only considers past singles
    /// of the same group": while the game generates sales (fame) and fan opinion (appeal) for a single, its
    /// list of the latest released singles holds only that single's group's.
    /// </summary>
    public class SinglesHistoryTests
    {
        private readonly Groups._group main;
        private readonly Groups._group sister;
        private readonly Groups._group other;

        public SinglesHistoryTests()
        {
            main = TestGame.Reset();
            sister = TestGame.Group();
            other = TestGame.Group();
        }

        public static TheoryData<string> ReleaseSteps => new() { "AddOpinion", "GenerateSales" };

        private static void SameSingles(IEnumerable<singles._single> expected, IEnumerable<singles._single> actual) =>
            Assert.Equal(expected, actual, TestGame.SameObject<singles._single>());

        /// <summary>
        /// The release step's own call, as the transpiler makes it: the latest 3 released singles of the single's group.
        /// </summary>
        private static List<singles._single> History(singles._single releasing) => Utility.LatestReleasedSinglesOf(3, releasing);

        /// <summary>
        /// Both release steps work out their penalty from the latest 3 released singles.
        /// </summary>
        [Theory]
        [MemberData(nameof(ReleaseSteps))]
        public void ReleaseStep_ReadsTheLatestReleasedSingles(string step)
        {
            MethodInfo latest = AccessTools.Method(typeof(singles), nameof(singles.GetLatestReleasedSingles));
            Assert.True(TestGame.Calls(AccessTools.Method(typeof(singles), step), latest));
        }

        /// <summary>
        /// Added in 1.1.1: only the release step's own call becomes the same-group one, with the step's single
        /// (its first argument) passed after the count.
        /// </summary>
        [Theory]
        [MemberData(nameof(ReleaseSteps))]
        public void ReleaseStep_ItsOwnCallBecomesTheSameGroupHistory(string step)
        {
            MethodInfo original = AccessTools.Method(typeof(singles), step);
            MethodInfo latest = AccessTools.Method(typeof(singles), nameof(singles.GetLatestReleasedSingles), new[] { typeof(int) });
            MethodInfo sameGroup = AccessTools.Method(typeof(Utility), nameof(Utility.LatestReleasedSinglesOf));

            List<CodeInstruction> patched = Utility.SameGroupHistory(PatchProcessor.GetOriginalInstructions(original), step).ToList();

            Assert.DoesNotContain(patched, i => i.Calls(latest));
            int call = Assert.Single(Enumerable.Range(0, patched.Count), i => patched[i].Calls(sameGroup));
            Assert.Equal(OpCodes.Ldarg_1, patched[call - 1].opcode);
            Assert.Equal(typeof(singles._single), original.GetParameters()[0].ParameterType);
        }

        /// <summary>
        /// Added in 1.1.1: applied twice (two copies of the patch), the second pass leaves the step as the first made it.
        /// </summary>
        [Theory]
        [MemberData(nameof(ReleaseSteps))]
        public void ReleaseStep_PatchedTwice_ChangesOnce(string step)
        {
            List<CodeInstruction> once = Utility.SameGroupHistory(PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(singles), step)), step).ToList();

            List<CodeInstruction> twice = Utility.SameGroupHistory(once, step).ToList();

            Assert.Equal(once.Select(i => i.ToString()), twice.Select(i => i.ToString()));
        }

        /// <summary>
        /// Added in 1.1.1: if the step doesn't have exactly one such call (the game code changed, or another mod
        /// rewrote it), its code is left as it is and the mod says why.
        /// </summary>
        [Fact]
        public void ReleaseStepWithoutTheCall_IsLeftAloneAndLogged()
        {
            UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            LogRecorder log = new();
            UnityEngine.Debug.unityLogger.logHandler = log;
            try
            {
                List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(singles), nameof(singles.GetSaturationCoeff)));

                List<CodeInstruction> patched = Utility.SameGroupHistory(instructions, "GetSaturationCoeff").ToList();

                Assert.Equal(instructions.Select(i => i.ToString()), patched.Select(i => i.ToString()));
                Assert.Contains(log.Messages, m => m.StartsWith("[More Sister Groups] Found 0 calls"));
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logHandler = gameLog;
            }
        }

        [Fact]
        public void ReleaseHistory_OnlyTheSameGroupsSinglesCount()
        {
            singles._single s1 = TestGame.Single(sister);
            TestGame.Single(other);
            singles._single s2 = TestGame.Single(sister);
            TestGame.Single(main);
            TestGame.Single(sister, status: working);
            singles._single s3 = TestGame.Single(sister);
            TestGame.Single(other);
            singles._single releasing = TestGame.Single(sister, status: working);

            SameSingles(new[] { s3, s2, s1 }, History(releasing));
        }

        [Fact]
        public void ReleaseHistory_OnlyTheLatestCountAreReturned()
        {
            TestGame.Single(sister);
            singles._single s2 = TestGame.Single(sister);
            singles._single s3 = TestGame.Single(sister);
            singles._single s4 = TestGame.Single(sister);

            SameSingles(new[] { s4, s3, s2 }, History(s4));
        }

        /// <summary>
        /// A group's first single has no history to be penalised against, however many other groups have released.
        /// </summary>
        [Fact]
        public void ReleaseHistory_AGroupsFirstSingle_HasNoHistory()
        {
            TestGame.Single(main);
            TestGame.Single(other);
            singles._single first = TestGame.Single(sister, status: working);

            Assert.Empty(History(first));
        }

        /// <summary>
        /// The main group's singles (any not on a sister group's list) don't count sister groups' singles either.
        /// </summary>
        [Fact]
        public void ReleaseHistory_MainGroupIgnoresSisterGroups()
        {
            singles._single m1 = TestGame.Single();
            TestGame.Single(sister);
            singles._single m2 = TestGame.Single(main);
            TestGame.Single(other);
            singles._single releasing = TestGame.Single(status: working);

            SameSingles(new[] { m2, m1 }, History(releasing));
        }

        /// <summary>
        /// Added in 1.1.1: while a single's sales are generated, everything else that reads the latest singles
        /// (another mod's patch inside the release) still sees every group's. Only the step's own call is filtered.
        /// </summary>
        [Fact]
        public void DuringARelease_OtherCallsSeeEveryGroup()
        {
            singles._single s1 = TestGame.Single(sister);
            singles._single o1 = TestGame.Single(other);
            singles._single m1 = TestGame.Single(main);

            singles_GenerateSales.Prefix(s1, out Utility.ReleaseContext state);
            try
            {
                SameSingles(new[] { m1, o1, s1 }, singles.GetLatestReleasedSingles(5));
            }
            finally
            {
                singles_GenerateSales.Finalizer(state);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ReleaseStepThatThrows(singles._single single) => throw new InvalidOperationException("release step failed");

        /// <summary>
        /// If the game's release step throws, the error still reaches the game, and the releasing single isn't
        /// kept for later sister group new fans. The patch is applied to a stand-in method that throws, because the
        /// game's methods don't run outside the game.
        /// </summary>
        [Fact]
        public void AfterAFailedRelease_TheSingleIsNotKept()
        {
            singles._single s1 = TestGame.Single(sister);
            Assert.Null(AccessTools.Method(typeof(singles_GenerateSales), "Postfix"));

            Harmony harmony = new(TestGame.HarmonyId + ".FailedRelease");
            try
            {
                MethodInfo standIn = AccessTools.Method(typeof(SinglesHistoryTests), nameof(ReleaseStepThatThrows));
                harmony.Patch(standIn,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(singles_GenerateSales), "Prefix")),
                    finalizer: new HarmonyMethod(AccessTools.Method(typeof(singles_GenerateSales), "Finalizer")));

                Assert.Throws<InvalidOperationException>(() => ReleaseStepThatThrows(s1));
            }
            finally
            {
                harmony.UnpatchSelf();
            }

            Assert.True(Utility.singleSales is null);
        }

        private class LogRecorder : UnityEngine.ILogHandler
        {
            public readonly List<string> Messages = new();
            public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args) => Messages.Add(string.Format(format, args));
            public void LogException(Exception exception, UnityEngine.Object context) => Messages.Add(exception.ToString());
        }

        [Fact]
        public void OutsideRelease_AllGroupsSinglesCount()
        {
            singles._single s1 = TestGame.Single(sister);
            TestGame.Single(main, status: working);
            singles._single o1 = TestGame.Single(other);
            singles._single m1 = TestGame.Single(main);

            SameSingles(new[] { m1, o1 }, singles.GetLatestReleasedSingles(2));
            SameSingles(new[] { m1, o1, s1 }, singles.GetLatestReleasedSingles(5));
        }

        [Fact]
        public void Utility_WithNoGroup_MatchesTheGame()
        {
            singles._single s1 = TestGame.Single(sister);
            singles._single o1 = TestGame.Single(other);

            SameSingles(new[] { o1, s1 }, Utility.GetLatestReleasedSingles(3));
        }

        [Fact]
        public void CompareAppeal_SortsHighestRatioFirst()
        {
            singles._fanAppeal low = new() { ratio = 0.2f };
            singles._fanAppeal high = new() { ratio = 0.8f };
            List<singles._fanAppeal> appeals = new() { low, high };

            appeals.Sort(Utility.CompareAppeal);

            Assert.Equal(new[] { high, low }, appeals, TestGame.SameObject<singles._fanAppeal>());
        }
    }
}
