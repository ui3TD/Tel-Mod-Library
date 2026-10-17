using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Xunit;
using _type = Theaters._theater._schedule._type;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// Another mod's patches on the same methods, applied for one test under their own Harmony ID.
    /// </summary>
    public abstract class OtherModTests : IDisposable
    {
        private readonly Harmony otherMod = new("tests.UnofficialPatch.OtherMod");

        protected OtherModTests() => TestGame.Reset();

        public void Dispose() => otherMod.UnpatchSelf();

        protected void Prefix(MethodBase target, string stub) =>
            otherMod.Patch(target, prefix: new HarmonyMethod(typeof(OtherModTests), stub));

        protected void Postfix(MethodBase target, string stub) =>
            otherMod.Patch(target, postfix: new HarmonyMethod(typeof(OtherModTests), stub));

        protected static MethodInfo Method(Type type, string name) => AccessTools.Method(type, name);

        // What the other mod does
        private static bool Skip() => false;
        private static void PayIdols() => TestGame.MainGroup.Girls.ForEach(girl => girl.Earn(1000));
        private static void PropagateOpinion(resources._fanOpinion __instance, float val) =>
            data_girls.girl.ForEach(girl => girl.AddAppeal(__instance.type, val));
        private static void ChangeInt(ref int __result) => __result = 12345;
        private static void ChangeLong(ref long __result) => __result = 12345L;
    }

    /// <summary>
    /// When another mod skips the game's method, the fix doesn't act on a change that didn't happen.
    /// </summary>
    public class SkippedByAnotherModTests : OtherModTests
    {
        [Fact]
        public void SkippedTheaterDay_PaysNothingAndKeepsTheSchedule()
        {
            Theaters._theater theater = TheaterGame.Open(_type.performance, idols: 2);
            TheaterGame.CompleteDay(TestGame.Today);
            Seams.MoneyAdded.Clear();
            List<long> earnings = TestGame.MainGroup.Girls.Select(girl => girl.Earnings_CurrentMonth).ToList();
            int stats = theater.Stats.Count;
            TheaterGame.Schedule(theater, _type.manzai);

            Prefix(Method(typeof(Theaters), "CompleteDay"), "Skip");
            TheaterGame.CompleteDay(TestGame.Today.AddDays(1));

            Assert.Equal(0L, TheaterGame.MoneyAdded);
            Assert.Equal(earnings, TestGame.MainGroup.Girls.Select(girl => girl.Earnings_CurrentMonth));
            Assert.Equal(stats, theater.Stats.Count);
            Assert.Equal(_type.performance, theater.Doing_Now);
        }

        [Fact]
        public void SkippedBreakUp_KeepsTheStatusKnown()
        {
            data_girls.girls a = TestGame.Hire(TestGame.Idol(name: "A"));
            data_girls.girls b = TestGame.Hire(TestGame.Idol(name: "B"));
            a.DatingData.Is_Partner_Status_Known = true;
            b.DatingData.Is_Partner_Status_Known = true;
            Relationships._relationship couple = new() { Dating = true };
            couple.Girls.Add(a);
            couple.Girls.Add(b);

            Prefix(Method(typeof(Relationships._relationship), nameof(Relationships._relationship.BreakUp)), "Skip");
            couple.BreakUp();

            Assert.True(couple.Dating);
            Assert.True(a.DatingData.Is_Partner_Status_Known);
            Assert.True(b.DatingData.Is_Partner_Status_Known);
        }

        [Fact]
        public void SkippedOpinionChange_DoesNotReachIdols()
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol());
            girl.Fans.Add(new resources._fan { age = resources.fanType.teen });

            Prefix(Method(typeof(resources._fanOpinion), nameof(resources._fanOpinion.Add)), "Skip");
            new resources._fanOpinion { type = resources.fanType.teen }.Add(2f);

            Assert.Empty(girl.Fans[0].Vals);
            Assert.Equal(0f, girl.Fans[0].Temp);
        }
    }

    /// <summary>
    /// When another mod already fixed the same bug, the fix isn't applied a second time.
    /// </summary>
    public class AlreadyFixedByAnotherModTests : OtherModTests
    {
        [Fact]
        public void IdolsAlreadyPaidForTheShow_AreNotPaidAgain()
        {
            TheaterGame.Open(_type.performance, idols: 2);

            Postfix(Method(typeof(Theaters), "CompleteDay"), "PayIdols");
            TheaterGame.CompleteDay(TestGame.Today);

            Assert.All(TestGame.MainGroup.Girls, girl => Assert.Equal(1000L, girl.Earnings_CurrentMonth));
            Assert.True(TheaterGame.MoneyAdded > 0L);
        }

        [Theory]
        [InlineData(2.5f)]
        [InlineData(0.4f)]
        public void OpinionAlreadyPropagated_ReachesIdolsOnce(float change)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol());
            girl.Fans.Add(new resources._fan { age = resources.fanType.teen });
            resources._fan once = new() { age = resources.fanType.teen };
            once.AddOpinion(change);

            // A prefix: a postfix's val is what the game counted it down to
            Prefix(Method(typeof(resources._fanOpinion), nameof(resources._fanOpinion.Add)), "PropagateOpinion");
            new resources._fanOpinion { type = resources.fanType.teen }.Add(change);

            Assert.Equal(once.Vals, girl.Fans[0].Vals);
            Assert.Equal(once.Temp, girl.Fans[0].Temp);
        }

        [Fact]
        public void ChangedAverages_AreLeftAsTheyAre()
        {
            Theaters._theater theater = new();
            theater.Stats.Add(TheaterGame.Stat(theater, _type.performance, 1000, 80));
            theater.Stats.Add(TheaterGame.Stat(theater, _type.day_off, 0, 0));

            Postfix(Method(typeof(Theaters._theater), nameof(Theaters._theater.GetAvgRevenue)), "ChangeInt");
            Postfix(Method(typeof(Theaters._theater), nameof(Theaters._theater.GetAvgAttendance)), "ChangeInt");

            Assert.Equal(12345, theater.GetAvgRevenue());
            Assert.Equal(12345, theater.GetAvgAttendance());
            Assert.Equal(2, Log.Of(LogType.Warning).Count(m => m.Contains("already changed by another mod")));
        }

        [Fact]
        public void ChangedWeeklyTotals_AreLeftAsTheyAre()
        {
            Theaters._theater theater = new();
            for (int day = 0; day < 7; day++)
                theater.Stats.Add(TheaterGame.Stat(theater, _type.performance, 10));
            Theaters.Theaters_.Add(theater);
            Cafes._cafe cafe = new();
            for (int day = 0; day < 7; day++)
                cafe.Stats.Add(new Cafes._cafe._stat { Profit = 10 });
            Cafes.Cafes_.Add(cafe);

            Postfix(Method(typeof(Theaters), nameof(Theaters.GetLastWeekEarning)), "ChangeLong");
            Postfix(Method(typeof(Cafes), nameof(Cafes.GetLastWeekEarning)), "ChangeInt");

            Assert.Equal(12345L, Theaters.GetLastWeekEarning());
            Assert.Equal(12345, Cafes.GetLastWeekEarning());
        }
    }

    /// <summary>
    /// A patch that can't be applied is logged and skipped, so the mod's other patches still apply.
    /// </summary>
    public class PatchFailureTests : IDisposable
    {
        private readonly Harmony harmony = new("tests.UnofficialPatch.Failure");

        public PatchFailureTests()
        {
            TestGame.Reset();
            PatchLog.RethrowPatchFailures = false;
        }

        public void Dispose()
        {
            PatchLog.RethrowPatchFailures = true;
            harmony.UnpatchSelf();
        }

        public static int Target() => 1;

        [HarmonyPatch(typeof(PatchFailureTests), nameof(Target))]
        public class Failing
        {
            public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Failing>(ex);

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                throw new InvalidOperationException("another mod changed the method");
        }

        [Fact]
        public void FailedPatch_IsLoggedAndSkipped()
        {
            harmony.CreateClassProcessor(typeof(Failing)).Patch();

            string error = Assert.Single(Log.Of(LogType.Error));
            Assert.Contains("couldn't be applied", error);
            Assert.Equal(1, Target());
        }

        [Fact]
        public void EveryPatchClass_SkipsItselfIfItFails()
        {
            Assert.All(TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(Theaters_CompleteDay).Assembly, "UnofficialPatch"), patchClass =>
            {
                MethodInfo cleanup = AccessTools.DeclaredMethod(patchClass, "Cleanup");
                Assert.True(cleanup != null, patchClass.Name + " has no Cleanup");
                Assert.Null(cleanup.Invoke(null, new object[] { new InvalidOperationException("test") }));
            });
        }
    }

    /// <summary>
    /// Transpiling a method the mod already transpiled changes nothing and warns of nothing.
    /// </summary>
    public class TranspiledTwiceTests
    {
        public TranspiledTwiceTests() => TestGame.Reset();

        private static void AssertUnchanged(List<CodeInstruction> once, List<CodeInstruction> twice)
        {
            Assert.Equal(once.Select(ci => ci.ToString()), twice.Select(ci => ci.ToString()));
            Assert.Empty(Log.Messages);
        }

        [Fact]
        public void StatRoll()
        {
            MethodInfo method = AccessTools.Method(typeof(data_girls), nameof(data_girls.GenerateParams));
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(method, out ILGenerator generator);
            List<CodeInstruction> once = data_girls_GenerateParams.Transpiler(original, generator).ToList();
            AssertUnchanged(once, data_girls_GenerateParams.Transpiler(once, generator).ToList());
        }

        [Fact]
        public void SenbatsuRows()
        {
            MethodInfo method = AccessTools.Method(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam));
            List<CodeInstruction> once = singles__single_SenbatsuCalcParam.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToList();
            AssertUnchanged(once, singles__single_SenbatsuCalcParam.Transpiler(once).ToList());
        }

        [Fact]
        public void ConcertRevenue()
        {
            MethodInfo method = AccessTools.Method(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetRevenue));
            List<CodeInstruction> once = SEvent_Concerts__concert__projectedValues_GetRevenue.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToList();
            AssertUnchanged(once, SEvent_Concerts__concert__projectedValues_GetRevenue.Transpiler(once).ToList());
        }
    }

    /// <summary>
    /// Missing data the game never produces is skipped instead of throwing.
    /// </summary>
    public class MissingDataTests
    {
        public MissingDataTests() => TestGame.Reset();

        [Fact]
        public void NoGossipList_StaysNull()
        {
            List<Date_Gossip._gossip> gossips = null;
            Date_Gossip_GetAvailableGossips.Postfix(ref gossips, TestGame.Idol());
            Assert.Null(gossips);
        }

        [Fact]
        public void StatWithoutASchedule_KeepsTheGamesAverage()
        {
            Theaters._theater theater = new();
            theater.Stats.Add(TheaterGame.Stat(theater, _type.performance, 1000, 80));
            theater.Stats.Add(new Theaters._theater._stat { Revenue = 3000, Attendance = 40 });

            Assert.Equal(2000, theater.GetAvgRevenue());
            Assert.Equal(60, theater.GetAvgAttendance());
        }
    }
}
