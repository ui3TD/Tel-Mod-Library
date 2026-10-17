using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// The game's monthly event runs once, on the first tick of the 1st: Best Employer strikes are counted and the
    /// idols' monthly earnings are recorded.
    /// </summary>
    public class MonthlyEventTests : IDisposable
    {
        private static readonly FieldInfo OnNewMonth = AccessTools.Field(typeof(mainScript), "onNewMonth");

        private readonly mainScript main = TestGame.Component<mainScript>();
        private readonly Awards awards = TestGame.Component<Awards>();
        private readonly data_girls girlsData = TestGame.Component<data_girls>();
        private readonly data_girls.girls idol;

        // A game where the monthly event has its vanilla subscribers and one idol earned 500 this month,
        // with an injury that gives a Best Employer strike.
        public MonthlyEventTests()
        {
            TestGame.Reset();
            idol = TestGame.Registered(1, data_girls._status.injured);
            idol.Earnings_CurrentMonth = 500;
            Awards.Checks = new Awards._checks();
            main.onNewMonth += awards.OnNewMonth;
            main.onNewMonth += girlsData.OnNewMonth;
            MonthlyEvent.Attach(main);
        }

        public void Dispose() => MonthlyEvent.Detach();

        [Fact]
        public void RaisedOnTheFirstTickOfTheMonth()
        {
            NewDay(new DateTime(2023, 3, 1));
            MonthlyEvent.OnTimeTick();
            MonthlyEvent.OnTimeTick();

            Assert.Equal(new List<long> { 500 }, idol.Earnings_History);
            Assert.Equal(0, idol.Earnings_CurrentMonth);
            Assert.Equal(1, Awards.Checks.Best_Employer_Counter);
        }

        [Fact]
        public void NotRaisedOnOtherDays()
        {
            NewDay(new DateTime(2023, 3, 2));
            MonthlyEvent.OnTimeTick();

            Assert.Empty(idol.Earnings_History);
            Assert.Equal(0, Awards.Checks.Best_Employer_Counter);
        }

        [Fact]
        public void NotRaisedWhenLoadingASaveOnTheFirst()
        {
            // Loading sets the date without a day change
            staticVars.SetTime(new DateTime(2023, 3, 1));
            MonthlyEvent.OnTimeTick();

            Assert.Empty(idol.Earnings_History);
        }

        [Fact]
        public void NotRaisedAgainWhenAnotherModRaisedIt()
        {
            NewDay(new DateTime(2023, 3, 1));
            // Another mod raises the event right after onNewDay
            ((mainScript.newWeek)OnNewMonth.GetValue(main))();
            MonthlyEvent.OnTimeTick();

            Assert.Equal(new List<long> { 500 }, idol.Earnings_History);
            Assert.Equal(1, Awards.Checks.Best_Employer_Counter);
        }

        [Fact]
        public void EmployerStrikeCountedOnceWhenAnotherModRanTheCheck()
        {
            NewDay(new DateTime(2023, 3, 1));
            // Another mod runs the Best Employer check in a postfix on Awards.OnNewDay
            Awards.Checks.Best_Employer_Counter++;
            MonthlyEvent.OnTimeTick();

            Assert.Equal(1, Awards.Checks.Best_Employer_Counter);
            Assert.Equal(new List<long> { 500 }, idol.Earnings_History);
        }

        [Fact]
        public void FailingSubscriberDoesNotStopTheOthers()
        {
            main.onNewMonth -= girlsData.OnNewMonth;
            main.onNewMonth += () => throw new InvalidOperationException("test");
            main.onNewMonth += girlsData.OnNewMonth;
            NewDay(new DateTime(2023, 3, 1));
            MonthlyEvent.OnTimeTick();

            Assert.Equal(new List<long> { 500 }, idol.Earnings_History);
            Assert.Contains(Log.Of(UnityEngine.LogType.Error), m => m.Contains("onNewMonth subscriber"));
        }

        // What a real day change does: Awards.OnNewDay (with this mod's prefix) and the day subscribers
        private static void NewDay(DateTime date)
        {
            staticVars.SetTime(date);
            Awards_OnNewDay.Prefix();
            MonthlyEvent.OnNewDay();
        }
    }
}
