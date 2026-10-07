using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using WorkerRights;
using Xunit;
using static staticVars._playerData._difficulty;
using Mod = WorkerRights.WorkerRights;

// Every test shares the game's static state (idols, policies, date, difficulty).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WorkerRightsTests
{
    /// <summary>
    /// Builds the minimal game state the salary code reads: one idol, the salary policy and the difficulty.
    /// </summary>
    public static class TestGame
    {
        public static readonly DateTime Today = new(2023, 6, 5);
        public static readonly DateTime GraduationDate = new(2026, 1, 1);

        private static readonly Lazy<bool> Patched = new(() =>
        {
            // Not "tests.WorkerRights": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.WorkerRights.Behaviour");
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(staff__staff_CanFire).Assembly, "WorkerRights"))
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        /// <summary>
        /// Resets the game state. With patched set, the mod is applied to the game's methods
        /// (once per test run), so tests can call them as the game would.
        /// </summary>
        public static void Reset(staticVars._playerData._difficulty difficulty, policies._value? salaryPolicy = policies._value.salary_manual, bool patched = false)
        {
            if (patched)
                _ = Patched.Value;

            staticVars.PlayerData.Difficulty = difficulty;
            staticVars.dateTime = Today;
            policies.Values = new List<policies.value>();
            if (salaryPolicy != null)
                policies.Values.Add(new policies.value { Type = policies._type.salary, Value = salaryPolicy.Value, Selected = true });
            data_girls.girl = new List<data_girls.girls>();
            Traverse.Create(typeof(data_girls)).Field("cached_AverageExpectedSalaryCoeff").SetValue(0f);
        }

        /// <summary>
        /// Adds an idol at this fame level. With weeklyEarnings, she has earned that much a week for three months.
        /// </summary>
        public static data_girls.girls Idol(int fameLevel, long salary = Mod.DEF_SALARY, long weeklyEarnings = 0)
        {
            data_girls.girls girl = new() { salary = salary, Graduation_Date = GraduationDate };
            girl.parameters.Add(new data_girls.girls.param { type = data_girls._paramType.famePoints, _val = resources.FameLevelToPoints(fameLevel) });
            if (weeklyEarnings > 0)
                girl.Earnings_History = new List<long> { weeklyEarnings * 4, weeklyEarnings * 4, weeklyEarnings * 4 };
            data_girls.girl.Add(girl);
            return girl;
        }

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModAsset(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Worker Rights", "assets", relativePath);
    }

    /// <summary>
    /// Staff cannot be fired using scandal points within the first month.
    /// </summary>
    public class StaffFiringTests
    {
        public StaffFiringTests() => TestGame.Reset(normal);

        private static bool CanFire(int daysEmployed, bool vanilla)
        {
            staff._staff staffer = new() { HireDate = TestGame.Today.AddDays(-daysEmployed) };
            bool result = vanilla;
            staff__staff_CanFire.Postfix(staffer, ref result);
            return result;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(29)]
        public void FirstMonth_CannotFire(int days)
        {
            Assert.False(CanFire(days, vanilla: true));
        }

        [Theory]
        [InlineData(30)]
        [InlineData(31)]
        [InlineData(400)]
        public void AfterFirstMonth_VanillaDecides(int days)
        {
            Assert.True(CanFire(days, vanilla: true));
            Assert.False(CanFire(days, vanilla: false));
        }

        [Fact]
        public void NullStaff_LeftAlone()
        {
            bool result = true;
            staff__staff_CanFire.Postfix(null, ref result);
            Assert.True(result);
        }

        /// <summary>
        /// The firing hint the mod ships names the same 30 days.
        /// </summary>
        [Fact]
        public void FireHint_MatchesMinimumDays()
        {
            string json = File.ReadAllText(TestGame.ModAsset(Path.Combine("JSON", "Constants", "constants.json")));
            Assert.Contains("STAFF__FIRE_HINT", json);
            Assert.Contains($"at least {Mod.FIRE_MIN_DAYS} days", json);
        }
    }

    /// <summary>
    /// 20000 yen/wk is the default starting salary, and what a new idol expects for 100% satisfaction.
    /// </summary>
    public class StartingSalaryTests
    {
        [Fact]
        public void NewIdol_Paid20000()
        {
            data_girls.girls girl = new() { salary = 5000 };
            data_girls_GenerateGirl.Postfix(ref girl);
            Assert.Equal(20000L, girl.salary);
        }

        [Fact]
        public void NullIdol_LeftAlone()
        {
            data_girls.girls girl = null;
            data_girls_GenerateGirl.Postfix(ref girl);
            Assert.Null(girl);
        }

        [Theory]
        [InlineData(0, 40000)]
        [InlineData(39999, 40000)]
        [InlineData(40000, 40000)]
        [InlineData(55000, 55000)]
        public void BelowFame1_ExpectsAtLeast40000Base(int vanilla, int expected)
        {
            TestGame.Reset(normal);
            int result = vanilla;
            data_girls_girls_GetExpectedSalary.Postfix(ref result, TestGame.Idol(0));
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void Fame1AndUp_Unchanged(int fame)
        {
            TestGame.Reset(normal);
            int result = 12345;
            data_girls_girls_GetExpectedSalary.Postfix(ref result, TestGame.Idol(fame));
            Assert.Equal(12345, result);
        }

        [Fact]
        public void NullIdol_BaseUnchanged()
        {
            int result = 0;
            data_girls_girls_GetExpectedSalary.Postfix(ref result, null);
            Assert.Equal(0, result);
        }

        /// <summary>
        /// Through the game's own satisfaction maths: a new idol on 20000 with no earnings is exactly 100% satisfied.
        /// </summary>
        [Theory]
        [InlineData(20000, 100)]
        [InlineData(10000, 50)]
        [InlineData(40000, 200)]
        public void NewIdol_SatisfactionAgainst20000(long salary, int expected)
        {
            TestGame.Reset(normal, patched: true);
            data_girls.girls girl = TestGame.Idol(0, salary);
            Assert.Equal(20000L, girl.GetExpectedSalary_Total());
            Assert.Equal(expected, girl.GetSalarySatisfaction_Percentage());
        }

        /// <summary>
        /// A new idol who already earns more expects what she earns, up to double the base.
        /// </summary>
        [Theory]
        [InlineData(50000, 50000)]
        [InlineData(100000, 80000)]
        public void NewIdol_WithEarnings_ExpectsEarnings(long weeklyEarnings, long expected)
        {
            TestGame.Reset(normal, patched: true);
            Assert.Equal(expected, TestGame.Idol(0, weeklyEarnings: weeklyEarnings).GetExpectedSalary_Total());
        }
    }

    /// <summary>
    /// In Unfair, idols at 10 fame expect at least 10% of their average earnings, never less than otherwise.
    /// </summary>
    public class FameTenFloorTests
    {
        private static long Total(staticVars._playerData._difficulty difficulty, int fame, long weeklyEarnings, long vanilla)
        {
            TestGame.Reset(difficulty);
            long result = vanilla;
            data_girls_girls_GetExpectedSalary_Total.Postfix(ref result, TestGame.Idol(fame, weeklyEarnings: weeklyEarnings));
            return result;
        }

        [Theory]
        [InlineData(10_000_000, 900_000, 1_000_000)]
        [InlineData(10_000_000, 1_000_000, 1_000_000)]
        [InlineData(10_000_000, 1_200_000, 1_200_000)]
        [InlineData(0, 500_000, 500_000)]
        public void Unfair_Fame10_RaisedTo10PercentOfEarnings(long weeklyEarnings, long vanilla, long expected)
        {
            Assert.Equal(expected, Total(hard, 10, weeklyEarnings, vanilla));
        }

        [Theory]
        [InlineData(easy, 10)]
        [InlineData(normal, 10)]
        [InlineData(hard, 9)]
        [InlineData(hard, 0)]
        public void OtherwiseUnchanged(staticVars._playerData._difficulty difficulty, int fame)
        {
            Assert.Equal(100_000L, Total(difficulty, fame, 10_000_000, 100_000));
        }

        [Fact]
        public void NullIdol_Unchanged()
        {
            TestGame.Reset(hard);
            long result = 100_000;
            data_girls_girls_GetExpectedSalary_Total.Postfix(ref result, null);
            Assert.Equal(100_000L, result);
        }

        /// <summary>
        /// Through the game: vanilla caps the expectation at double the base; above 20x base earnings
        /// the 10% floor takes over. Between 10x and 20x it used to lower the expectation below vanilla's cap.
        /// </summary>
        [Theory]
        [InlineData(1, 1.0)]
        [InlineData(15, 2.0)]
        [InlineData(20, 2.0)]
        [InlineData(30, 3.0)]
        public void Unfair_Fame10_ThroughGame(int earningsTimesBase, double expectedTimesBase)
        {
            TestGame.Reset(hard, patched: true);
            long baseSalary = TestGame.Idol(10).GetExpectedSalary();
            data_girls.girls girl = TestGame.Idol(10, weeklyEarnings: baseSalary * earningsTimesBase);
            Assert.InRange(girl.GetExpectedSalary_Total(), (long)(baseSalary * expectedTimesBase) - 1, (long)(baseSalary * expectedTimesBase) + 1);
        }

        [Fact]
        public void Normal_Fame10_ThroughGame_CappedAtDoubleBase()
        {
            TestGame.Reset(normal, patched: true);
            long baseSalary = TestGame.Idol(10).GetExpectedSalary();
            Assert.Equal(baseSalary * 2, TestGame.Idol(10, weeklyEarnings: baseSalary * 30).GetExpectedSalary_Total());
        }
    }

    /// <summary>
    /// In Unfair and Normal mode with manual salaries, each week the graduation date moves 10 days closer
    /// under 50% salary satisfaction, and 30 days closer under 20%.
    /// </summary>
    public class GraduationPenaltyTests
    {
        /// <summary>
        /// Runs the weekly update's postfix for a new idol, whose expectation is 20000 (so salary / 200 = satisfaction %).
        /// </summary>
        private static int DaysMoved(long salary, staticVars._playerData._difficulty difficulty = normal,
            policies._value? salaryPolicy = policies._value.salary_manual, data_girls._status status = default)
        {
            TestGame.Reset(difficulty, salaryPolicy, patched: true);
            data_girls.girls girl = TestGame.Idol(0, salary);
            girl.status = status;
            data_girls_girls_Graduation_Date_Update.Postfix(girl);
            return (girl.Graduation_Date - TestGame.GraduationDate).Days;
        }

        [Theory]
        [InlineData(40000, 0)]
        [InlineData(20000, 0)]
        [InlineData(10000, 0)]
        [InlineData(9999, -10)]
        [InlineData(5000, -10)]
        [InlineData(4000, -10)]
        [InlineData(3999, -30)]
        [InlineData(1000, -30)]
        [InlineData(0, -30)]
        public void Normal_MovesBySatisfaction(long salary, int expectedDays)
        {
            Assert.Equal(expectedDays, DaysMoved(salary));
        }

        [Theory]
        [InlineData(9999, -10)]
        [InlineData(3999, -30)]
        public void Unfair_SameAsNormal(long salary, int expectedDays)
        {
            Assert.Equal(expectedDays, DaysMoved(salary, hard));
        }

        [Fact]
        public void Easy_NoPenalty()
        {
            Assert.Equal(0, DaysMoved(0, easy));
        }

        [Theory]
        [InlineData(policies._value.salary_low)]
        [InlineData(policies._value.salary_satisfied)]
        [InlineData(null)]
        public void NotManualSalaries_NoPenalty(policies._value? salaryPolicy)
        {
            Assert.Equal(0, DaysMoved(0, salaryPolicy: salaryPolicy));
        }

        [Fact]
        public void AnnouncedGraduation_NoPenalty()
        {
            Assert.Equal(0, DaysMoved(0, status: data_girls._status.announced_graduation));
        }

        [Fact]
        public void NullIdol_LeftAlone()
        {
            TestGame.Reset(normal, patched: true);
            data_girls_girls_Graduation_Date_Update.Postfix(null);
        }

        /// <summary>
        /// The penalty is per call, so it accumulates week after week.
        /// </summary>
        [Fact]
        public void PenaltyAccumulatesWeekly()
        {
            TestGame.Reset(normal, patched: true);
            data_girls.girls girl = TestGame.Idol(0, 0);
            for (int week = 0; week < 4; week++)
                data_girls_girls_Graduation_Date_Update.Postfix(girl);
            Assert.Equal(TestGame.GraduationDate.AddDays(-120), girl.Graduation_Date);
        }
    }
}
