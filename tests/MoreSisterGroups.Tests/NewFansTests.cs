using HarmonyLib;
using SisterGroups;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using static resources.fanType;
using static staticVars._playerData._difficulty;

namespace MoreSisterGroups.Tests
{
    /// <summary>
    /// "In Unfair, sister group new fans are reduced 5x", then by the square of the group's size under 10 members
    /// (1 member gets 1%, 2 get 4%, 5 get 25%), by the share of its members singing, and by its own release
    /// cooldown (1 / (1 + its singles released in the past 30 days)), as the game does for sales.
    /// </summary>
    public class NewFansTests
    {
        // 10 points each of male, casual and teen: 1300 new fans, x4 for casual fans
        private const int VanillaNewFans = 5200;

        // A fifth: what a full-size group with every member singing gets
        private const int FullSize = VanillaNewFans / 5;

        private readonly Groups._group main;

        public NewFansTests()
        {
            main = TestGame.Reset(hard);
        }

        private static Groups._group Sister(int members)
        {
            Groups._group sister = TestGame.Group(members);
            TestGame.FanPoints(sister, 10, male, casual, teen);
            return sister;
        }

        private static int NewFans(Groups._group group) => group.GetNewFansPerSingle(male, casual, teen);

        /// <summary>
        /// A single in production for the group, sung by these idols (null for an empty senbatsu slot).
        /// </summary>
        private static singles._single Releasing(Groups._group group, params data_girls.girls[] senbatsu)
        {
            singles._single single = TestGame.Single(group, singles._single._status.working);
            single.girls.AddRange(senbatsu);
            return single;
        }

        private static singles._single Releasing(Groups._group group, int singing) =>
            Releasing(group, group.GetGirls().GetRange(0, singing).ToArray());

        /// <summary>
        /// New fans as the game works them out while generating the single's sales.
        /// </summary>
        private static int NewFansReleasing(singles._single single, Groups._group group = null)
        {
            singles_GenerateSales.Prefix(single, out Utility.ReleaseContext state);
            try
            {
                return NewFans(group ?? single.GetGroup());
            }
            finally
            {
                singles_GenerateSales.Finalizer(state);
            }
        }

        // ---------------------------------------------------------------- difficulty

        [Theory]
        [InlineData(easy)]
        [InlineData(normal)]
        public void OutsideUnfair_NewFansAreUnchanged(staticVars._playerData._difficulty difficulty)
        {
            TestGame.Reset(difficulty);
            Groups._group one = Sister(1);
            Groups._group twelve = Sister(12);
            TestGame.Released(twelve, daysAgo: 3);

            Assert.Equal(VanillaNewFans, NewFans(one));
            Assert.Equal(VanillaNewFans, NewFansReleasing(Releasing(twelve, singing: 1)));
        }

        // ---------------------------------------------------------------- size

        [Theory]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(30)]
        public void TenOrMoreMembers_GetAFifth(int members)
        {
            Assert.Equal(FullSize, NewFans(Sister(members)));
        }

        [Theory]
        [InlineData(1, 10)]   // 1040 x 1%
        [InlineData(2, 42)]   // 4%: 41.6
        [InlineData(5, 260)]  // 25%
        [InlineData(9, 842)]  // 81%: 842.4
        public void UnderTenMembers_GetTheSquareOfTheirShare(int members, int expected)
        {
            Assert.Equal(expected, NewFans(Sister(members)));
        }

        [Fact]
        public void NoMembers_GetNoNewFans()
        {
            Assert.Equal(0, NewFans(Sister(0)));
        }

        /// <summary>
        /// Graduated idols stay on a group's list but aren't members.
        /// </summary>
        [Fact]
        public void GraduatedIdols_AreNotCounted()
        {
            Groups._group sister = Sister(4);
            for (int i = 0; i < 6; i++)
                TestGame.Idol(sister, data_girls._status.graduated);

            Assert.Equal(166, NewFans(sister)); // 16%: 166.4
        }

        // ---------------------------------------------------------------- singers

        [Theory]
        [InlineData(1, FullSize / 10)]
        [InlineData(5, FullSize / 2)]
        [InlineData(10, FullSize)]
        public void OnlyTheMembersSinging_Count(int singing, int expected)
        {
            Groups._group sister = Sister(10);

            Assert.Equal(expected, NewFansReleasing(Releasing(sister, singing)));
        }

        [Fact]
        public void EmptySenbatsuSlots_AreIgnored()
        {
            Groups._group sister = Sister(10);
            List<data_girls.girls> girls = sister.GetGirls();

            Assert.Equal(FullSize / 5, NewFansReleasing(Releasing(sister, girls[0], null, girls[1], null)));
        }

        [Fact]
        public void GuestsFromOtherGroups_DontCount()
        {
            Groups._group sister = Sister(10);
            Groups._group other = Sister(10);

            Assert.Equal(FullSize / 10, NewFansReleasing(Releasing(sister, sister.GetGirls()[0], other.GetGirls()[0], TestGame.Idol(main))));
        }

