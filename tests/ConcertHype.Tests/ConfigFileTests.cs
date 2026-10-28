using ConcertHype.Model;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ConcertHype.Tests
{
    public class ConfigFileTests
    {
        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        private static string ShippedPath() => Path.Combine(RepoRoot(), "mods", "Concert Hype", "assets", ConfigFile.FileName);

        [Fact]
        public void Defaults_AreTheSpecNumbers()
        {
            Config config = new();
            Assert.Equal(new double[] { 15, 15, 10, 0.7 }, new[] { config.KAppeal, config.KSenbatsu, config.RollSigma, config.EnergyMinFactor });
            Assert.Equal(new double[] { 50, 0.25, 0.5, 60, 3 }, new[] { config.StartingHype, config.FanWeightFloor, config.KPerf, config.Par, config.GenreFlowBonus });
            Assert.Equal(new double[] { 20, 1.5, 0.5 }, new[] { config.NoveltyBlendBand, config.AmpHigh, config.AmpLow });
            Assert.Equal(new double[] { 1.5, 1.0, 0.6, 0.6, 0.6 }, config.RowWeights);
            Assert.Equal(new double[] { 8, 1, 2, 0.1, 10, 30 }, new[] { config.EnergyCostBase, config.KRollCost, config.HypeLiftBase, config.HypeLiftPerPoint, config.BenchRecovery, config.ReturnThreshold });
            Assert.Equal(new double[] { 0.005, 40, 0.01, 0.05, 0.5 }, new[] { config.PBase, config.AccidentRollThreshold, config.PRoll, config.PTired, config.PMax });
            Assert.Equal(new[] { 1, 3, 2, 1 }, new[] { config.AbsenceMin, config.AbsenceMax, config.McCharges, config.McTimeUnits });
            Assert.Equal(new double[] { 0.5, 10, 0.5, 0.25, 0.25 }, new[] { config.KMc, config.McRecovery, config.WAvg, config.WPeak, config.WEnd });
        }

        [Fact]
        public void ShippedFile_HoldsEverySettingAtItsDefault()
        {
            List<string> warnings = new();
            Config shipped = ConfigFile.Load(ShippedPath(), warnings.Add);
            Assert.Empty(warnings);
            Config defaults = new();
            foreach (FieldInfo field in typeof(Config).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.Equal(field.GetValue(defaults), field.GetValue(shipped));

            JSONClass root = (JSONClass)JSON.Parse(File.ReadAllText(ShippedPath()));
            List<string> keys = new();
            foreach (KeyValuePair<string, JSONNode> item in root)
                keys.Add(item.Key);
            Assert.Equal(typeof(Config).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => ConfigFile.CamelCase(f.Name)), keys);
        }

        [Fact]
        public void Defaults_ReadBackAsTheDefaults()
        {
            List<string> warnings = new();
            Config config = ConfigFile.Parse(ConfigFile.Defaults(), warnings.Add);
            Assert.Empty(warnings);
            Assert.Equal(new Config().RowCost, config.RowCost);
        }

        [Fact]
        public void Values_OverrideTheDefaults()
        {
            List<string> warnings = new();
            Config config = ConfigFile.Parse("{ \"par\": 55.5, \"mcCharges\": 3, \"rowWeights\": [2, 1], \"seed\": 42 }", warnings.Add);
            Assert.Empty(warnings);
            Assert.Equal(55.5, config.Par);
            Assert.Equal(3, config.McCharges);
            Assert.Equal(new double[] { 2, 1 }, config.RowWeights);
            Assert.Equal(1, config.RowWeight(4));  // rows past the list use its last value
            Assert.Equal(42, config.Seed);
        }

        [Fact]
        public void UnreadableOrUnknownValues_KeepTheDefaults_WithAWarning()
        {
            List<string> warnings = new();
            Config config = ConfigFile.Parse("{ \"par\": \"high\", \"mcCharges\": 2.5, \"rowCost\": [], \"rowWeights\": [1, \"x\"], \"colour\": 3 }", warnings.Add);
            Assert.Equal(60, config.Par);
            Assert.Equal(2, config.McCharges);
            Assert.Equal(new Config().RowCost, config.RowCost);
            Assert.Equal(new Config().RowWeights, config.RowWeights);
            Assert.Equal(5, warnings.Count);
            Assert.Contains(warnings, w => w.Contains("'colour'"));
        }

        [Fact]
        public void AMissingOrBrokenFile_UsesTheDefaults_WithAWarning()
        {
            List<string> warnings = new();
            Assert.Equal(60, ConfigFile.Load(Path.Combine(Path.GetTempPath(), "no-such-dir", ConfigFile.FileName), warnings.Add).Par);
            Assert.Equal(60, ConfigFile.Parse("[1, 2]", warnings.Add).Par);
            Assert.Equal(2, warnings.Count);
        }
    }
}
