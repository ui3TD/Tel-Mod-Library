using HarmonyLib;
using SisterGroups;
using System.Reflection;
using Xunit;
using static resources.fanType;
using static staticVars._playerData._difficulty;

namespace MoreSisterGroups.Tests
{
    /// <summary>
    /// "In Unfair, sister group new fans are reduced 5x", and further when the group has under 10 members
    /// (1 member gets 10%, 2 get 20%, and so on).
    /// </summary>
    public class NewFansTests
    {
        // 10 points each of male, casual and teen: 1300 new fans, x4 for casual fans
        private const int VanillaNewFans = 5200;

        private static int NewFans(staticVars._playerData._difficulty difficulty, int members, int graduated = 0)
        {
            TestGame.Reset(difficulty);
            Groups._group sister = TestGame.Group(members);
            for (int i = 0; i < graduated; i++)
                TestGame.Idol(sister, data_girls._status.graduated);
            TestGame.FanPoints(sister, 10, male, casual, teen);
            return sister.GetNewFansPerSingle(male, casual, teen);
        }

        [Theory]
        [InlineData(easy)]
        [InlineData(normal)]
        public void OutsideUnfair_NewFansAreUnchanged(staticVars._playerData._difficulty difficulty)
        {
            Assert.Equal(VanillaNewFans, NewFans(difficulty, members: 1));
            Assert.Equal(VanillaNewFans, NewFans(difficulty, members: 12));
        }

        [Theory]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(30)]
        public void Unfair_TenOrMoreMembers_GetAFifth(int members)
        {
            Assert.Equal(VanillaNewFans / 5, NewFans(hard, members));
        }

        [Theory]
        [InlineData(1, 10)]
        [InlineData(2, 20)]
        [InlineData(5, 50)]
        [InlineData(9, 90)]
        public void Unfair_UnderTenMembers_GetTenPercentPerMember(int members, int percent)
        {
            Assert.Equal(VanillaNewFans / 5 * percent / 100, NewFans(hard, members));
        }

        [Fact]
        public void Unfair_NoMembers_GetNoNewFans()
        {
            Assert.Equal(0, NewFans(hard, members: 0));
        }

        /// <summary>
        /// Graduated idols stay on a group's list but aren't members.
        /// </summary>
        [Fact]
        public void Unfair_GraduatedIdols_AreNotCounted()
        {
            Assert.Equal(VanillaNewFans / 5 * 4 / 10, NewFans(hard, members: 4, graduated: 6));
        }

        [Fact]
        public void Unfair_ResultIsRounded()
        {
            Groups._group group = TestGame.Reset(hard);
            TestGame.Idol(group);
            int result = 7;

            Groups__group_GetNewFansPerSingle.Postfix(ref result, group);

            Assert.Equal(0, result); // 7 / 5 / 10 = 0.14
            result = 33;
            Groups__group_GetNewFansPerSingle.Postfix(ref result, group);
            Assert.Equal(1, result); // 0.66
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
    }
}
