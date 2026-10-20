using System.Collections.Generic;
using System.Linq;
using Xunit;
using static resources.fanType;

namespace FanAttrition.Tests
{
    /// <summary>
    /// Which fan groups lose the day's fans, and how new fans are split between them (FanSplit).
    /// At stats of 50 an idol's stat appeal is the same for both genders of a group, so groups that differ only in
    /// gender weigh the same unless their opinion differs.
    /// </summary>
    public class FanSplitTests
    {
        public FanSplitTests() => TestGame.Reset();

        private static data_girls.girls Idol(params resources._fan[] fans) => TestGame.Famous(TestGame.Idol(0.5f, fans), 100f);

        private static resources._fan Group(resources.fanType gender, resources.fanType hardcoreness, long people, float opinion = 0.5f)
        {
            resources._fan fan = TestGame.Fans(gender, hardcoreness, teen, people);
            fan.Ratio = opinion;
            return fan;
        }

        // ---------------------------------------------------------------- losses

        /// <summary>
        /// Equal weights: 400 fans come off 30,000 and 10,000 as 300 and 100.
        /// </summary>
        [Fact]
        public void Loss_IsBySize_WhenWeightsAreEqual()
        {
            resources._fan big = Group(male, casual, 30000), small = Group(female, casual, 10000);
            data_girls.girls girl = Idol(big, small);

            Assert.Equal(400, FanSplit.TakeFromSegments(girl, 400));

            Assert.Equal(new long[] { 30000 - 300, 10000 - 100 }, new[] { big.people, small.people });
        }

        /// <summary>
        /// Same size, opinion +100 against -100: weights 1.505 and 0.505 give factors 0.7475 and 1.2475, so 1,000
        /// fans split 374 / 626 (the last fan goes to the group that pulls hardest).
        /// </summary>
        [Fact]
        public void Loss_LowOpinionLosesMore()
        {
            resources._fan liked = Group(male, casual, 10000, 1f), disliked = Group(female, casual, 10000, 0f);
            data_girls.girls girl = Idol(liked, disliked);

            FanSplit.TakeFromSegments(girl, 1000);

            Assert.Equal(new long[] { 10000 - 374, 10000 - 626 }, new[] { liked.people, disliked.people });
        }

        /// <summary>
        /// Higher stat appeal loses less: raising the idol's cute and cool stats, which hardcore fans like, makes her
        /// hardcore group lose fewer than her casual group of the same size.
        /// </summary>
        [Fact]
        public void Loss_HigherStatAppealLosesLess()
        {
            resources._fan hard = Group(male, hardcore, 10000), soft = Group(male, casual, 10000);
            data_girls.girls girl = Idol(hard, soft);
            foreach (data_girls.girls.param p in girl.parameters.Where(p => p.type == data_girls._paramType.cute || p.type == data_girls._paramType.cool))
                p.val = 100f;

            Assert.True(FanSplit.StatAppeal(girl, hard) > FanSplit.StatAppeal(girl, soft));
            FanSplit.TakeFromSegments(girl, 1000);

            Assert.True(10000 - hard.people < 10000 - soft.people);
            Assert.Equal(20000 - 1000, hard.people + soft.people);
        }

        [Fact]
        public void Loss_GroupWithNoFans_IsPaidByTheOthers()
        {
            resources._fan empty = Group(male, casual, 0), full = Group(female, casual, 5000);
            data_girls.girls girl = Idol(empty, full);

            Assert.Equal(300, FanSplit.TakeFromSegments(girl, 300));

            Assert.Equal(new long[] { 0, 5000 - 300 }, new[] { empty.people, full.people });
        }

        [Fact]
        public void Loss_IdolWithTooFewFans_LosesThemAll()
        {
            resources._fan a = Group(male, casual, 60), b = Group(female, casual, 40);
            data_girls.girls girl = Idol(a, b);

            Assert.Equal(100, FanSplit.TakeFromSegments(girl, 300));

            Assert.Equal(new long[] { 0, 0 }, new[] { a.people, b.people });
        }

