using Xunit;
using Patch = MBTIPersonalities.SEvent_Concerts__concert_AccidentSuccessChance;

namespace MBTIPersonalities.Tests
{
    public class ISTPAccidentTests
    {
        /// <summary>
        /// ISTP halves the chance of failing an accident: the success chance closes half the gap to 100%.
        /// </summary>
        [Theory]
        [InlineData(0, 50)]
        [InlineData(40, 70)]
        [InlineData(60, 80)]
        [InlineData(90, 95)]
        [InlineData(100, 100)]
        public void HalvesTheFailureChance(int successChance, int expected)
        {
            Assert.Equal(expected, Patch.ApplyISTPBonus(successChance));
        }

        /// <summary>
        /// The bonus never lowers the success chance or pushes it past 100%.
        /// </summary>
        [Fact]
        public void NeverWorseThanWithoutISTP()
        {
            for (int chance = 0; chance <= 100; chance++)
            {
                int result = Patch.ApplyISTPBonus(chance);
                Assert.InRange(result, chance, 100);
            }
        }

        /// <summary>
        /// Odd failure chances leave half a percent, which rounds to the nearest whole percent.
        /// </summary>
        [Fact]
        public void FailureChanceIsHalvedToWithinRounding()
        {
            for (int chance = 0; chance <= 100; chance++)
            {
                float failure = 100 - Patch.ApplyISTPBonus(chance);
                Assert.InRange(failure, (100 - chance) / 2f - 0.5f, (100 - chance) / 2f + 0.5f);
            }
        }
    }
}
