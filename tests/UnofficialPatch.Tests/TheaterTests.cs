using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using Xunit;
using _type = Theaters._theater._schedule._type;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A theater run by the main group, and the game's end-of-day processing for it.
    /// </summary>
    public static class TheaterGame
    {
        /// <summary>
        /// Hires idols with 10,000 hardcore fans each, and opens a theater running this show every day.
        /// </summary>
        public static Theaters._theater Open(_type show, int idols = 1, int ticketPrice = 3000)
        {
            for (int i = 0; i < idols; i++)
            {
                data_girls.girls girl = TestGame.Hire(TestGame.Idol(name: "Idol " + i));
                girl.Fans.Add(new resources._fan
                {
                    gender = resources.fanType.male,
                    hardcoreness = resources.fanType.hardcore,
                    age = resources.fanType.adult,
                    people = 10_000,
                });
            }

            Theaters._theater theater = new() { ID = 1, Group = TestGame.MainGroup.ID, Ticket_Price = ticketPrice };
            for (int day = 0; day < 7; day++)
                theater.Schedule.Add(new Theaters._theater._schedule { Type = show, FanType_Everyone = false, FanType = resources.fanType.hardcore });
            Theaters.Theaters_.Add(theater);
            return theater;
        }

        public static void Schedule(Theaters._theater theater, _type show)
        {
            foreach (Theaters._theater._schedule day in theater.Schedule)
                day.Type = show;
        }

        public static void UnlockSubscriptions(Theaters._theater theater, int people)
        {
            theater.Streaming_Researched = true;
            theater.Equipment_Purchased = true;
            theater.Subscribers.Add(new Theaters._theater._subscriber { People = people });
        }

        /// <summary>
        /// Runs the game's end of day for every theater on this date (patched by the mod).
        /// </summary>
        public static void CompleteDay(DateTime date)
        {
            staticVars.dateTime = date;
            TestGame.CallPrivate(TestGame.Component<Theaters>(), typeof(Theaters), "CompleteDay");
        }

        public static long MoneyAdded => Seams.MoneyAdded.Sum();

        public static Theaters._theater._stat Stat(Theaters._theater theater, _type show, long revenue, int attendance = 0) =>
            new() { Schedule = new Theaters._theater._schedule { Type = show }, Revenue = revenue, Attendance = attendance };
    }

    /// <summary>
    /// A show's ticket money is paid the day the show runs, matches the day's stats, and is shared with the idols.
    /// </summary>
    public class TheaterDayTests
    {
        public TheaterDayTests() => TestGame.Reset();

        [Theory]
        [InlineData(_type.performance)]
        [InlineData(_type.manzai)]
        public void ScheduledShow_IsPaidTheSameDay(_type show)
        {
            Theaters._theater theater = TheaterGame.Open(show);

            TheaterGame.CompleteDay(TestGame.Today);

            long revenue = theater.Stats.Last().Revenue;
            Assert.True(revenue > 0L);
            Assert.Equal(revenue, TheaterGame.MoneyAdded);
        }

        [Fact]
        public void YesterdaysShow_IsNotPaidAgainAfterTheScheduleChanges()
        {
            Theaters._theater theater = TheaterGame.Open(_type.performance);
            TheaterGame.CompleteDay(TestGame.Today);
            Seams.MoneyAdded.Clear();

            TheaterGame.Schedule(theater, _type.day_off);
            TheaterGame.CompleteDay(TestGame.Today.AddDays(1));

            Assert.Equal(0L, TheaterGame.MoneyAdded);
        }

        [Fact]
        public void DayOff_RecordsNoRevenue()
        {
            // The game records the ticket sales a show would have made, even on a day off
            Theaters._theater theater = TheaterGame.Open(_type.day_off);

            TheaterGame.CompleteDay(TestGame.Today);

            Assert.Equal(0L, theater.Stats.Last().Revenue);
            Assert.Equal(0L, TheaterGame.MoneyAdded);
            Assert.All(TestGame.MainGroup.Girls, girl => Assert.Equal(0L, girl.Earnings_CurrentMonth));
        }

        [Fact]
        public void AutoSchedule_PaysTheShowItRollsTheSameDay()
        {
            Theaters._theater theater = TheaterGame.Open(_type.auto);
            Seams.Chance = _ => true; // the first 60% roll picks a performance

            TheaterGame.CompleteDay(TestGame.Today);

            Assert.Equal(_type.performance, theater.Doing_Now);
            long revenue = theater.Stats.Last().Revenue;
            Assert.True(revenue > 0L);
            Assert.Equal(revenue, TheaterGame.MoneyAdded);
            Assert.Contains(Floats.type.icon_money, Seams.FloatsShown);
        }

        [Fact]
        public void AutoSchedule_RollingADayOff_PaysNothing()
        {
            Theaters._theater theater = TheaterGame.Open(_type.auto);
            Seams.Chance = _ => false;

            TheaterGame.CompleteDay(TestGame.Today);

            Assert.Equal(_type.day_off, theater.Doing_Now);
            Assert.Equal(0L, theater.Stats.Last().Revenue);
            Assert.Equal(0L, TheaterGame.MoneyAdded);
            Assert.Empty(Seams.FloatsShown);
        }

        [Fact]
        public void Idols_ShareTheDaysRevenue()
        {
            Theaters._theater theater = TheaterGame.Open(_type.performance, idols: 3);

            TheaterGame.CompleteDay(TestGame.Today);

            long share = theater.Stats.Last().Revenue / 3;
            Assert.True(share > 0L);
            Assert.All(TestGame.MainGroup.Girls, girl => Assert.Equal(share, girl.Earnings_CurrentMonth));
        }

        [Fact]
        public void GraduatedIdols_GetNoShare()
        {
            Theaters._theater theater = TheaterGame.Open(_type.performance, idols: 2);
            data_girls.girls graduate = TestGame.Hire(TestGame.Idol(name: "Graduate"));
            graduate.status = data_girls._status.graduated;

            TheaterGame.CompleteDay(TestGame.Today);

            Assert.Equal(0L, graduate.Earnings_CurrentMonth);
            Assert.Equal(theater.Stats.Last().Revenue / 2, TestGame.MainGroup.Girls[0].Earnings_CurrentMonth);
        }

        [Fact]
        public void FirstOfTheMonth_SharesTheSubscriptionsToo()
        {
            Theaters._theater theater = TheaterGame.Open(_type.performance, idols: 2);
            TheaterGame.UnlockSubscriptions(theater, 100);
            long subscriptions = theater.GetSubRevenue();

            TheaterGame.CompleteDay(new DateTime(2023, 7, 1));

            long tickets = theater.Stats.Last().Revenue;
            Assert.Equal(tickets + subscriptions, TheaterGame.MoneyAdded);
            // The game pays the subscriptions before the day's subscriber changes; the idols' share is
            // worked out afterwards, from the new count (here 100 → 96 subscribers)
            long subscriptionsAfterTheDay = theater.GetSubRevenue();
            Assert.NotEqual(subscriptions, subscriptionsAfterTheDay);
            Assert.All(TestGame.MainGroup.Girls, girl => Assert.Equal((tickets + subscriptionsAfterTheDay) / 2, girl.Earnings_CurrentMonth));
        }

        [Fact]
        public void ShowDay_CostsTheIdolsStamina()
        {
            TheaterGame.Open(_type.performance, idols: 2);

            TheaterGame.CompleteDay(TestGame.Today);

            Assert.Equal(2, Seams.ParamsAdded.Count(p => p.type == data_girls._paramType.physicalStamina && p.val == -5f));
        }
    }

    public class TheaterStaminaTests
    {
        public TheaterStaminaTests() => TestGame.Reset();

        [Theory]
        [InlineData(_type.performance, false, 5f)]
        [InlineData(_type.manzai, false, 2f)]
        [InlineData(_type.day_off, false, 0f)]
        [InlineData(_type.auto, false, 0f)]
        [InlineData(_type.performance, true, 10f)]
        [InlineData(_type.manzai, true, 4f)]
        public void ShowCostsStamina_DoubleOnUnfair(_type show, bool hard, float expected)
        {
            // The game computes these costs, then returns 0
            if (hard)
                staticVars.PlayerData.Difficulty = staticVars._playerData._difficulty.hard;
            Assert.Equal(expected, Theaters.GetStaminaCost(show));
        }

        /// <summary>
        /// The mod changes one instruction: the final "return 0f" returns the cost instead. Transpiling
        /// again (or a game that already returns the cost) changes nothing.
        /// </summary>
        [Fact]
        public void Transpiler_ChangesOnlyTheReturn_AndOnlyOnce()
        {
            MethodInfo method = AccessTools.Method(typeof(Theaters), nameof(Theaters.GetStaminaCost));
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(method);
            List<CodeInstruction> once = Theaters_GetStaminaCost.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToList();
            List<CodeInstruction> twice = Theaters_GetStaminaCost.Transpiler(once).ToList();

            Assert.Equal(original.Count, once.Count);
            Assert.Equal(1, Enumerable.Range(0, original.Count).Count(i => original[i].ToString() != once[i].ToString()));
            Assert.Equal(once.Select(ci => ci.ToString()), twice.Select(ci => ci.ToString()));
            Assert.Empty(Log.Messages);
        }
    }

    /// <summary>
    /// The theater screen's 7-day averages leave out days off.
    /// </summary>
    public class TheaterAverageTests
    {
        public TheaterAverageTests() => TestGame.Reset();

        private static Theaters._theater WithStats(params (_type show, long revenue, int attendance)[] days)
        {
            Theaters._theater theater = new();
            foreach ((_type show, long revenue, int attendance) in days)
                theater.Stats.Add(TheaterGame.Stat(theater, show, revenue, attendance));
            return theater;
        }

        [Fact]
        public void Averages_SkipDaysOff()
        {
            Theaters._theater theater = WithStats(
                (_type.performance, 1000, 80),
                (_type.day_off, 0, 0),
                (_type.manzai, 2000, 60),
                (_type.day_off, 0, 0));

            Assert.Equal(1500, theater.GetAvgRevenue());
            Assert.Equal(70, theater.GetAvgAttendance());
        }

        [Fact]
        public void Averages_OnlyCoverTheLastSevenDays()
        {
            Theaters._theater theater = WithStats(
                (_type.performance, 9000, 100), // eight days ago
                (_type.performance, 1000, 50),
                (_type.performance, 1000, 50),
                (_type.performance, 1000, 50),
                (_type.performance, 1000, 50),
                (_type.performance, 1000, 50),
                (_type.performance, 1000, 50),
                (_type.performance, 1000, 50));

            Assert.Equal(1000, theater.GetAvgRevenue());
            Assert.Equal(50, theater.GetAvgAttendance());
        }

        [Fact]
        public void Averages_AreZeroWithOnlyDaysOff()
        {
            Theaters._theater theater = WithStats((_type.day_off, 0, 0), (_type.day_off, 0, 0));

            Assert.Equal(0, theater.GetAvgRevenue());
            Assert.Equal(0, theater.GetAvgAttendance());
        }

        /// <summary>
        /// Revenue is summed exactly. In a float, 7 days of 20,000,001 yen averaged to 20,000,000.
        /// </summary>
        [Fact]
        public void RevenueAverage_KeepsEveryYen()
        {
            Theaters._theater theater = WithStats(Enumerable.Repeat((_type.performance, 20_000_001L, 100), 7).ToArray());

            Assert.Equal(20_000_001, theater.GetAvgRevenue());
        }

        /// <summary>
        /// Halves round to even, as the game's Mathf.RoundToInt does.
        /// </summary>
        [Fact]
        public void Averages_RoundHalvesToEven()
        {
            Assert.Equal(2, WithStats((_type.performance, 2, 2), (_type.performance, 3, 3)).GetAvgRevenue());
            Assert.Equal(4, WithStats((_type.performance, 4, 4), (_type.performance, 3, 3)).GetAvgAttendance());
        }

        [Fact]
        public void Averages_AreZeroWithNoStats()
        {
            Assert.Equal(0, WithStats().GetAvgRevenue());
            Assert.Equal(0, WithStats().GetAvgAttendance());
        }
    }

    /// <summary>
    /// The money tooltip's weekly theater and cafe totals cover 7 days, and theaters add a week of subscriptions.
    /// </summary>
    public class WeeklyTooltipTests
    {
        public WeeklyTooltipTests() => TestGame.Reset();

        private static Theaters._theater TheaterWithRevenue(params long[] days)
        {
            Theaters._theater theater = new();
            foreach (long revenue in days)
                theater.Stats.Add(TheaterGame.Stat(theater, _type.performance, revenue));
            Theaters.Theaters_.Add(theater);
            return theater;
        }

        private static void CafeWithProfits(params int[] days)
        {
            Cafes._cafe cafe = new();
            foreach (int profit in days)
                cafe.Stats.Add(new Cafes._cafe._stat { Profit = profit });
            Cafes.Cafes_.Add(cafe);
        }

        [Fact]
        public void Theater_CountsSevenDays()
        {
            TheaterWithRevenue(100_000, 1, 2, 4, 8, 16, 32, 64);
            Assert.Equal(127L, Theaters.GetLastWeekEarning());
        }

        [Fact]
        public void Theater_CountsWhatThereIsInTheFirstWeek()
        {
            TheaterWithRevenue(1, 2, 4);
            Assert.Equal(7L, Theaters.GetLastWeekEarning());
        }

        [Fact]
        public void Theater_AddsEveryTheater()
        {
            TheaterWithRevenue(1, 1, 1, 1, 1, 1, 1);
            TheaterWithRevenue(10, 10, 10, 10, 10, 10, 10);
            Assert.Equal(77L, Theaters.GetLastWeekEarning());
        }

        [Theory]
        [InlineData(100, 45_977L)]                    // ¥200,000 a month / 4.35
        [InlineData(10_000, 4_597_701L)]              // ¥20 million: a float divide would be off
        [InlineData(10_000_000, 4_597_701_149L)]      // ¥20 billion
        public void Theater_AddsAWeekOfSubscriptions(int subscribers, long weekly)
        {
            Theaters._theater theater = TheaterWithRevenue();
            TheaterGame.UnlockSubscriptions(theater, subscribers);
            Assert.Equal(weekly, Theaters.GetLastWeekEarning());
        }

        [Fact]
        public void Theater_SubscriptionsCountOnlyOnceUnlocked()
        {
            Theaters._theater theater = TheaterWithRevenue();
            theater.Subscribers.Add(new Theaters._theater._subscriber { People = 100 });
            Assert.Equal(0L, Theaters.GetLastWeekEarning());
        }

        [Fact]
        public void Cafe_CountsSevenDays()
        {
            CafeWithProfits(100_000, 1, 2, 4, 8, 16, 32, 64);
            CafeWithProfits(-10, 10);
            Assert.Equal(127, Cafes.GetLastWeekEarning());
        }

        [Fact]
        public void Cafe_CapsAtTheIntMaximum()
        {
            // 7 × ¥500 million = ¥3.5 billion; the game's int total wraps negative
            CafeWithProfits(Enumerable.Repeat(500_000_000, 7).ToArray());
            Assert.Equal(int.MaxValue, Cafes.GetLastWeekEarning());
        }

        [Fact]
        public void Cafe_CapsLossesAtTheIntMinimum()
        {
            CafeWithProfits(Enumerable.Repeat(-500_000_000, 7).ToArray());
            Assert.Equal(int.MinValue, Cafes.GetLastWeekEarning());
        }

        [Fact]
        public void Cafe_LargeButFittingTotalIsExact()
        {
            CafeWithProfits(300_000_000, 300_000_000, 300_000_000, 300_000_000, 300_000_000, 300_000_000, 300_000_000);
            Assert.Equal(2_100_000_000, Cafes.GetLastWeekEarning());
        }
    }
}
