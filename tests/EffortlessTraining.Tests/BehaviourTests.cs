using Xunit;
using static agency._type;

namespace EffortlessTraining.Tests
{
    /// <summary>
    /// With the mod, a day of vocal or dance practice costs 1 physical stamina instead of the game's 3.
    /// </summary>
    public class TrainingCostTests
    {
        public TrainingCostTests() => TestGame.Reset(patched: true);

        [Theory]
        [InlineData(danceStudio)]
        [InlineData(recordingStudio)]
        public void DayOfPractice_CostsOneStamina(agency._type roomType)
        {
            data_girls.girls girl = TestGame.Idol();

            TestGame.Train(TestGame.TrainingRoom(roomType, girl), TestGame.TicksPerDay());

            Assert.Equal(99f, TestGame.Stamina(girl), 3);
        }

        /// <summary>
        /// The cost is spread over the day's ticks, so it stays 1 per day at any game speed.
        /// </summary>
        [Theory]
        [InlineData(25.0)]
        [InlineData(50.0)]
        [InlineData(100.0)]
        [InlineData(200.0)]
        public void DayOfPractice_CostsOneStamina_AtAnyGameSpeed(double minutesPerSecond)
        {
            staticVars.dateTimeAddMinutesPerSecond = minutesPerSecond;
            data_girls.girls girl = TestGame.Idol();

            TestGame.Train(TestGame.TrainingRoom(danceStudio, girl), TestGame.TicksPerDay());

            Assert.Equal(99f, TestGame.Stamina(girl), 3);
        }

        [Fact]
        public void WeekOfPractice_CostsSevenStamina()
        {
            data_girls.girls girl = TestGame.Idol();

            TestGame.Train(TestGame.TrainingRoom(recordingStudio, girl), 7 * TestGame.TicksPerDay());

            Assert.Equal(93f, TestGame.Stamina(girl), 2);
        }

        /// <summary>
        /// "3x less": the game's own multipliers still apply on top, so each case is a third of the game's cost.
        /// </summary>
        [Theory]
        [InlineData(policies._value.performances_quality, traits._trait._type.None, 1.3f)]
        [InlineData(policies._value.performances_neutral, traits._trait._type.Moonlighter, 5f)]
        [InlineData(policies._value.performances_quality, traits._trait._type.Moonlighter, 6.5f)]
        public void GameMultipliers_StillApply(policies._value performances, traits._trait._type trait, float dailyCost)
        {
            TestGame.UsePolicies(performances);
            data_girls.girls girl = TestGame.Idol(trait);

            TestGame.Train(TestGame.TrainingRoom(danceStudio, girl), TestGame.TicksPerDay());

            Assert.Equal(100f - dailyCost, TestGame.Stamina(girl), 3);
        }

        /// <summary>
        /// Style practice in the dressing room was already free, and stays free.
        /// </summary>
        [Fact]
        public void DressingRoom_StaysFree()
        {
            data_girls.girls girl = TestGame.Idol();

            TestGame.Train(TestGame.TrainingRoom(dressingRoom, girl), TestGame.TicksPerDay());

            Assert.Equal(100f, TestGame.Stamina(girl));
        }

        /// <summary>
        /// Only the stamina cost changes: the practiced stat still moves with the room's progress.
        /// </summary>
        [Fact]
        public void Practice_StillRaisesTheStat()
        {
            data_girls.girls girl = TestGame.Idol();
            agency._room room = TestGame.TrainingRoom(danceStudio, girl);
            room.Progress = 0.75f;

            TestGame.Train(room, 1);

            Assert.Equal(40.75f, girl.getParam(data_girls._paramType.dance).val, 3);
            Assert.Equal(40f, girl.getParam(data_girls._paramType.vocal).val);
        }
    }
}
