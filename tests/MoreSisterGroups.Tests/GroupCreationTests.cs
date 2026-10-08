using SisterGroups;
using Xunit;
using static Groups._group._status;

namespace MoreSisterGroups.Tests
{
    /// <summary>
    /// "Sister group minimum member limit for creation removed": the game needs 10 idols per active group
    /// before it allows another; the mod only needs one idol more than there are active groups.
    /// </summary>
    public class GroupCreationTests
    {
        /// <summary>
        /// Applies the mod before any test method is compiled. In Release, the JIT inlines the small
        /// GetIdolsNeededForNewGroup into a test method, which skips the patch if that test applied it.
        /// </summary>
        public GroupCreationTests()
        {
            TestGame.Reset();
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 3)]
        [InlineData(5, 6)]
        public void IdolsNeeded_IsOneMoreThanActiveGroups(int activeGroups, int needed)
        {
            TestGame.Reset();
            for (int i = 1; i < activeGroups; i++)
                TestGame.Group();

            Assert.Equal(needed, Groups.GetIdolsNeededForNewGroup());
        }

        [Fact]
        public void DisbandedGroups_AreNotCounted()
        {
            TestGame.Reset();
            TestGame.Group();
            TestGame.Group(status: disbanded);
            TestGame.Group(status: disbanded);

            Assert.Equal(3, Groups.GetIdolsNeededForNewGroup());
        }

        /// <summary>
        /// A new save with two idols can already make a sister group (the game needs 10).
        /// </summary>
        [Fact]
        public void TwoIdols_CanCreateTheFirstSisterGroup()
        {
            Groups._group main = TestGame.Reset();
            TestGame.Idol(main);
            Assert.False(Groups.CanCreateNewGroup());

            TestGame.Idol(main);
            Assert.True(Groups.CanCreateNewGroup());
        }

        /// <summary>
        /// Every group keeps at least one idol: with one idol per group there's no room for another.
        /// </summary>
        [Fact]
        public void OneIdolPerGroup_CannotCreateAnother()
        {
            Groups._group main = TestGame.Reset();
            TestGame.Idol(main);
            TestGame.Group(members: 1);
            TestGame.Group(members: 1);
            Assert.False(Groups.CanCreateNewGroup());

            TestGame.Idol(main);
            Assert.True(Groups.CanCreateNewGroup());
        }

        [Fact]
        public void GraduatedIdols_AreNotCounted()
        {
            Groups._group main = TestGame.Reset();
            TestGame.Idol(main);
            TestGame.Idol(main, data_girls._status.graduated);

            Assert.False(Groups.CanCreateNewGroup());
        }

        [Theory]
        [InlineData(staticVars._playerData._difficulty.easy)]
        [InlineData(staticVars._playerData._difficulty.normal)]
        [InlineData(staticVars._playerData._difficulty.hard)]
        public void LimitApplies_OnEveryDifficulty(staticVars._playerData._difficulty difficulty)
        {
            TestGame.Reset(difficulty);

            Assert.Equal(2, Groups.GetIdolsNeededForNewGroup());
        }

        [Fact]
        public void Postfix_ReplacesTheGamesValue()
        {
            TestGame.Reset();
            int result = 10;

            Groups_GetIdolsNeededForNewGroup.Postfix(ref result);

            Assert.Equal(2, result);
        }
    }
}
