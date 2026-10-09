using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace InGameTests.TelMods
{
    /// <summary>
    /// FastForward at its top speed on the game's real clock: the fast button's second click turns
    /// it on in the real scene, and four weeks at that speed fire every day's and every week's events
    /// once. The unit tests check the speed setting and the clicks on stand-ins, not the tick loop.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class FastForwardTests
    {
        private const string HarmonyId = "com.tel.fastforward";
        private const string MultiplierVariable = "FastForward_Multiplier";

        // The mod's cap. Each tick adds speed / 4 minutes: 5600 / 4 = 1400 minutes, just under a day.
        private const int MaxMultiplier = 28;
        private const double SuperFast = 200d * MaxMultiplier;

        // A tick a little over a day only skips one once the time of day wraps past midnight, so a
        // longer run catches a smaller overshoot: 28 days catch any cap above about 29.8x.
        private const int Days = 28;
        private const int AtLeastDaysAtSuperFast = 14;

        /// <summary>At 28x every calendar day crossed fires one new-day event, and every Monday one new-week event.</summary>
        [InGameTest(Suite = ModTest.ClockSuite, Order = 10)]
        private static IEnumerator MaxSpeedSkipsNoDays(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;
            yield return Game.CloseAllPopups(ctx);

            mainScript main = Game.Main;
            var days = new List<DateTime>();
            var speeds = new List<double>();
            var weeks = new List<DateTime>();
            var pausedAt = AccessTools.FieldRefAccess<mainScript, double>("time_speed");
            mainScript.newDay onDay = () =>
            {
                days.Add(staticVars.dateTime.Date);
                // A handler before this one may have opened a popup, which pauses the clock and keeps its speed.
                double speed = staticVars.dateTimeAddMinutesPerSecond;
                speeds.Add(speed == 0d ? pausedAt(main) : speed);
            };
            mainScript.newWeek onWeek = () => weeks.Add(staticVars.dateTime.Date);
            DateTime start = staticVars.dateTime;

            // Fewer dialogues and popups pausing the clock, so more of the run is at full speed.
            using (Game.Variable(MultiplierVariable, MaxMultiplier.ToString()))
            using (Game.Option(staticVars._playerData._options.randomEvents, false))
            using (Game.Option(staticVars._playerData._options.substories, false))
            using (Game.Option(staticVars._playerData._options.datingScandals, false))
            {
                // From pause, the first click is the game's fast and the second the mod's super-fast.
                Game.TimeControl(mainScript._time_state.pause).OnClick();
                TimeControlButton fast = Game.TimeControl(mainScript._time_state.fast);
                fast.OnClick();
                fast.OnClick();
                ctx.Assert(staticVars.dateTimeAddMinutesPerSecond == SuperFast,
                    $"Two clicks on the fast button set {staticVars.dateTimeAddMinutesPerSecond} minutes a second; expected {SuperFast}");
                ctx.Assert(main.TimeControls_Fast.GetComponent<TextMeshProUGUI>().color == (UnityEngine.Color)mainScript.gold32,
                    "The fast button's label isn't gold at super-fast");

                main.onNewDay += onDay;
                main.onNewWeek += onWeek;
                try
                {
                    yield return Game.AdvanceDays(ctx, Days);
                }
                finally
                {
                    main.onNewDay -= onDay;
                    main.onNewWeek -= onWeek;
                }
            }

            DateTime end = staticVars.dateTime;
            List<DateTime> calendar = Enumerable.Range(1, (end.Date - start.Date).Days).Select(d => start.Date.AddDays(d)).ToList();
            ctx.Record("newDayEvents", days.Count);
            ctx.Record("calendarDays", calendar.Count);
            ctx.Assert(days.SequenceEqual(calendar),
                $"The new-day events don't match the {calendar.Count} days crossed: missing {Dates(calendar.Except(days))}, extra or repeated {Dates(Repeats(days, calendar))}");
            List<DateTime> mondays = calendar.Where(d => d.DayOfWeek == DayOfWeek.Monday).ToList();
            ctx.Assert(weeks.SequenceEqual(mondays),
                $"The new-week events fell on {Dates(weeks)}; the Mondays crossed are {Dates(mondays)}");

            int longest = 0;
            for (int run = 0, i = 0; i < speeds.Count; i++)
            {
                run = speeds[i] == SuperFast ? run + 1 : 0;
                longest = Math.Max(longest, run);
            }
            ctx.Record("daysAtSuperFast", speeds.Count(s => s == SuperFast) + " of " + speeds.Count);
            ctx.Assert(longest >= AtLeastDaysAtSuperFast,
                $"Only {longest} days in a row ran at {SuperFast} minutes a second; the check needs {AtLeastDaysAtSuperFast} to mean anything");
            int dropped = speeds.FindIndex(s => s != SuperFast);
            if (dropped >= 0)
                ctx.Note($"Super-fast was off on {days[dropped]:yyyy-MM-dd} ({speeds[dropped]} minutes a second)");
        }

        private static IEnumerable<DateTime> Repeats(List<DateTime> days, List<DateTime> calendar) =>
            days.GroupBy(d => d).Where(g => g.Count() > 1 || !calendar.Contains(g.Key)).Select(g => g.Key);

        private static string Dates(IEnumerable<DateTime> dates)
        {
            string text = string.Join(", ", dates.Select(d => d.ToString("yyyy-MM-dd")).ToArray());
            return text.Length == 0 ? "none" : text;
        }
    }
}