        [Fact]
        public void Loss_NoVotes_CountAsNeutral()
        {
            resources._fan noVotes = Group(male, casual, 10000, float.NaN), neutral = Group(female, casual, 10000, 0.5f);
            data_girls.girls girl = Idol(noVotes, neutral);

            FanSplit.TakeFromSegments(girl, 1000);

            Assert.Equal(noVotes.people, neutral.people);
        }

        [Fact]
        public void Loss_IdolWithoutFanList_TakesNothing()
        {
            data_girls.girls girl = Idol();
            girl.Fans = null;

            Assert.Equal(0, FanSplit.TakeFromSegments(girl, 100));
        }

        /// <summary>
        /// The day's whole churn is taken even when most groups are empty, which the game's own AddFans skipped,
        /// and the fan display is updated once. 100,000 fans churn 68 a day in Normal.
        /// </summary>
        [Fact]
        public void Churn_TakesTheWholeLoss_WhenGroupsAreEmpty()
        {
            resources._fan full = Group(male, casual, 100000);
            Idol(full, Group(female, casual, 0), Group(male, hardcore, 0), Group(female, hardcore, 0));

            Utility.DailyFanChurn();

            Assert.Equal(100000 - 68, full.people);
            Assert.Equal(1, Seams.FanDisplayUpdates);
        }

        /// <summary>
        /// The stat appeal is the game's own: RecalcFanAppeal stores the same value.
        /// </summary>
        [Fact]
        public void StatAppeal_MatchesTheGame()
        {
            resources._fan[] fans = { TestGame.Fans(male, hardcore, teen, 1), TestGame.Fans(female, casual, adult, 1), TestGame.Fans(male, casual, youngAdult, 1) };
            data_girls.girls girl = Idol(fans);
            float v = 20f;
            foreach (data_girls.girls.param p in girl.parameters.Where(p => p.type != data_girls._paramType.famePoints))
                p.val = v += 9f;

            float[] ours = fans.Select(f => FanSplit.StatAppeal(girl, f)).ToArray();
            girl.RecalcFanAppeal();

            Assert.Equal(fans.Select(f => f.appeal).ToArray(), ours, new FloatClose());
        }

        // ---------------------------------------------------------------- gains

        private static void Gain(resources._fan fan, long fans) => fan.AddPeople(fans);

        /// <summary>
        /// 1,000 new fans each to a casual and a hardcore group: hardcore gets 0.75 of its slice, 2,000 x 0.75 / 1.75
        /// = 857.1, so 857 / 1,143.
        /// </summary>
        [Fact]
        public void Gain_HardcoreJoinsAtThreeQuarters()
        {
            resources._fan soft = Group(male, casual, 5000), hard = Group(male, hardcore, 5000);
            data_girls.girls girl = Idol(soft, hard);
            List<FanSplit.Before> before = FanSplit.Capture(new[] { girl });
            Gain(soft, 1000);
            Gain(hard, 1000);

            FanSplit.Respread(before, weighted: false);

            Assert.Equal(new long[] { 5000 + 1143, 5000 + 857 }, new[] { soft.people, hard.people });
        }

        /// <summary>
        /// A release's new fans also follow the idol's weight: opinion +100 against -100 (weights 1.505 and 0.505)
        /// turns 1,000 + 1,000 into 2,000 x 1.505 / 2.011 = 1,497.3 and 502.7, so 1,497 + 503.
        /// </summary>
        [Fact]
        public void Gain_FromReleases_FollowsOpinionAndStatAppeal()
        {
            resources._fan liked = Group(male, casual, 5000, 1f), disliked = Group(female, casual, 5000, 0f);
            data_girls.girls girl = Idol(liked, disliked);
            List<FanSplit.Before> before = FanSplit.Capture(new[] { girl });
            Gain(liked, 1000);
            Gain(disliked, 1000);

            FanSplit.Respread(before, weighted: true);

            Assert.Equal(new long[] { 5000 + 1497, 5000 + 503 }, new[] { liked.people, disliked.people });
        }

