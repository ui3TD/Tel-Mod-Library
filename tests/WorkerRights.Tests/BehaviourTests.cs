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
            // The game's weekly update reads the dating policy
            policies.Values.Add(new policies.value { Type = policies._type.dating, Value = policies._value.dating_allowed, Selected = true });
            if (salaryPolicy != null)
                policies.Values.Add(new policies.value { Type = policies._type.salary, Value = salaryPolicy.Value, Selected = true });
            data_girls.girl = new List<data_girls.girls>();
            Traverse.Create(typeof(data_girls)).Field("cached_AverageExpectedSalaryCoeff").SetValue(0f);

            business.History = new List<business._history>();
            business.data = new List<business._data>
            {
                new() { business_type = business._type.photoshoot, duration = 0 },
                new() { business_type = business._type.tv_drama, duration = 3 },
            };
            Shows.shows = new List<Shows._show>();
            singles.Singles = new List<singles._single>();
            Pushes.Girls = new List<data_girls.girls> { null, null, null };
        }


        /// <summary>
        /// Adds an idol at this fame level. With weeklyEarnings, she has earned that much a week for three months.
        /// </summary>
        public static data_girls.girls Idol(int fameLevel, long salary = Mod.DEF_SALARY, long weeklyEarnings = 0)
        {
            data_girls.girls girl = new() { salary = salary, Graduation_Date = GraduationDate, birthday = Today.AddYears(-17).AddDays(-10) };
            girl.parameters.Add(new data_girls.girls.param { type = data_girls._paramType.famePoints, _val = resources.FameLevelToPoints(fameLevel) });
            girl.parameters.Add(new data_girls.girls.param { type = data_girls._paramType.physicalStamina, _val = 100f });
            girl.parameters.Add(new data_girls.girls.param { type = data_girls._paramType.mentalStamina, _val = 100f });
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
        public void OtherModsStartingSalary_Kept()
        {
            data_girls.girls girl = new() { salary = 8000 };
            data_girls_GenerateGirl.Postfix(ref girl);
            Assert.Equal(8000L, girl.salary);
        }

        [Fact]
        public void NullIdol_LeftAlone()
        {
            data_girls.girls girl = null;
            data_girls_GenerateGirl.Postfix(ref girl);
            Assert.Null(girl);
        }

        /// <summary>
        /// The game's own value below fame 1 is 0; any other value comes from another mod and is kept.
        /// </summary>
        [Theory]
        [InlineData(0, 40000)]
        [InlineData(10000, 10000)]
        [InlineData(39999, 39999)]
        [InlineData(55000, 55000)]
        public void BelowFame1_GameValueBecomes40000Base(int vanilla, int expected)
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
    /// The weekly graduation date change, row by row, from the inputs alone.
    /// </summary>
    public class WeeklyDaysTests
    {
        /// <summary>
        /// An idol no row applies to: age 17, full stamina, average relationship, 60% salary satisfaction,
        /// never had a contract, not in a show or the single, not pushed.
        /// </summary>
        public static GraduationInputs Neutral() => new()
        {
            PhysicalStamina = 100f,
            MentalStamina = 100f,
            Relationship = 0.5f,
            SalarySatisfaction = 60,
            LowSalaryApplies = true,
            Status = data_girls._status.normal,
            DaysSinceContract = -1,
            SingleRow = -1,
            Age = 17,
        };

        public static GraduationInputs With(Func<GraduationInputs, GraduationInputs> change) => change(Neutral());

        public static TheoryData<string, GraduationInputs, float> Rows => new()
        {
            { "nothing", Neutral(), 0f },
            { "physical stamina 24", With(w => { w.PhysicalStamina = 24f; return w; }), -1f },
            { "physical stamina 25", With(w => { w.PhysicalStamina = 25f; return w; }), 0f },
            { "mental stamina 24", With(w => { w.MentalStamina = 24f; return w; }), -3f },
            { "mental stamina 49", With(w => { w.MentalStamina = 49f; return w; }), -1f },
            { "mental stamina 50", With(w => { w.MentalStamina = 50f; return w; }), 0f },
            { "relationship 0.39", With(w => { w.Relationship = 0.39f; return w; }), -2f },
            { "relationship 0.40", With(w => { w.Relationship = 0.40f; return w; }), 0f },
            { "relationship 0.85", With(w => { w.Relationship = 0.85f; return w; }), 0f },
            { "relationship 0.86", With(w => { w.Relationship = 0.86f; return w; }), 1f },
            { "1 flirt point", With(w => { w.FlirtPoints = 1; return w; }), 1f },
            { "4 flirt points", With(w => { w.FlirtPoints = 4; return w; }), 4f },
            { "9 flirt points", With(w => { w.FlirtPoints = 9; return w; }), 4f },
            { "dating banned", With(w => { w.DatingBanned = true; return w; }), -2f },
            { "salary 19%", With(w => { w.SalarySatisfaction = 19; return w; }), -30f },
            { "salary 49%", With(w => { w.SalarySatisfaction = 49; return w; }), -10f },
            { "salary 19%, Easy or automatic", With(w => { w.SalarySatisfaction = 19; w.LowSalaryApplies = false; return w; }), 0f },
            { "salary 49%, Easy or automatic", With(w => { w.SalarySatisfaction = 49; w.LowSalaryApplies = false; return w; }), 0f },
            { "salary 75%", With(w => { w.SalarySatisfaction = 75; return w; }), 0f },
            { "salary 76%", With(w => { w.SalarySatisfaction = 76; return w; }), 0.5f },
            { "salary 101%", With(w => { w.SalarySatisfaction = 101; return w; }), 1f },
            { "salary 151%", With(w => { w.SalarySatisfaction = 151; return w; }), 1.5f },
            { "salary 151%, Easy or automatic", With(w => { w.SalarySatisfaction = 151; w.LowSalaryApplies = false; return w; }), 1.5f },
            { "injured", With(w => { w.Status = data_girls._status.injured; return w; }), -2f },
            { "depressed", With(w => { w.Status = data_girls._status.depressed; return w; }), -7f },
            { "on hiatus", With(w => { w.Status = data_girls._status.hiatus; return w; }), 2f },
            { "last contract 61 days ago", With(w => { w.DaysSinceContract = 61; return w; }), -1f },
            { "last contract 91 days ago", With(w => { w.DaysSinceContract = 91; return w; }), -2f },
            { "last contract 121 days ago", With(w => { w.DaysSinceContract = 121; return w; }), -3f },
            { "last contract 181 days ago", With(w => { w.DaysSinceContract = 181; return w; }), -4f },
            { "last contract 361 days ago", With(w => { w.DaysSinceContract = 361; return w; }), -6f },
            { "last contract 60 days ago", With(w => { w.DaysSinceContract = 60; return w; }), 0.5f },
            { "2 contract weeks", With(w => { w.DaysSinceContract = 5; w.ContractWeeks = 2; return w; }), 1f },
            { "4 contract weeks", With(w => { w.DaysSinceContract = 5; w.ContractWeeks = 4; return w; }), 1.5f },
            { "6 contract weeks", With(w => { w.DaysSinceContract = 5; w.ContractWeeks = 6; return w; }), 2f },
            { "10 contract weeks", With(w => { w.DaysSinceContract = 5; w.ContractWeeks = 10; return w; }), 2f },
            { "1 show", With(w => { w.Shows = 1; return w; }), 1f },
            { "2 shows", With(w => { w.Shows = 2; return w; }), 1.5f },
            { "3 shows", With(w => { w.Shows = 3; return w; }), 2f },
            { "5 shows", With(w => { w.Shows = 5; return w; }), 2f },
            { "single center", With(w => { w.SingleRow = 0; return w; }), 3.5f },
            { "single 2nd-3rd", With(w => { w.SingleRow = 1; return w; }), 1.5f },
            { "single 4th-6th", With(w => { w.SingleRow = 2; return w; }), 0.5f },
            { "single 11th-15th", With(w => { w.SingleRow = 4; return w; }), 0.5f },
            { "not in the single", With(w => { w.SingleRow = 5; return w; }), 0f },
            { "pushed", With(w => { w.Pushed = true; return w; }), 1.5f },
            { "age 20", With(w => { w.Age = 20; return w; }), 0f },
            { "age 21", With(w => { w.Age = 21; return w; }), -1f },
            { "age 26", With(w => { w.Age = 26; return w; }), -3f },
            { "age 31", With(w => { w.Age = 31; return w; }), -7f },
        };

        [Theory]
        [MemberData(nameof(Rows))]
        public void EachRow(string row, GraduationInputs inputs, float expected)
        {
            Assert.True(expected == GraduationWeek.WeeklyDays(inputs), $"{row}: expected {expected}, got {GraduationWeek.WeeklyDays(inputs)}");
        }

        public static GraduationInputs EverythingGood() => With(w =>
        {
            w.SalarySatisfaction = 151;
            w.Relationship = 0.9f;
            w.DaysSinceContract = 3;
            w.ContractWeeks = 8;
            w.Shows = 3;
            w.SingleRow = 0;
            w.Pushed = true;
            return w;
        });

        [Fact]
        public void EverythingGood_LimitedTo5()
        {
            Assert.Equal(5f, GraduationWeek.WeeklyDays(EverythingGood()));
        }

        /// <summary>
        /// The limit applies to positive and negative rows together, so good treatment above it cushions a bad week.
        /// </summary>
        [Fact]
        public void NegativeRows_CountBeforeTheLimit()
        {
            GraduationInputs w = EverythingGood();
            w.MentalStamina = 10f;
            Assert.Equal(5f, GraduationWeek.WeeklyDays(w));
        }

        [Fact]
        public void Age_AddedAfterTheLimit()
        {
            GraduationInputs w = EverythingGood();
            w.Age = 31;
            Assert.Equal(-2f, GraduationWeek.WeeklyDays(w));
        }

        /// <summary>
        /// The Steam description names the same limit, salary penalties and contract values.
        /// </summary>
        [Fact]
        public void Description_MatchesCode()
        {
            string text = File.ReadAllText(TestGame.ModAsset("steam description.txt"));
            Assert.Contains($"at most +{GraduationWeek.WEEKLY_MAX} days", text);
            Assert.Contains($"under 50% moves the date {Mod.GRAD_DAYS_PENALTY} days closer each week, under 20% {Mod.GRAD_DAYS_PENALTY_SEVERE} days", text);
            Assert.Contains($"(+{GraduationWeek.CONTRACT_BASE}, plus {GraduationWeek.CONTRACT_PER_WEEK} per week of work, up to +{GraduationWeek.CONTRACT_MAX}; a one-off counts as one week", text);
            Assert.Contains("counts from the day her last contract ended", text);
            Assert.Contains($"in the last {GraduationWeek.CONTRACT_WINDOW_DAYS} days", text);
        }

        [Fact]
        public void NegativeRows_NotLimited()
        {
            GraduationInputs w = Neutral();
            w.SalarySatisfaction = 0;
            w.Status = data_girls._status.depressed;
            Assert.Equal(-37f, GraduationWeek.WeeklyDays(w));
        }
    }

    /// <summary>
    /// How long the typical idol lasts with one situation held all career: hired at 17, peak age 20,
    /// so the game sets her date 6 years and 150 days ahead. A graduation is announced once the date is
    /// under 90 days away, and she leaves 3 months later.
    /// </summary>
    public class CareerLengthTests
    {
        public static double Years(GraduationInputs inputs)
        {
            double gap = 6 * 365 + 150;
            for (int days = 0; days < 80 * 365; days += 7)
            {
                if (gap < 90)
                    return (days + 91) / 365.0;
                inputs.Age = 17 + days / 365;
                gap += GraduationWeek.WeeklyDays(inputs) - 7;
            }
            return double.PositiveInfinity;
        }

        private static GraduationInputs With(Func<GraduationInputs, GraduationInputs> change) => WeeklyDaysTests.With(change);

        public static TheoryData<string, GraduationInputs, double> Situations => new()
        {
            { "age only", WeeklyDaysTests.Neutral(), 6.2 },
            { "salary below 20%", With(w => { w.SalarySatisfaction = 19; return w; }), 1.4 },
            { "salary below 50%", With(w => { w.SalarySatisfaction = 49; return w; }), 2.8 },
            { "salary below 20%, Easy or automatic", With(w => { w.SalarySatisfaction = 19; w.LowSalaryApplies = false; return w; }), 6.2 },
            { "salary above 75%", With(w => { w.SalarySatisfaction = 76; return w; }), 6.5 },
            { "salary above 100%", With(w => { w.SalarySatisfaction = 101; return w; }), 7.0 },
            { "salary above 150%", With(w => { w.SalarySatisfaction = 151; return w; }), 7.5 },
            { "physical stamina below 25", With(w => { w.PhysicalStamina = 10f; return w; }), 5.5 },
            { "mental stamina below 25", With(w => { w.MentalStamina = 10f; return w; }), 4.5 },
            { "mental stamina below 50", With(w => { w.MentalStamina = 40f; return w; }), 5.5 },
            { "relationship below 40", With(w => { w.Relationship = 0.3f; return w; }), 5.0 },
            { "relationship above 85", With(w => { w.Relationship = 0.9f; return w; }), 7.0 },
            { "1 flirt point", With(w => { w.FlirtPoints = 1; return w; }), 7.0 },
            { "4 flirt points", With(w => { w.FlirtPoints = 4; return w; }), 11.1 },
            { "dating banned", With(w => { w.DatingBanned = true; return w; }), 5.0 },
            { "injured", With(w => { w.Status = data_girls._status.injured; return w; }), 5.0 },
            { "depressed", With(w => { w.Status = data_girls._status.depressed; return w; }), 3.3 },
            { "no contract for 61 days", With(w => { w.DaysSinceContract = 61; return w; }), 5.5 },
            { "no contract for 91 days", With(w => { w.DaysSinceContract = 91; return w; }), 5.0 },
            { "no contract for 121 days", With(w => { w.DaysSinceContract = 121; return w; }), 4.5 },
            { "no contract for 181 days", With(w => { w.DaysSinceContract = 181; return w; }), 4.2 },
            { "no contract for 361 days", With(w => { w.DaysSinceContract = 361; return w; }), 3.6 },
            { "a contract a month", With(w => { w.DaysSinceContract = 10; w.ContractWeeks = 2; return w; }), 7.0 },
            { "a contract every 2 weeks", With(w => { w.DaysSinceContract = 5; w.ContractWeeks = 4; return w; }), 7.5 },
            { "a contract a week", With(w => { w.DaysSinceContract = 3; w.ContractWeeks = 8; return w; }), 8.1 },
            { "always on a 3-month contract", With(w => { w.DaysSinceContract = 0; w.ContractWeeks = 60f / 7; return w; }), 8.1 },
            { "1 show", With(w => { w.Shows = 1; return w; }), 7.0 },
            { "2 shows", With(w => { w.Shows = 2; return w; }), 7.5 },
            { "3 shows", With(w => { w.Shows = 3; return w; }), 8.1 },
            { "single back row", With(w => { w.SingleRow = 3; return w; }), 6.5 },
            { "single 2nd-3rd row", With(w => { w.SingleRow = 1; return w; }), 7.5 },
            { "single center", With(w => { w.SingleRow = 0; return w; }), 10.3 },
            { "pushed", With(w => { w.Pushed = true; return w; }), 7.5 },
            { "everything good", WeeklyDaysTests.EverythingGood(), 13.3 },
        };

        [Theory]
        [MemberData(nameof(Situations))]
        public void TypicalIdol(string situation, GraduationInputs inputs, double expectedYears)
        {
            double years = Years(inputs);
            Assert.True(Math.Abs(years - expectedYears) <= 0.1, $"{situation}: expected {expectedYears} years, got {years:0.00}");
        }
    }

    /// <summary>
    /// Contracts: days since the last one ended, and work over the last 60 days (1 per one-off,
    /// 10 per longer contract spread evenly from acceptance to its end). Today is 2023-06-05.
    /// </summary>
    public class ContractTests
    {
        public ContractTests() => TestGame.Reset(normal);

        private static void Accepted(data_girls.girls girl, business._type type, int daysAgo) =>
            business.History.Add(new business._history { Girl = girl, Type = type, Date = TestGame.Today.AddDays(-daysAgo) });

        private static (int days, float weeks) Read(data_girls.girls girl)
        {
            GraduationWeek.ReadContracts(girl, out int days, out float weeks);
            return (days, weeks);
        }

        private static void AssertRead(data_girls.girls girl, int expectedDays, double expectedWeeks)
        {
            (int days, float weeks) = Read(girl);
            Assert.Equal(expectedDays, days);
            Assert.Equal(expectedWeeks, weeks, 3);
        }

        [Fact]
        public void NeverHadOne()
        {
            AssertRead(TestGame.Idol(0), -1, 0);
        }

        [Fact]
        public void OneOff_1PointAtOnce()
        {
            data_girls.girls girl = TestGame.Idol(0);
            Accepted(girl, business._type.photoshoot, 10);
            AssertRead(girl, 10, 1);
        }

        /// <summary>
        /// A contract type with no data (e.g. one a removed mod added) counts as a one-off.
        /// </summary>
        [Fact]
        public void TypeWithNoData_CountsAsOneOff()
        {
            data_girls.girls girl = TestGame.Idol(0);
            Accepted(girl, business._type.variety, 10);
            AssertRead(girl, 10, 1);
        }

        /// <summary>
        /// A 3-month contract counts every week it runs, and counts as work today while it runs.
        /// </summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(10, 10 / 7.0)]
        [InlineData(30, 30 / 7.0)]
        [InlineData(80, 60 / 7.0)]
        public void LongerContract_Running(int daysAgo, double expectedWeeks)
        {
            data_girls.girls girl = TestGame.Idol(0);
            Accepted(girl, business._type.tv_drama, daysAgo);
            AssertRead(girl, 0, expectedWeeks);
        }

        /// <summary>
        /// Accepted 100 days ago, it ran 89 days and ended 11 days ago: the timer starts at its end,
        /// and the 49 days of it inside the last 60 still count.
        /// </summary>
        [Fact]
        public void LongerContract_TimerStartsAtItsEnd()
        {
            data_girls.girls girl = TestGame.Idol(0);
            Accepted(girl, business._type.tv_drama, 100);
            AssertRead(girl, 11, 49 / 7.0);
        }

        [Fact]
        public void LongerContract_LongOver()
        {
            data_girls.girls girl = TestGame.Idol(0);
            Accepted(girl, business._type.tv_drama, 200);
            AssertRead(girl, 108, 0);
        }

        /// <summary>
        /// The game's GetDaysSinceLastProposal returns the oldest contract; this counts from the most recent.
        /// </summary>
        [Fact]
        public void DaysCountFromTheLastContract()
        {
            data_girls.girls girl = TestGame.Idol(0);
            Accepted(girl, business._type.photoshoot, 400);
            Accepted(girl, business._type.photoshoot, 5);
            AssertRead(girl, 5, 1);
        }

        [Fact]
        public void OneOffs_Only60DaysCount()
        {
            data_girls.girls girl = TestGame.Idol(0);
            Accepted(girl, business._type.photoshoot, 59);
            Accepted(girl, business._type.photoshoot, 60);
            AssertRead(girl, 59, 1);
        }

        [Fact]
        public void OtherIdolsContracts_Ignored()
        {
            data_girls.girls girl = TestGame.Idol(0);
            Accepted(TestGame.Idol(0), business._type.tv_drama, 5);
            AssertRead(girl, -1, 0);
        }
    }

    /// <summary>
    /// Through the game's weekly update: the date moves by the weekly change and keeps fractions.
    /// </summary>
    public class GraduationDateTests
    {
        /// <summary>
        /// Runs the game's weekly update for a new 17-year-old idol, whose expectation is 20000
        /// (so salary / 200 = satisfaction %), and returns how many days her date moved.
        /// </summary>
        private static double DaysMoved(long salary, staticVars._playerData._difficulty difficulty = normal,
            policies._value? salaryPolicy = policies._value.salary_manual, int weeks = 1)
        {
            TestGame.Reset(difficulty, salaryPolicy, patched: true);
            data_girls.girls girl = TestGame.Idol(0, salary);
            for (int week = 0; week < weeks; week++)
                Assert.False(girl.Graduation_Date_Update());
            return (girl.Graduation_Date - TestGame.GraduationDate).TotalDays;
        }

        [Theory]
        [InlineData(40000, 1.5)]
        [InlineData(20000, 0.5)]
        [InlineData(12000, 0)]
        [InlineData(15200, 0.5)]
        [InlineData(10000, 0)]
        [InlineData(9999, -10)]
        [InlineData(4000, -10)]
        [InlineData(3999, -30)]
        [InlineData(0, -30)]
        public void Normal_MovesBySatisfaction(long salary, double expectedDays)
        {
            Assert.Equal(expectedDays, DaysMoved(salary), 3);
        }

        [Theory]
        [InlineData(9999, -10)]
        [InlineData(3999, -30)]
        public void Unfair_SameAsNormal(long salary, double expectedDays)
        {
            Assert.Equal(expectedDays, DaysMoved(salary, hard), 3);
        }

        [Fact]
        public void Easy_NoLowSalaryEffect()
        {
            Assert.Equal(0, DaysMoved(0, easy), 3);
        }

        [Theory]
        [InlineData(policies._value.salary_low)]
        [InlineData(policies._value.salary_satisfied)]
        [InlineData(null)]
        public void NotManualSalaries_NoLowSalaryEffect(policies._value? salaryPolicy)
        {
            Assert.Equal(0, DaysMoved(0, salaryPolicy: salaryPolicy), 3);
        }

        [Fact]
        public void FractionsAccumulate()
        {
            Assert.Equal(2, DaysMoved(15200, weeks: 4), 3);
        }

        [Fact]
        public void AnnouncedGraduation_LeftAlone()
        {
            TestGame.Reset(normal, patched: true);
            data_girls.girls girl = TestGame.Idol(0, 0);
            girl.status = data_girls._status.announced_graduation;
            Assert.False(girl.Graduation_Date_Update());
            Assert.Equal(TestGame.GraduationDate, girl.Graduation_Date);
        }

        /// <summary>
        /// When the game announces a graduation it sets the date itself and returns true.
        /// </summary>
        [Fact]
        public void AnnouncementWeek_LeftAlone()
        {
            TestGame.Reset(normal);
            data_girls.girls girl = TestGame.Idol(0, 0);
            data_girls_girls_Graduation_Date_Update.Postfix(girl, __result: true, __runOriginal: true, TestGame.GraduationDate.AddDays(-5));
            Assert.Equal(TestGame.GraduationDate, girl.Graduation_Date);
        }

        [Fact]
        public void NullIdol_LeftAlone()
        {
            TestGame.Reset(normal);
            data_girls_girls_Graduation_Date_Update.Prefix(null, out DateTime? state);
            data_girls_girls_Graduation_Date_Update.Postfix(null, false, true, state);
        }

        /// <summary>
        /// Reads an idol as the game does: stamina, relationship, salary, contracts, shows, single and age.
        /// </summary>
        [Fact]
        public void Read_ThroughGame()
        {
            TestGame.Reset(normal);
            data_girls.girls girl = TestGame.Idol(0, 40000);
            girl.getParam(data_girls._paramType.mentalStamina)._val = 30f;
            girl.Rel_Friendship_Points = 512;
            girl.DatingData.Success_Counter = 3;
            business.History.Add(new business._history { Girl = girl, Type = business._type.photoshoot, Date = TestGame.Today.AddDays(-4) });
            Pushes.Girls[1] = girl;

            GraduationInputs w = GraduationWeek.Read(girl);
            Assert.Equal(100f, w.PhysicalStamina);
            Assert.Equal(30f, w.MentalStamina);
            Assert.Equal(1f, w.Relationship);
            Assert.Equal(3, w.FlirtPoints);
            Assert.False(w.DatingBanned);
            Assert.Equal(200, w.SalarySatisfaction);
            Assert.True(w.LowSalaryApplies);
            Assert.Equal(4, w.DaysSinceContract);
            Assert.Equal(1, w.ContractWeeks);
            Assert.Equal(0, w.Shows);
            Assert.Equal(-1, w.SingleRow);
            Assert.True(w.Pushed);
            Assert.Equal(17, w.Age);
        }

        [Fact]
        public void Read_DatingBan_Adults()
        {
            TestGame.Reset(normal);
            policies.GetSelectedPolicyValue(policies._type.dating).Value = policies._value.dating_forbidden;
            data_girls.girls girl = TestGame.Idol(0);
            Assert.False(GraduationWeek.Read(girl).DatingBanned);
            girl.birthday = TestGame.Today.AddYears(-18);
            Assert.True(GraduationWeek.Read(girl).DatingBanned);
        }
    }

    /// <summary>
    /// Other mods on the same method: a game value stored by another mod is replaced, not added to,
    /// and a mod that replaces the method leaves only the low-salary effect.
    /// </summary>
    public class GraduationCompatibilityTests
    {
        private static readonly System.Reflection.MethodInfo Target = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Date_Update));

        public static void MoveDateInside(data_girls.girls girl) => girl.Graduation_Date = girl.Graduation_Date.AddDays(99);

        /// <summary>Stand-in for a mod that makes the game store its own weekly value: the date moves inside the method.</summary>
        public static IEnumerable<CodeInstruction> StoreInside(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);
            yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(GraduationCompatibilityTests), nameof(MoveDateInside)));
            foreach (CodeInstruction instruction in instructions)
                yield return instruction;
        }

        public static bool SkipOriginal() => false;

        public static bool BrokenSatisfaction() => throw new InvalidOperationException("broken satisfaction patch");

        public static readonly List<string> Errors = new();

        /// <summary>Debug.LogError doesn't work outside the game.</summary>
        public static bool LogError(object message)
        {
            Errors.Add(message?.ToString());
            return false;
        }

        private static double DaysMoved(long salary, HarmonyMethod prefix = null, HarmonyMethod transpiler = null)
        {
            TestGame.Reset(normal, patched: true);
            data_girls.girls girl = TestGame.Idol(0, salary);
            Harmony standIn = new("tests.WorkerRights.StandIn");
            try
            {
                standIn.Patch(Target, prefix: prefix, transpiler: transpiler);
                girl.Graduation_Date_Update();
            }
            finally
            {
                standIn.UnpatchSelf();
            }
            return (girl.Graduation_Date - TestGame.GraduationDate).TotalDays;
        }

        /// <summary>
        /// An error would stop the rest of the game's weekly update, so it's logged once and the date is left alone.
        /// </summary>
        [Fact]
        public void Error_IsLoggedOnce_AndDateUnchanged()
        {
            TestGame.Reset(normal);
            data_girls.girls girl = TestGame.Idol(0, 0);
            Errors.Clear();
            Harmony standIn = new("tests.WorkerRights.StandIn");
            try
            {
                standIn.Patch(AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.GetSalarySatisfaction)),
                    prefix: new HarmonyMethod(typeof(GraduationCompatibilityTests), nameof(BrokenSatisfaction)));
                standIn.Patch(AccessTools.Method(typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogError), new[] { typeof(object) }),
                    prefix: new HarmonyMethod(typeof(GraduationCompatibilityTests), nameof(LogError)));
                data_girls_girls_Graduation_Date_Update.Postfix(girl, false, true, TestGame.GraduationDate);
                data_girls_girls_Graduation_Date_Update.Postfix(girl, false, false, TestGame.GraduationDate);
            }
            finally
            {
                standIn.UnpatchSelf();
            }
            Assert.Equal(TestGame.GraduationDate, girl.Graduation_Date);
            string error = Assert.Single(Errors);
            Assert.Contains("[Worker Rights]", error);
        }

        [Fact]
        public void GameValueStoredByAnotherMod_Replaced()
        {
            Assert.Equal(1.5, DaysMoved(40000, transpiler: new HarmonyMethod(typeof(GraduationCompatibilityTests), nameof(StoreInside))), 3);
        }

        [Theory]
        [InlineData(40000, 0)]
        [InlineData(9999, -10)]
        [InlineData(0, -30)]
        public void MethodReplacedByAnotherMod_OnlyLowSalary(long salary, double expected)
        {
            Assert.Equal(expected, DaysMoved(salary, prefix: new HarmonyMethod(typeof(GraduationCompatibilityTests), nameof(SkipOriginal))), 3);
        }
    }
}