        /// <summary>
        /// Can't happen in the game; a mod that lets an idol sing twice still gets no more than the full amount.
        /// </summary>
        [Fact]
        public void MoreSingersThanMembers_IsCappedAtEveryMember()
        {
            Groups._group sister = Sister(2);
            data_girls.girls a = sister.GetGirls()[0], b = sister.GetGirls()[1];

            Assert.Equal(42, NewFansReleasing(Releasing(sister, a, b, a)));
        }

        /// <summary>
        /// The group appeal popup shows new fans with no single: every member counts as singing.
        /// </summary>
        [Fact]
        public void OutsideARelease_EveryMemberCountsAsSinging()
        {
            Groups._group sister = Sister(10);
            Releasing(sister, singing: 1);

            Assert.Equal(FullSize, NewFans(sister));
        }

        /// <summary>
        /// A mod reading another group's new fans while a single is released: that single's singers aren't this group's.
        /// </summary>
        [Fact]
        public void DuringAnotherGroupsRelease_EveryMemberCountsAsSinging()
        {
            Groups._group sister = Sister(10);
            Groups._group other = Sister(10);

            Assert.Equal(FullSize, NewFansReleasing(Releasing(other, singing: 1), sister));
        }

        // ---------------------------------------------------------------- release frequency

        [Theory]
        [InlineData(0, FullSize)]
        [InlineData(1, FullSize / 2)]
        [InlineData(2, 347)]   // a third: 346.67
        public void RecentSinglesOfTheGroup_CutNewFans(int recent, int expected)
        {
            Groups._group sister = Sister(10);
            for (int i = 0; i < recent; i++)
                TestGame.Released(sister, daysAgo: 5 + i);

            Assert.Equal(expected, NewFansReleasing(Releasing(sister, singing: 10)));
        }

        [Fact]
        public void TheCooldownIsThirtyDays()
        {
            Groups._group sister = Sister(10);
            TestGame.Released(sister, daysAgo: 31);
            Assert.Equal(FullSize, NewFansReleasing(Releasing(sister, singing: 10)));

            TestGame.Released(sister, daysAgo: 30);
            Assert.Equal(FullSize / 2, NewFansReleasing(Releasing(sister, singing: 10)));
        }

        /// <summary>
        /// Each group has its own cooldown: the main group's and other sister groups' singles don't count.
        /// </summary>
        [Fact]
        public void OtherGroupsSingles_DontCount()
        {
            Groups._group sister = Sister(10);
            Groups._group other = Sister(10);
            TestGame.Released(main, daysAgo: 2);
            TestGame.Released(other, daysAgo: 3);

            Assert.Equal(FullSize, NewFansReleasing(Releasing(sister, singing: 10)));
        }

        [Fact]
        public void TwoSisterGroupsReleasingTheSameWeek_DontPenaliseEachOther()
        {
            Groups._group a = Sister(10);
            Groups._group b = Sister(10);

            singles._single first = Releasing(a, singing: 10);
            Assert.Equal(FullSize, NewFansReleasing(first));
            first.status = singles._single._status.released;
            first.ReleaseData.ReleaseDate = staticVars.dateTime;

            staticVars.dateTime = staticVars.dateTime.AddDays(3);
            Assert.Equal(FullSize, NewFansReleasing(Releasing(b, singing: 10)));
        }

        [Fact]
        public void OutsideARelease_TheCooldownStillApplies()
        {
            Groups._group sister = Sister(10);
            TestGame.Released(sister, daysAgo: 4);

            Assert.Equal(FullSize / 2, NewFans(sister));
        }

        // ---------------------------------------------------------------- together

        [Fact]
        public void FactorsCombine_AndRoundOnce()
        {
            Groups._group sister = Sister(5);
            TestGame.Released(sister, daysAgo: 10);

            // 1040 x 25% size x 2/5 singing x 1/2 cooldown
            Assert.Equal(52, NewFansReleasing(Releasing(sister, singing: 2)));
        }

        [Fact]
        public void ResultIsRounded()
        {
            Groups._group sister = Sister(10);
            int result = 7;

            Groups__group_GetNewFansPerSingle.Postfix(ref result, sister);

            Assert.Equal(1, result); // 1.4
            result = 8;
            Groups__group_GetNewFansPerSingle.Postfix(ref result, sister);
            Assert.Equal(2, result); // 1.6
        }

        /// <summary>
        /// Only sister groups are nerfed: the game's sales use this method only for non-main groups,
        /// working the main group's new fans out from fame instead.
        /// </summary>
        [Fact]
        public void GameSales_UseThisMethodOnlyForSisterGroups()
        {
            MethodInfo sales = AccessTools.Method(typeof(singles), nameof(singles.GenerateSales));
            Assert.True(TestGame.Calls(sales, AccessTools.Method(typeof(Groups._group), nameof(Groups._group.IsMain))));
            Assert.True(TestGame.Calls(sales, AccessTools.Method(typeof(Groups._group), nameof(Groups._group.GetNewFansPerSingle))));
        }

