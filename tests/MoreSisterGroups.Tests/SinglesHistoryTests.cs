using HarmonyLib;
using SisterGroups;
using System;
using System.Collections.Generic;
using System.Reflection;
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

        /// <summary>
        /// Runs the action between the release step's prefix and postfix, as if inside the game's method.
        /// (Neither method runs outside the game.)
        /// </summary>
        private static void During(string step, singles._single single, Action action)
        {
            if (step == "AddOpinion")
                singles_AddOpinion.Prefix(single);
            else
                singles_GenerateSales.Prefix(single);
            try
            {
                action();
            }
            finally
            {
                if (step == "AddOpinion")
                    singles_AddOpinion.Postfix();
                else
                    singles_GenerateSales.Postfix();
            }
        }

        private static void SameSingles(IEnumerable<singles._single> expected, IEnumerable<singles._single> actual) =>
            Assert.Equal(expected, actual, TestGame.SameObject<singles._single>());

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

        [Theory]
        [MemberData(nameof(ReleaseSteps))]
        public void DuringRelease_OnlyTheSameGroupsSinglesCount(string step)
        {
            singles._single s1 = TestGame.Single(sister);
            TestGame.Single(other);
            singles._single s2 = TestGame.Single(sister);
            TestGame.Single(main);
            TestGame.Single(sister, status: working);
            singles._single s3 = TestGame.Single(sister);
            TestGame.Single(other);
            singles._single releasing = TestGame.Single(sister, status: working);

            During(step, releasing, () =>
                SameSingles(new[] { s3, s2, s1 }, singles.GetLatestReleasedSingles(3)));
        }

        [Theory]
        [MemberData(nameof(ReleaseSteps))]
        public void DuringRelease_OnlyTheLatestCountAreReturned(string step)
        {
            TestGame.Single(sister);
            singles._single s2 = TestGame.Single(sister);
            singles._single s3 = TestGame.Single(sister);
            singles._single s4 = TestGame.Single(sister);

            During(step, s4, () =>
                SameSingles(new[] { s4, s3, s2 }, singles.GetLatestReleasedSingles(3)));
        }

        /// <summary>
        /// A group's first single has no history to be penalised against, however many other groups have released.
        /// </summary>
        [Theory]
        [MemberData(nameof(ReleaseSteps))]
        public void DuringRelease_AGroupsFirstSingle_HasNoHistory(string step)
        {
            TestGame.Single(main);
            TestGame.Single(other);
            singles._single first = TestGame.Single(sister, status: working);

            During(step, first, () => Assert.Empty(singles.GetLatestReleasedSingles(3)));
        }

        /// <summary>
        /// The main group's singles (any not on a sister group's list) don't count sister groups' singles either.
        /// </summary>
        [Theory]
        [MemberData(nameof(ReleaseSteps))]
        public void DuringRelease_MainGroupIgnoresSisterGroups(string step)
        {
            singles._single m1 = TestGame.Single();
            TestGame.Single(sister);
            singles._single m2 = TestGame.Single(main);
            TestGame.Single(other);
            singles._single releasing = TestGame.Single(status: working);

            During(step, releasing, () =>
                SameSingles(new[] { m2, m1 }, singles.GetLatestReleasedSingles(3)));
        }

        /// <summary>
        /// Everything else that reads the latest singles (e.g. the chapter 1 story choice) still sees every group's.
        /// </summary>
        [Theory]
        [MemberData(nameof(ReleaseSteps))]
        public void AfterRelease_AllGroupsSinglesCountAgain(string step)
        {
            singles._single s1 = TestGame.Single(sister);
            singles._single o1 = TestGame.Single(other);
            singles._single m1 = TestGame.Single(main);

            During(step, s1, () => { });

            Assert.True(Utility.groupSales is null);
            SameSingles(new[] { m1, o1, s1 }, singles.GetLatestReleasedSingles(5));
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
