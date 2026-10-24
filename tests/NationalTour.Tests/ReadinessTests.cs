using System;
using System.Linq;
using Xunit;
using static NationalTour.Readiness;

namespace NationalTour.Tests
{
    /// <summary>
    /// A module that can't find what it patches turns itself off, and the features that need it stay off.
    /// </summary>
    public class ReadinessTests
    {
        public ReadinessTests() => TestGame.Reset();

        /// <summary>
        /// This build plans, creates and runs national tours; saving them comes in a later stage.
        /// </summary>
        [Fact]
        public void ThisBuild_HasEveryModuleButSaving()
        {
            Assert.All(Enum.GetValues(typeof(Module)).Cast<Module>().Where(m => m != Module.Save), m => Assert.True(IsReady(m), m.ToString()));
            Assert.False(IsReady(Module.Save));
        }

        [Fact]
        public void FailedModule_IsOff_AndSaysWhy()
        {
            using LogRecorder log = new();

            Fail(Module.NewTourMap, "no map");

            Assert.False(IsReady(Module.NewTourMap));
            Assert.False(AreReady(Module.Prefectures, Module.NewTourMap));
            Assert.True(AreReady(Module.Prefectures, Module.NationalRecord));
            Assert.Equal("no map", WhyFailed(Module.NewTourMap));
            string error = Assert.Single(log.Errors);
            Assert.Contains("NewTourMap is off: no map", error);
        }

        [Fact]
        public void FailingAgain_LogsOnce()
        {
            using LogRecorder log = new();

            Fail(Module.NationalTab, "first");
            Fail(Module.NationalTab, "second");

            Assert.Single(log.Errors);
            Assert.Equal("first", WhyFailed(Module.NationalTab));
        }

        [Fact]
        public void FailedPrefectures_TurnOffTheTab()
        {
            TestGame.LoadImages();
            TestGame.LoadCountries();
            Assert.True(Seams.CanOpenTab());
            using LogRecorder log = new();

            Fail(Module.Prefectures, "test");

            Assert.False(Seams.CanOpenTab());
        }
    }
}
