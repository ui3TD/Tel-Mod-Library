using HarmonyLib;
using StaleTheater;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static resources.fanType;
using static staticVars._playerData._difficulty;
using _type = Theaters._theater._schedule._type;

// Every test shares the game's static state (groups, singles, date, difficulty).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace StaleTheaterShows.Tests
{
    /// <summary>
    /// Builds the minimal game state the theater reads: one group with one idol, and a theater
    /// running the same show every day.
    /// </summary>
    public static class TestGame
    {
        public static readonly DateTime Today = new(2023, 6, 5);
        private const int GroupID = 7;

        private static readonly Lazy<bool> Patched = new(() =>
        {
            Harmony harmony = new("tests.StaleTheaterShows");
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(Theaters__theater_GetSubRevenue).Assembly, "StaleTheater"))
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        public static Groups._group Group { get; private set; }

        /// <summary>
        /// Resets the game state. With patched set, the mod is applied to the game's methods
        /// (once per test run), so tests can call them as the game would.
        /// </summary>
        public static void Reset(staticVars._playerData._difficulty difficulty, bool patched = false)
        {
            if (patched)
                _ = Patched.Value;

            staticVars.PlayerData.Difficulty = difficulty;
            staticVars.dateTime = Today;
            singles.Singles = new List<singles._single>();
            Group = new Groups._group { ID = GroupID, Date_Created = Today.AddYears(-1) };
            Groups.Groups_ = new List<Groups._group> { Group };
        }

        /// <summary>
        /// Gives the group's idol this many fans, all of the given type.
        /// </summary>
        public static void SetFans(resources.fanType fanType, long people)
        {
            resources._fan fan = new() { gender = male, hardcoreness = hardcore, age = adult, people = people };
            switch (fanType)
            {
                case male:
                case female:
                    fan.gender = fanType;
                    break;
                case casual:
                case hardcore:
                    fan.hardcoreness = fanType;
                    break;
                default:
                    fan.age = fanType;
                    break;
            }

            data_girls.girls girl = new();
            girl.Fans.Add(fan);
            Group.Girls = new List<data_girls.girls> { girl };
        }

        /// <summary>
        /// Releases a single for the group the given number of days ago.
        /// </summary>
        public static singles._single ReleaseSingle(int daysAgo, Groups._group group = null, bool released = true)
        {
            singles._single single = new() { status = released ? singles._single._status.released : singles._single._status.normal };
            single.ReleaseData.ReleaseDate = Today.AddDays(-daysAgo);
            (group ?? Group).Singles.Add(single);
            singles.Singles.Add(single);
            return single;
        }

        /// <summary>
        /// A theater running this show every day. A null fan type targets everyone.
        /// </summary>
        public static Theaters._theater Theater(_type show, resources.fanType? fanType, int ticketPrice = 3500)
        {
            Theaters._theater theater = new() { Group = GroupID, Doing_Now = show, Ticket_Price = ticketPrice };
            for (int day = 0; day < 7; day++)
            {
                theater.Schedule.Add(new Theaters._theater._schedule
                {
                    Type = show,
                    FanType_Everyone = fanType == null,
                    FanType = fanType ?? hardcore,
                });
            }
            return theater;
        }
    }

    public class StalenessTests
    {
        private static float Coeff(int daysSinceSingle, int price = 3500, float vanilla = 1f)
        {
            TestGame.ReleaseSingle(daysSinceSingle);
            float result = vanilla;
            Theaters__theater_GetPriceCoeff.Postfix(price, TestGame.Theater(_type.performance, null), ref result);
            return result;
        }

        /// <summary>
        /// Shows stay fresh for 30 days, then sales follow 0.1 + 0.9 × 10^(−t²), where t is the
        /// share of the decay time (183 days Unfair, 366 Normal) since day 30. 90% of the drop
        /// is done by the decay time.
        /// </summary>
        [Theory]
        [InlineData(hard, 0, 1f)]
        [InlineData(hard, 30, 1f)]
        [InlineData(hard, 45, 0.980f)]
        [InlineData(hard, 60, 0.924f)]
        [InlineData(hard, 90, 0.732f)]
        [InlineData(hard, 183, 0.19f)]
        [InlineData(hard, 270, 0.103f)]
        [InlineData(normal, 30, 1f)]
        [InlineData(normal, 60, 0.984f)]
        [InlineData(normal, 183, 0.658f)]
        [InlineData(normal, 366, 0.19f)]
        [InlineData(normal, 500, 0.110f)]
        public void SalesDecayAfterThirtyDays(staticVars._playerData._difficulty difficulty, int days, float expected)
        {
            TestGame.Reset(difficulty);
            Assert.Equal(expected, Coeff(days), 3);
        }

        /// <summary>
        /// The decay levels off at 10% instead of reaching zero.
        /// </summary>
        [Theory]
        [InlineData(hard)]
        [InlineData(normal)]
        public void SalesLevelOffAtTenPercent(staticVars._playerData._difficulty difficulty)
        {
            TestGame.Reset(difficulty);
            Assert.Equal(0.1f, Coeff(1000), 4);
            Assert.Equal(0.1f, Coeff(100000), 4);
        }

        [Theory]
        [InlineData(hard)]
        [InlineData(normal)]
        public void SalesFallSteadilyAndNeverBelowTenPercent(staticVars._playerData._difficulty difficulty)
        {
            TestGame.Reset(difficulty);
            float previous = 1f;
            for (int days = 31; days <= 2000; days++)
            {
                singles.Singles.Clear();
                TestGame.Group.Singles.Clear();
                float coeff = Coeff(days);
                Assert.True(coeff <= previous, $"day {days}: {coeff} > {previous}");
                Assert.True(coeff >= 0.1f, $"day {days}: {coeff} < 0.1");
                previous = coeff;
            }
        }

        [Fact]
        public void ScalesTheVanillaPriceCoefficient()
        {
            TestGame.Reset(hard);
            Assert.Equal(2.195f, Coeff(90, vanilla: 3f), 3);
        }

        /// <summary>
        /// The 0.1% floor still applies when vanilla's own coefficient is already tiny.
        /// </summary>
        [Fact]
        public void TinyVanillaCoefficient_StopsAtTheFloor()
        {
            TestGame.Reset(hard);
            Assert.Equal(0.001f, Coeff(1000, price: 30000, vanilla: 0.001f));
        }

        [Fact]
        public void Easy_DoesNotDecay()
        {
            TestGame.Reset(easy);
            Assert.Equal(1f, Coeff(1000));
        }

        /// <summary>
        /// Above ¥30,000 the vanilla coefficient is already 0.001 or 0, and is left alone.
        /// </summary>
        [Theory]
        [InlineData(30001, 0.001f)]
        [InlineData(40001, 0f)]
        public void HighPrices_KeepVanillaCoefficient(int price, float vanilla)
        {
            TestGame.Reset(hard);
            Assert.Equal(vanilla, Coeff(1000, price, vanilla));
        }

        [Fact]
        public void UsesTheLatestReleasedSingle()
        {
            TestGame.Reset(hard);
            TestGame.ReleaseSingle(183);
            Assert.Equal(1f, Coeff(10), 4);
        }

        [Fact]
        public void IgnoresOtherGroupsAndUnreleasedSingles()
        {
            TestGame.Reset(hard);
            Groups._group other = new() { ID = 8 };
            Groups.Groups_.Add(other);
            TestGame.ReleaseSingle(183);
            TestGame.ReleaseSingle(0, other);
            TestGame.ReleaseSingle(0, released: false);

            float result = 1f;
            Theaters__theater_GetPriceCoeff.Postfix(3500, TestGame.Theater(_type.performance, null), ref result);
            Assert.Equal(0.19f, result, 4);
        }

        [Fact]
        public void NoSingle_CountsFromGroupCreation()
        {
            TestGame.Reset(hard);
            TestGame.Group.Date_Created = TestGame.Today.AddDays(-90);

            float result = 1f;
            Theaters__theater_GetPriceCoeff.Postfix(3500, TestGame.Theater(_type.performance, null), ref result);
            Assert.Equal(0.732f, result, 3);
        }
    }

    public class SubscriptionTests
    {
        private static long Run(long vanilla)
        {
            long result = vanilla;
            Theaters__theater_GetSubRevenue.Postfix(ref result);
            return result;
        }

        /// <summary>
        /// Unfair keeps 10% of subscription revenue, Normal 30%, Easy all of it.
        /// </summary>
        [Theory]
        [InlineData(hard, 2000000L, 200000L)]
        [InlineData(normal, 2000000L, 600000L)]
        [InlineData(easy, 2000000L, 2000000L)]
        [InlineData(normal, 2001L, 600L)]
        [InlineData(hard, 0L, 0L)]
        public void RevenueIsCut(staticVars._playerData._difficulty difficulty, long vanilla, long expected)
        {
            TestGame.Reset(difficulty);
            Assert.Equal(expected, Run(vanilla));
        }

        /// <summary>
        /// Results above int's ~2.1 billion limit stay exact. 1.0.0 converted them to int, which
        /// wrapped to about −¥2.1 billion a month.
        /// </summary>
        [Theory]
        [InlineData(normal, 10000000000L, 3000000000L)]
        [InlineData(hard, 30000000000L, 3000000000L)]
        [InlineData(normal, 7158278830L, 2147483649L)]
        [InlineData(normal, 1000000000000000L, 300000000000000L)]
        public void LargeRevenue_DoesNotOverflow(staticVars._playerData._difficulty difficulty, long vanilla, long expected)
        {
            TestGame.Reset(difficulty);
            Assert.Equal(expected, Run(vanilla));
        }

        [Fact]
        public void AppliesToTheGamesRevenue()
        {
            TestGame.Reset(normal, patched: true);
            Theaters._theater theater = TestGame.Theater(_type.performance, null);
            theater.Subscription_Price = 2000;
            theater.Subscribers.Add(new Theaters._theater._subscriber { People = 5000000 });

            Assert.Equal(3000000000L, theater.GetSubRevenue());
        }
    }

    public class AttendanceTests
    {
        /// <summary>
        /// 20,000 targeted fans fill 100 seats at multiplier 1 (vanilla's scale), at a ¥3,500
        /// ticket where vanilla's price coefficient is 1. Easy turns off staleness.
        /// </summary>
        private static int Visitors(_type show, resources.fanType? target, staticVars._playerData._difficulty difficulty = easy, long fans = 20000, int ticketPrice = 3500)
        {
            TestGame.Reset(difficulty, patched: true);
            TestGame.SetFans(target ?? hardcore, fans);
            return TestGame.Theater(show, target, ticketPrice).GetNumberOfVisitors();
        }

        /// <summary>
        /// Performances: everyone 0.3, casual 0.65, teen 0.9, adult/young adult 0.95,
        /// male/female 0.75, hardcore 1 (vanilla 0.2, 0.3, 0.5, 0.75, 0.75, 1).
        /// </summary>
        [Theory]
        [InlineData(null, 30)]
        [InlineData(casual, 65)]
        [InlineData(teen, 90)]
        [InlineData(adult, 95)]
        [InlineData(youngAdult, 95)]
        [InlineData(male, 75)]
        [InlineData(female, 75)]
        [InlineData(hardcore, 100)]
        public void Performance_UsesModMultipliers(resources.fanType? target, int expected)
        {
            Assert.Equal(expected, Visitors(_type.performance, target));
        }

        /// <summary>
        /// Manzai halves the performance multiplier, and non-hardcore audiences take another
        /// ×0.75. Everyone is 0.2 halved.
        /// </summary>
        [Theory]
        [InlineData(null, 10)]
        [InlineData(hardcore, 50)]
        [InlineData(casual, 24)]
        [InlineData(teen, 34)]
        [InlineData(adult, 36)]
        [InlineData(youngAdult, 36)]
        [InlineData(male, 28)]
        [InlineData(female, 28)]
        public void Manzai_UsesModMultipliers(resources.fanType? target, int expected)
        {
            Assert.Equal(expected, Visitors(_type.manzai, target));
        }

        [Fact]
        public void StalenessReducesAttendance()
        {
            TestGame.Reset(hard, patched: true);
            TestGame.ReleaseSingle(90);
            TestGame.SetFans(hardcore, 20000);
            Assert.Equal(22, TestGame.Theater(_type.performance, null).GetNumberOfVisitors());
        }

        [Fact]
        public void AttendanceIsCappedAtCapacity()
        {
            Assert.Equal(250, Visitors(_type.performance, hardcore, fans: 10000000));
        }

        [Fact]
        public void TicketsOverFortyThousand_SellNothing()
        {
            Assert.Equal(0, Visitors(_type.performance, hardcore, ticketPrice: 40001));
        }

        /// <summary>
        /// The multiplier is stored where vanilla's ticket-price check branches to, so the normal
        /// path runs it. Inserting it after the early return without that branch label left it
        /// unreachable (1.0.0).
        /// </summary>
        [Fact]
        public void Multiplier_IsOnTheBranchTargetAfterThePriceCheck()
        {
            MethodInfo original = AccessTools.Method(typeof(Theaters._theater), nameof(Theaters._theater.GetNumberOfVisitors));
            List<CodeInstruction> patched = Theaters__theater_GetNumberOfVisitors.Transpiler(PatchProcessor.GetOriginalInstructions(original)).ToList();
            MethodInfo infix = AccessTools.Method(typeof(Theaters__theater_GetNumberOfVisitors), nameof(Theaters__theater_GetNumberOfVisitors.Infix));

            int index = Assert.Single(Enumerable.Range(0, patched.Count), i => patched[i].Calls(infix));

            // if (Ticket_Price > 40000) return 0; <label> this.Infix(); num = ...; num *= GetPriceCoeff(...)
            Assert.Equal(OpCodes.Ret, patched[index - 2].opcode);
            Assert.Equal(OpCodes.Ldarg_0, patched[index - 1].opcode);
            Label target = Assert.Single(patched[index - 1].labels);
            Assert.Contains(patched, i => i.Branches(out Label? branch) && branch == target);
            Assert.Equal(OpCodes.Stloc_0, patched[index + 1].opcode);
        }

        /// <summary>
        /// If a game update changes the visitor formula, the game's code is left as it is and the mod says
        /// why its attendance multipliers stopped applying, instead of failing to load.
        /// </summary>
        [Fact]
        public void Transpiler_GameCodeChanged_LeavesItAndLogs()
        {
            UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            LogRecorder log = new();
            UnityEngine.Debug.unityLogger.logHandler = log;
            try
            {
                List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Theaters._theater), nameof(Theaters._theater.GetTicketSales)));

                List<CodeInstruction> patched = Theaters__theater_GetNumberOfVisitors.Transpiler(instructions).ToList();

                Assert.Equal(instructions.Select(i => i.ToString()), patched.Select(i => i.ToString()));
                Assert.Contains(log.Messages, m => m.StartsWith("[Stale Theater Shows] Couldn't find"));
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logHandler = gameLog;
            }
        }

        private class LogRecorder : UnityEngine.ILogHandler
        {
            public readonly List<string> Messages = new();
            public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args) => Messages.Add(string.Format(format, args));
            public void LogException(Exception exception, UnityEngine.Object context) => Messages.Add(exception.ToString());
        }
    }
}
