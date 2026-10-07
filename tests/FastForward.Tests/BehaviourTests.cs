using System.Globalization;
using System.Threading;
using Xunit;

namespace FastForward.Tests
{
    // The OnClick/Update patches reference Camera.main and Input (Unity ECalls),
    // so they can't be JIT-compiled outside the game and aren't tested here.
    public class MultiplierTests
    {
        // variables.Set needs the game running, so edit the list directly.
        private static void SetMultiplier(string value)
        {
            variables.variable.RemoveAll(v => v.name == FastForward.VARID);
            if (value != null)
                variables.variable.Add(new variables._variable { name = FastForward.VARID, value = value });
        }

        [Theory]
        [InlineData(null, 5d)]
        [InlineData("", 5d)]
        [InlineData("  ", 5d)]
        [InlineData("abc", 5d)]
        [InlineData("2,5", 5d)]
        [InlineData("NaN", 5d)]
        [InlineData("Infinity", 5d)]
        [InlineData("10", 10d)]
        [InlineData("2.5", 2.5d)]
        [InlineData("0.5", 1d)]
        [InlineData("-3", 1d)]
        [InlineData("28", 28d)]
        [InlineData("28.8", 28d)]
        [InlineData("100", 28d)]
        public void MultiplierIsParsedAndClamped(string raw, double expected)
        {
            SetMultiplier(raw);
            Assert.Equal(expected, FastForward.GetConfiguredMultiplier());
        }

        /// <summary>
        /// A dot decimal must not be read as a thousands separator on e.g. German Windows.
        /// </summary>
        [Fact]
        public void DotDecimal_IgnoresRegionalFormat()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                SetMultiplier("2.5");
                Assert.Equal(2.5d, FastForward.GetConfiguredMultiplier());
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void SpeedIsVanillaFastTimesMultiplier()
        {
            SetMultiplier("10");
            Assert.Equal(2000d, FastForward.GetConfiguredSpeed());
        }

        [Fact]
        public void ApplySuperFast_NullMain_DoesNothing()
        {
            staticVars.dateTimeAddMinutesPerSecond = 200d;
            FastForward.ApplySuperFast(null);
            Assert.Equal(200d, staticVars.dateTimeAddMinutesPerSecond);
        }
    }
}