        [Fact]
        public void Gain_OtherSources_KeepTheGamesWeightSplit()
        {
            resources._fan liked = Group(male, casual, 5000, 1f), disliked = Group(female, casual, 5000, 0f);
            data_girls.girls girl = Idol(liked, disliked);
            List<FanSplit.Before> before = FanSplit.Capture(new[] { girl });
            Gain(liked, 700);
            Gain(disliked, 300);

            FanSplit.Respread(before, weighted: false);

            Assert.Equal(new long[] { 5700, 5300 }, new[] { liked.people, disliked.people });
        }

        [Fact]
        public void Gain_GroupsThatGainedNothing_StillGainNothing()
        {
            resources._fan gained = Group(male, hardcore, 5000), untouched = Group(male, casual, 5000);
            data_girls.girls girl = Idol(gained, untouched);
            List<FanSplit.Before> before = FanSplit.Capture(new[] { girl });
            Gain(gained, 1000);

            FanSplit.Respread(before, weighted: true);

            Assert.Equal(new long[] { 6000, 5000 }, new[] { gained.people, untouched.people });
        }

        /// <summary>
        /// Every idol keeps her own total, and the rounding remainder is placed, for any split.
        /// </summary>
        [Theory]
        [InlineData(1, 0, 0)]
        [InlineData(7, 3, 1)]
        [InlineData(333, 333, 334)]
        [InlineData(100000, 1, 50)]
        public void Gain_KeepsEachIdolsTotal(long a, long b, long c)
        {
            resources._fan x = Group(male, hardcore, 10, 0.9f), y = Group(female, casual, 10, 0.1f), z = Group(female, hardcore, 10);
            data_girls.girls first = Idol(x, y, z);
            resources._fan other = Group(male, casual, 10);
            data_girls.girls second = Idol(other);
            List<FanSplit.Before> before = FanSplit.Capture(new[] { first, second });
            Gain(x, a);
            Gain(y, b);
            Gain(z, c);
            Gain(other, 5);

            FanSplit.Respread(before, weighted: true);

            Assert.Equal(30 + a + b + c, x.people + y.people + z.people);
            Assert.Equal(15, other.people);
        }

        [Fact]
        public void Gain_TypedToHardcoreOnly_IsLeftAlone()
        {
            resources._fan a = Group(male, hardcore, 100), b = Group(female, hardcore, 100);
            data_girls.girls girl = Idol(a, b);
            List<FanSplit.Before> before = FanSplit.Capture(new[] { girl });
            Gain(a, 30);
            Gain(b, 70);

            FanSplit.Respread(before, weighted: false);

            Assert.Equal(new long[] { 130, 170 }, new[] { a.people, b.people });
        }

        /// <summary>
        /// Losses through the game's AddFans (scandals, events) are not touched.
        /// </summary>
        [Fact]
        public void AddFansPatch_LeavesLossesAlone()
        {
            data_girls.girls girl = Idol(Group(male, casual, 100));

            data_girls_girls_AddFans_Split.Prefix(girl, -50, out List<FanSplit.Before> state);

            Assert.Null(state);
        }

        [Fact]
        public void Capture_SkipsMissingIdolsAndFanLists()
        {
            data_girls.girls noList = Idol();
            noList.Fans = null;

            List<FanSplit.Before> before = FanSplit.Capture(new data_girls.girls[] { null, noList });

            Assert.Empty(before);
            FanSplit.Respread(before, weighted: true);
            FanSplit.Respread(null, weighted: true);
        }

        private class FloatClose : IEqualityComparer<float>
        {
            public bool Equals(float a, float b) => System.Math.Abs(a - b) < 1e-5f;
            public int GetHashCode(float f) => 0;
        }
    }
}
