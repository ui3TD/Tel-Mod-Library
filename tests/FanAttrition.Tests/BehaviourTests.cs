using Xunit;

namespace FanAttrition.Tests
{
    public class McFameTests
    {
        private static int Run(int? mcFame, int fans)
        {
            Shows._show show = new();
            if (mcFame.HasValue)
                show.mc = new Shows._param { fame = mcFame.Value };
            return Shows__show_SetSales_MC.Infix(show, fans);
        }

        /// <summary>
        /// Show fans scale by 1 + fame²/10, plus 5 at max fame.
        /// </summary>
        [Theory]
        [InlineData(0, 1000)]
        [InlineData(1, 1100)]
        [InlineData(2, 1400)]
        [InlineData(3, 1900)]
        [InlineData(5, 3500)]
        [InlineData(9, 9100)]
        [InlineData(10, 16000)]
        public void FansScaleWithMcFame(int fame, int expected)
        {
            Assert.Equal(expected, Run(fame, 1000));
        }

        [Fact]
        public void NoMc_LeavesFansUnchanged()
        {
            Assert.Equal(1000, Run(null, 1000));
        }
    }
}
