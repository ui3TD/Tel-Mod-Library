using ConcertHype.Model;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ConcertHype.Tests
{
    public class ConfigTests
    {
        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        [Fact]
        public void Defaults_AreTheShowsNumbers()
        {
            Config config = new();
            Assert.Equal(new double[] { 30, 0.7, 15, 10, 0.7 }, new[] { config.SkillBase, config.SkillScale, config.KSenbatsu, config.RollSigma, config.EnergyMinFactor });
            Assert.Equal(new double[] { 0, 4, 8, 12, 16 }, config.RowEase);
            Assert.Equal(new double[] { 50, 0.25, 0.5, 60 }, new[] { config.StartingHype, config.FanWeightFloor, config.KPerf, config.Par });
            Assert.Equal(new double[] { 15, 6, 2, 4 }, new[] { config.AppealPoints, config.AppealMax, config.FlowPoints, config.QualityBonus });
            Assert.Equal(new double[] { 500, 50000, 0.2, 1 }, new[] { config.QualitySeatsLow, config.QualitySeatsHigh, config.QualityNeededLow, config.QualityNeededHigh });
            Assert.Equal(new double[] { 10, 0.9, 15 }, new[] { config.VenueSoldOutBonus, config.VenueNeutralShare, config.VenueEmptyPenalty });
            Assert.Equal(new double[] { 20, 1.5, 0.5 }, new[] { config.NoveltyBlendBand, config.AmpHigh, config.AmpLow });
            Assert.Equal(new double[] { 1.5, 1.0, 0.6, 0.6, 0.6 }, config.RowWeights);
            Assert.Equal(new double[] { 8, 1, 10, 30 }, new[] { config.EnergyCostBase, config.KRollCost, config.BenchRecovery, config.ReturnThreshold });
            Assert.Equal(new double[] { 0.005, 40, 0.01, 0.05, 0.5 }, new[] { config.PBase, config.AccidentRollThreshold, config.PRoll, config.PTired, config.PMax });
            Assert.Equal(new[] { 1, 3, 2, 1 }, new[] { config.AbsenceMin, config.AbsenceMax, config.McCharges, config.McTimeUnits });
            Assert.Equal(new double[] { 0.5, 10, 0.5, 0.25, 0.25 }, new[] { config.KMc, config.McRecovery, config.WAvg, config.WPeak, config.WEnd });
        }
    }
}
