using ConcertHype.Model;
using System.Linq;
using Xunit;

namespace ConcertHype.Tests
{
    public class OutcomesTests
    {
        private static readonly Config Bands = new Config();

        [Theory]
        [InlineData(0, Outcome.CriticalFailure)]
        [InlineData(39.99, Outcome.CriticalFailure)]
        [InlineData(40, Outcome.Failure)]
        [InlineData(59.99, Outcome.Failure)]
        [InlineData(60, Outcome.Success)]
        [InlineData(79.99, Outcome.Success)]
        [InlineData(80, Outcome.CriticalSuccess)]
        [InlineData(150, Outcome.CriticalSuccess)]
        public void TheOutcome_IsTheBandTheRollIsIn(double roll, Outcome outcome)
        {
            Assert.Equal(outcome, Outcomes.Of(Bands, roll));
        }

        [Fact]
        public void TheBands_ComeFromTheConfig()
        {
            Config config = new Config { AccidentRollThreshold = 30, Par = 50, CriticalSuccessRoll = 90 };
            Assert.Equal(Outcome.Failure, Outcomes.Of(config, 35));
            Assert.Equal(Outcome.Success, Outcomes.Of(config, 85));
            Assert.Equal(Outcome.CriticalSuccess, Outcomes.Of(config, 90));
        }

        [Fact]
        public void NormalCdf_MatchesKnownValues()
        {
            Assert.Equal(0.5, Rng.NormalCdf(0), 7);
            Assert.Equal(0.8413447, Rng.NormalCdf(1), 6);
            Assert.Equal(0.0227501, Rng.NormalCdf(-2), 6);
            Assert.Equal(0.9986501, Rng.NormalCdf(3), 6);
        }

        [Fact]
        public void Chances_AtPar_AreTheNormalShares()
        {
            double[] chances = Outcomes.Chances(Bands, 60);
            Assert.Equal(2.275, chances[(int)Outcome.CriticalFailure], 2);
            Assert.Equal(47.725, chances[(int)Outcome.Failure], 2);
            Assert.Equal(47.725, chances[(int)Outcome.Success], 2);
            Assert.Equal(2.275, chances[(int)Outcome.CriticalSuccess], 2);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(14)]
        [InlineData(45)]
        [InlineData(72)]
        [InlineData(130)]
        public void Chances_AddUpTo100_AndGrowWithTheExpectedRoll(double expected)
        {
            double[] chances = Outcomes.Chances(Bands, expected);
            Assert.Equal(100, chances.Sum(), 6);
            Assert.All(chances, c => Assert.InRange(c, 0, 100));
            double[] better = Outcomes.Chances(Bands, expected + 10);
            Assert.True(better[(int)Outcome.CriticalSuccess] >= chances[(int)Outcome.CriticalSuccess]);
            Assert.True(better[(int)Outcome.CriticalFailure] <= chances[(int)Outcome.CriticalFailure]);
        }

        [Fact]
        public void Chances_AreSymmetricAroundTheMiddleBand()
        {
            double[] low = Outcomes.Chances(Bands, 55), high = Outcomes.Chances(Bands, 65);
            Assert.Equal(low[(int)Outcome.CriticalFailure], high[(int)Outcome.CriticalSuccess], 6);
            Assert.Equal(low[(int)Outcome.Failure], high[(int)Outcome.Success], 6);
        }

        [Fact]
        public void WithoutSpread_TheExpectedRollsOutcomeIsCertain()
        {
            Config config = new Config { RollSigma = 0 };
            Assert.Equal(new double[] { 0, 0, 100, 0 }, Outcomes.Chances(config, 70));
            Assert.Equal(50, Outcomes.WheelPercent(config, 70, 70), 6);
        }

        /// <summary>
        /// The game's arcs from 0: success, critical success, critical failure, failure; the wheel stops inside
        /// the roll's own arc, never on its edge.
        /// </summary>
        [Theory]
        [InlineData(60, 0)]
        [InlineData(60, 39.99)]
        [InlineData(60, 40)]
        [InlineData(60, 59.99)]
        [InlineData(60, 60)]
        [InlineData(60, 79.99)]
        [InlineData(60, 80)]
        [InlineData(60, 120)]
        [InlineData(14, 10)]
        [InlineData(14, 95)]
        [InlineData(95, 3)]
        public void TheWheel_LandsInsideTheRollsOwnArc(double expected, double roll)
        {
            double[] chances = Outcomes.Chances(Bands, expected);
            Outcome outcome = Outcomes.Of(Bands, roll);
            double start = Outcomes.WheelOrder.TakeWhile(o => o != outcome).Sum(o => chances[(int)o]);
            double arc = chances[(int)outcome];
            double at = Outcomes.WheelPercent(Bands, expected, roll);
            Assert.InRange(at, start + 0.1 * arc - 1e-9, start + 0.9 * arc + 1e-9);
        }

        [Fact]
        public void TheWheel_LandsFurtherIntoTheArcForAHigherRoll()
        {
            Assert.True(Outcomes.WheelPercent(Bands, 60, 65) < Outcomes.WheelPercent(Bands, 60, 75));
        }
    }
}
