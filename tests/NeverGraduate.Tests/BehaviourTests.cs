using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static data_girls._status;

namespace NeverGraduate.Tests
{
    /// <summary>
    /// The weekly check that sets graduation dates and announces graduations never runs.
    /// </summary>
    public class WeeklyCheckTests
    {
        public WeeklyCheckTests() => TestGame.Reset(patched: true);

        private static MethodInfo WeeklyCheck => AccessTools.Method(typeof(data_girls), nameof(data_girls.UpdateGraduationDates));

        private static readonly MethodInfo[] GraduationSteps =
        {
            AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Set_Default_Date)),
            AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Date_Update)),
        };

        /// <summary>
        /// Added in 1.0.1: the weekly check runs, without its two graduation steps; the prefix lets it run.
        /// </summary>
        [Fact]
        public void WeeklyCheck_RunsWithoutItsGraduationSteps()
        {
            List<CodeInstruction> patched = data_girls_UpdateGraduationDates.RemoveGraduationSteps(
                PatchProcessor.GetOriginalInstructions(WeeklyCheck), out bool removed, out _);

            Assert.True(removed);
            Assert.DoesNotContain(patched, i => GraduationSteps.Any(step => i.Calls(step)));
            Assert.True(data_girls_UpdateGraduationDates.Removed);
            Assert.True(data_girls_UpdateGraduationDates.Prefix());
        }

        public static int AnotherModsCalls;

        public static void AnotherModsWork() => AnotherModsCalls++;

        public static IEnumerable<CodeInstruction> AddAnotherModsWork(IEnumerable<CodeInstruction> instructions) =>
            new[] { new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WeeklyCheckTests), nameof(AnotherModsWork))) }.Concat(instructions);

        /// <summary>
        /// Added in 1.0.1: code another mod adds to the weekly check runs. 1.0.0 skipped the whole method.
        /// </summary>
        [Fact]
        public void AnotherModsCodeInTheWeeklyCheck_Runs()
        {
            data_girls.girls girl = TestGame.Idol(graduationDate: TestGame.Today.AddDays(10));
            Harmony harmony = new(TestGame.HarmonyId + ".AnotherMod");
            AnotherModsCalls = 0;
            try
            {
                harmony.Patch(WeeklyCheck, transpiler: new HarmonyMethod(AccessTools.Method(typeof(WeeklyCheckTests), nameof(AddAnotherModsWork))));

                TestGame.Manager().UpdateGraduationDates();
            }
            finally
            {
                harmony.UnpatchSelf();
            }

            Assert.Equal(1, AnotherModsCalls);
            Assert.Equal(normal, girl.status);
        }

        /// <summary>
        /// Added in 1.0.1: code that doesn't have the two steps once each (the game changed, or another mod rewrote
        /// it) is left as it is, and the prefix skips the whole check as 1.0.0 did.
        /// </summary>
        [Fact]
        public void GameCodeChanged_FallsBackToSkipping()
        {
            // The daily check has Graduation_Date_Update but not Graduation_Set_Default_Date
            List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(data_girls), nameof(data_girls.CheckGraduations)));

            List<CodeInstruction> patched = data_girls_UpdateGraduationDates.RemoveGraduationSteps(instructions, out bool removed, out bool alreadyRemoved);

            Assert.False(removed);
            Assert.False(alreadyRemoved);
            Assert.Equal(instructions.Select(i => i.ToString()), patched.Select(i => i.ToString()));

            bool wasRemoved = data_girls_UpdateGraduationDates.Removed;
            try
            {
                data_girls_UpdateGraduationDates.Removed = false;
                Assert.False(data_girls_UpdateGraduationDates.Prefix());
            }
            finally
            {
                data_girls_UpdateGraduationDates.Removed = wasRemoved;
            }
        }

        /// <summary>
        /// Added in 1.0.1: applied twice (two copies of the patch), the second pass leaves the code as the first made it.
        /// </summary>
        [Fact]
        public void PatchedTwice_ChangesOnce()
        {
            List<CodeInstruction> once = data_girls_UpdateGraduationDates.RemoveGraduationSteps(PatchProcessor.GetOriginalInstructions(WeeklyCheck), out _, out _);

            List<CodeInstruction> twice = data_girls_UpdateGraduationDates.RemoveGraduationSteps(once, out bool removed, out bool alreadyRemoved);

            Assert.False(removed);
            Assert.True(alreadyRemoved);
            Assert.Equal(once.Select(i => i.ToString()), twice.Select(i => i.ToString()));
        }

        /// <summary>
        /// The game gives a new idol a graduation date at her first weekly check; with the mod she never gets one.
        /// </summary>
        [Fact]
        public void NewIdol_GetsNoGraduationDate()
        {
            data_girls.girls girl = TestGame.Idol();

            TestGame.Manager().UpdateGraduationDates();

            Assert.Equal(1900, girl.Graduation_Date.Year);
            Assert.False(girl.Will_Graduate_At_18);
        }

        /// <summary>
        /// An idol whose graduation date is under 90 days away (or already past) isn't made to announce it.
        /// </summary>
        [Theory]
        [InlineData(89)]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(-30)]
        public void IdolDueToGraduate_DoesNotAnnounce(int daysLeft)
        {
            DateTime date = TestGame.Today.AddDays(daysLeft);
            data_girls.girls girl = TestGame.Idol(graduationDate: date);

            TestGame.Manager().UpdateGraduationDates();

            Assert.Equal(normal, girl.status);
            Assert.Equal(date, girl.Graduation_Date);
        }

        /// <summary>
        /// Every idol on the roster is skipped, not only the first one checked.
        /// </summary>
        [Fact]
        public void WholeRoster_IsLeftAlone()
        {
            data_girls.girls unset = TestGame.Idol();
            data_girls.girls due = TestGame.Idol(graduationDate: TestGame.Today.AddDays(10));
            data_girls.girls onHiatus = TestGame.Idol(hiatus, TestGame.Today.AddDays(10));

            TestGame.Manager().UpdateGraduationDates();

            Assert.Equal(1900, unset.Graduation_Date.Year);
            Assert.Equal(normal, due.status);
            Assert.Equal(hiatus, onHiatus.status);
        }

        /// <summary>
        /// Control: without the mod, the game's per-idol check would announce the same idol's graduation
        /// (moving her date to three months away before opening the announcement dialogue).
        /// </summary>
        [Fact]
        public void WithoutTheMod_IdolDueToGraduate_WouldAnnounce()
        {
            data_girls.girls girl = TestGame.Idol(graduationDate: TestGame.Today.AddDays(10));

            try
            {
                Assert.True(girl.Graduation_Date_Update());
            }
            catch (Exception e) when (e is not Xunit.Sdk.XunitException)
            {
                // The announcement dialogue needs the running game; the date is already set by then.
            }

            Assert.Equal(TestGame.Today.AddMonths(3), girl.Graduation_Date);
        }
    }

    /// <summary>
    /// "Unless the girl is fired": firing, letting her graduate, story events, bankruptcy, and the daily
    /// check for idols who already announced all call the game's graduation methods directly, which the mod leaves alone.
    /// </summary>
    public class OtherGraduationsTests
    {
        public OtherGraduationsTests() => TestGame.Reset(patched: true);

        [Fact]
        public void OnlyTheWeeklyCheckIsPatched()
        {
            MethodBase patched = Assert.Single(new Harmony(TestGame.HarmonyId).GetPatchedMethods());
            Assert.Equal(AccessTools.Method(typeof(data_girls), nameof(data_girls.UpdateGraduationDates)), patched);
        }

        [Theory]
        [InlineData(typeof(data_girls.girls), nameof(data_girls.girls.Graduate))]
        [InlineData(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Announce))]
        [InlineData(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Announce_Confirm))]
        [InlineData(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Date_Update))]
        [InlineData(typeof(data_girls), nameof(data_girls.CheckGraduations))]
        public void GraduationMethods_AreNotPatched(Type type, string method)
        {
            Patches info = Harmony.GetPatchInfo(AccessTools.Method(type, method));
            Assert.True(info == null || !info.Owners.Contains(TestGame.HarmonyId), $"{type.Name}.{method} is patched");
        }
    }
}