        /// <summary>
        /// The game's sales use the same per-group cooldown, which the new fans follow.
        /// </summary>
        [Fact]
        public void GameSales_UseTheGroupsCooldown()
        {
            MethodInfo sales = AccessTools.Method(typeof(singles), nameof(singles.GenerateSales));
            Assert.True(TestGame.Calls(sales, AccessTools.Method(typeof(singles), nameof(singles.GetSaturationCoeff))));
        }

        // ---------------------------------------------------------------- other mods

        [Fact]
        public void NestedRelease_KeepsTheOuterSingle()
        {
            Groups._group sister = Sister(10);
            singles._single outer = Releasing(sister, singing: 1);
            singles._single inner = Releasing(Sister(10), singing: 10);

            singles_GenerateSales.Prefix(outer, out Utility.ReleaseContext outerState);
            singles_GenerateSales.Prefix(inner, out Utility.ReleaseContext innerState);
            singles_GenerateSales.Finalizer(innerState);

            Assert.Same(outer, Utility.singleSales);
            Assert.Equal(FullSize / 10, NewFans(sister));

            singles_GenerateSales.Finalizer(outerState);
            Assert.Null(Utility.singleSales);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GenerateSalesStandIn(singles._single single) => throw new InvalidOperationException("original ran");

        public static bool SkipOriginal() => false;

        /// <summary>
        /// Another mod's prefix replaces the release step: the context is still cleared afterwards.
        /// </summary>
        [Fact]
        public void AnotherModSkippingTheReleaseStep_LeavesNoContext()
        {
            singles._single single = Releasing(Sister(10), singing: 1);
            Harmony harmony = new(TestGame.HarmonyId + ".SkipRelease");
            try
            {
                MethodInfo standIn = AccessTools.Method(typeof(NewFansTests), nameof(GenerateSalesStandIn));
                harmony.Patch(standIn, prefix: new HarmonyMethod(AccessTools.Method(typeof(NewFansTests), nameof(SkipOriginal))) { priority = Priority.First });
                harmony.Patch(standIn,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(singles_GenerateSales), "Prefix")),
                    finalizer: new HarmonyMethod(AccessTools.Method(typeof(singles_GenerateSales), "Finalizer")));

                GenerateSalesStandIn(single);
            }
            finally
            {
                harmony.UnpatchSelf();
            }

            Assert.Null(Utility.singleSales);
        }

        private static float frequencyOverride;

        public static void OverrideFrequency(ref float __result) => __result = frequencyOverride;

        private static int WithFrequency(float value, Func<int> action)
        {
            frequencyOverride = value;
            Harmony harmony = new(TestGame.HarmonyId + ".Frequency");
            try
            {
                harmony.Patch(AccessTools.Method(typeof(singles), nameof(singles.GetSaturationCoeff)),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(NewFansTests), nameof(OverrideFrequency))));
                return action();
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }

        /// <summary>
        /// A mod that changes the game's frequency curve changes sister groups' the same way.
        /// </summary>
        [Fact]
        public void AnotherModsFrequencyCurve_IsFollowed()
        {
            Groups._group sister = Sister(10);

            Assert.Equal(FullSize / 4, WithFrequency(0.25f, () => NewFans(sister)));
        }

        [Fact]
        public void AnInvalidFrequency_IsIgnoredAndLogged()
        {
            Groups._group sister = Sister(10);
            using LogRecorder log = new();

            Assert.Equal(FullSize, WithFrequency(float.NaN, () => NewFans(sister)));
            Assert.Equal(FullSize, WithFrequency(-1f, () => NewFans(sister)));
            Assert.Contains(log.Messages, m => m.StartsWith("[More Sister Groups] The release frequency coefficient is"));
        }

        public static void HalveNewFans(ref int __result) => __result /= 2;

        /// <summary>
        /// Another mod's multiplier on the same method gives the same result whichever postfix runs first.
        /// </summary>
        [Theory]
        [InlineData(Priority.First)]
        [InlineData(Priority.Last)]
        public void AnotherModsMultiplier_CombinesInEitherOrder(int priority)
        {
            Groups._group sister = Sister(10);
            Harmony harmony = new(TestGame.HarmonyId + ".Multiplier");
            try
            {
                harmony.Patch(AccessTools.Method(typeof(Groups._group), nameof(Groups._group.GetNewFansPerSingle)),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(NewFansTests), nameof(HalveNewFans))) { priority = priority });

                Assert.Equal(FullSize / 2, NewFans(sister));
            }
            finally
            {
                harmony.UnpatchSelf();
            }
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
