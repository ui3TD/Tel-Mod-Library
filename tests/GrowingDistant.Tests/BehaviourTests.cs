using HarmonyLib;
using System;
using Xunit;
using Patch = GrowingDistant.data_girls_girls_UpdateRelationshipBasedOnSalary;

namespace GrowingDistant.Tests
{
    /// <summary>
    /// Salary satisfaction depends on the whole roster and earnings history,
    /// so the tests stub it with a Harmony prefix for the duration of the class.
    /// </summary>
    public sealed class SatisfactionStub : IDisposable
    {
        public static float Value;

        private readonly Harmony harmony = new("tests.GrowingDistant.SatisfactionStub");

        public SatisfactionStub()
        {
            harmony.Patch(
                AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.GetSalarySatisfaction)),
                prefix: new HarmonyMethod(typeof(SatisfactionStub), nameof(Prefix)));
        }

        private static bool Prefix(ref float __result)
        {
            __result = Value;
            return false;
        }

        public void Dispose()
        {
            harmony.UnpatchSelf();
        }
    }

    [Collection(Collection)]
    public class WeeklyDecayTests : IClassFixture<SatisfactionStub>
    {
        // Shared with PatchTargetTests so the two never patch the game at the same time.
        public const string Collection = "GrowingDistant";

        private static data_girls.girls Run(int satisfactionPercent, int influence = 100, int romance = 100, int friendship = 100,
            staticVars._playerData._difficulty difficulty = staticVars._playerData._difficulty.normal)
        {
            staticVars.PlayerData.Difficulty = difficulty;
            SatisfactionStub.Value = satisfactionPercent / 100f;
            data_girls.girls girl = new()
            {
                Rel_Influence_Points = influence,
                Rel_Romance_Points = romance,
                Rel_Friendship_Points = friendship
            };
            Patch.Postfix(ref girl);
            return girl;
        }

        /// <summary>
        /// Influence: +1 at 150%+ satisfaction, -1 at 100% or less, -2 at 50% or less.
        /// </summary>
        [Theory]
        [InlineData(200, 101)]
        [InlineData(150, 101)]
        [InlineData(125, 100)]
        [InlineData(100, 99)]
        [InlineData(75, 99)]
        [InlineData(50, 98)]
        [InlineData(0, 98)]
        public void InfluenceFollowsSalarySatisfaction(int satisfaction, int expected)
        {
            Assert.Equal(expected, Run(satisfaction).Rel_Influence_Points);
        }

        [Theory]
        [InlineData(511, 512)]
        [InlineData(512, 512)]
        public void InfluenceBonus_StopsAtMax(int influence, int expected)
        {
            Assert.Equal(expected, Run(200, influence).Rel_Influence_Points);
        }

        /// <summary>
        /// Saves aren't migrated: an idol already over the max drops to it at her next well-paid week...
        /// </summary>
        [Fact]
        public void InfluenceAboveMax_WellPaid_DropsToMax()
        {
            Assert.Equal(512, Run(200, 600).Rel_Influence_Points);
        }

        /// <summary>
        /// ...and otherwise only loses the usual penalty.
        /// </summary>
        [Fact]
        public void InfluenceAboveMax_Underpaid_TakesNormalPenalty()
        {
            Assert.Equal(599, Run(100, 600).Rel_Influence_Points);
        }

        [Theory]
        [InlineData(50, 0, 0)]
        [InlineData(50, 1, 0)]
        [InlineData(50, 2, 0)]
        [InlineData(100, 0, 0)]
        [InlineData(100, 1, 0)]
        public void InfluencePenalty_NeverGoesNegative(int satisfaction, int influence, int expected)
        {
            Assert.Equal(expected, Run(satisfaction, influence).Rel_Influence_Points);
        }

        [Fact]
        public void RomanceLosesOne_FriendshipLosesTwo()
        {
            data_girls.girls girl = Run(125);
            Assert.Equal(99, girl.Rel_Romance_Points);
            Assert.Equal(98, girl.Rel_Friendship_Points);
        }

        [Fact]
        public void RomanceAndFriendship_NeverGoNegative()
        {
            data_girls.girls girl = Run(125, romance: 0, friendship: 1);
            Assert.Equal(0, girl.Rel_Romance_Points);
            Assert.Equal(1, girl.Rel_Friendship_Points);
        }

        [Fact]
        public void EasyMode_ChangesNothing()
        {
            data_girls.girls girl = Run(0, difficulty: staticVars._playerData._difficulty.easy);
            Assert.Equal(100, girl.Rel_Influence_Points);
            Assert.Equal(100, girl.Rel_Romance_Points);
            Assert.Equal(100, girl.Rel_Friendship_Points);
        }

        [Fact]
        public void HardMode_Decays()
        {
            Assert.Equal(98, Run(0, difficulty: staticVars._playerData._difficulty.hard).Rel_Influence_Points);
        }
    }
}
