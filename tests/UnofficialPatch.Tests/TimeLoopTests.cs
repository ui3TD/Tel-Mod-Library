using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A day, week or tick handler that throws no longer stops the game's clock: the other handlers still run,
    /// and the failure is logged and shown as a notification.
    /// </summary>
    public class TimeLoopTests
    {
        private static readonly MethodBase MoveNext = AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(mainScript), "TimeProgress"));
        private const string NotificationText = "Unofficial Patch: error in TimeLoopTests.Throw. The game kept running. Details in Player.log.";

        private readonly mainScript main = TestGame.Component<mainScript>();
        private readonly List<string> ran = new();

        public TimeLoopTests()
        {
            TestGame.Reset();
            mainScript_TimeProgress.ResetReports();
        }

        private void Throw() => throw new InvalidOperationException("broken handler");
        private void First() => ran.Add("first");
        private void Second() => ran.Add("second");
        private void Week() => ran.Add("week");
        private void Tick() => ran.Add("tick");

        [Fact]
        public void HandlersRunInOrderWithoutReports()
        {
            mainScript_TimeProgress.Raise((mainScript.newDay)First + Second);

            Assert.Equal(new[] { "first", "second" }, ran);
            Assert.Empty(Seams.Notifications);
            Assert.Empty(Log.Of(LogType.Error));
        }

        [Fact]
        public void FailingDayHandlerIsReportedAndTheRestRun()
        {
            mainScript_TimeProgress.Raise((mainScript.newDay)First + Throw + Second);

            Assert.Equal(new[] { "first", "second" }, ran);
            Assert.Equal(new[] { NotificationText }, Seams.Notifications);
            string error = Assert.Single(Log.Of(LogType.Error));
            Assert.Contains("TimeLoopTests.Throw failed; the game kept running", error);
            Assert.Contains("broken handler", error);
            Assert.Contains("at UnofficialPatch.Tests.TimeLoopTests.Throw", error);
        }

        [Fact]
        public void FailingWeekHandlerIsReportedAndTheRestRun()
        {
            mainScript_TimeProgress.Raise((mainScript.newWeek)Throw + Week);

            Assert.Equal(new[] { "week" }, ran);
            Assert.Equal(new[] { NotificationText }, Seams.Notifications);
        }

        [Fact]
        public void DayFailuresAreReportedEveryTimeWithTheStackOnlyOnce()
        {
            mainScript.newDay handlers = Throw;
            mainScript_TimeProgress.Raise(handlers);
            staticVars.dateTime = staticVars.dateTime.AddDays(1);
            mainScript_TimeProgress.Raise(handlers);

            Assert.Equal(new[] { NotificationText, NotificationText }, Seams.Notifications);
            List<string> errors = Log.Of(LogType.Error).ToList();
            Assert.Equal(2, errors.Count);
            Assert.Contains(" at ", errors[0]);
            Assert.Equal("TimeLoopTests.Throw failed again: InvalidOperationException: broken handler", errors[1].Substring(errors[1].IndexOf("TimeLoopTests.Throw")));
        }

        [Fact]
        public void TickFailuresAreReportedOncePerDay()
        {
            mainScript.timeTick handlers = (mainScript.timeTick)Throw + Tick;
            for (int i = 0; i < 3; i++)
                mainScript_TimeProgress.Raise(handlers);

            Assert.Equal(new[] { "tick", "tick", "tick" }, ran);
            Assert.Single(Seams.Notifications);
            Assert.Single(Log.Of(LogType.Error));

            staticVars.dateTime = staticVars.dateTime.AddDays(1);
            mainScript_TimeProgress.Raise(handlers);

            Assert.Equal(2, Seams.Notifications.Count);
        }

        [Fact]
        public void NoHandlersIsFine()
        {
            mainScript_TimeProgress.Raise((mainScript.newDay)null);
            mainScript_TimeProgress.Raise((mainScript.newWeek)null);
            mainScript_TimeProgress.Raise((mainScript.timeTick)null);

            Assert.Empty(Log.Messages);
        }

        [Fact]
        public void FailingNotificationDoesntStopTheOtherHandlers()
        {
            Seams.NotificationsFail = true;

            mainScript_TimeProgress.Raise((mainScript.newDay)Throw + Second);

            Assert.Equal(new[] { "second" }, ran);
            Assert.Contains(Log.Of(LogType.Error), m => m.Contains("couldn't report a failed time handler"));
        }

        [Fact]
        public void TranspilerGuardsAllThreeEvents()
        {
            List<CodeInstruction> codes = mainScript_TimeProgress.Transpiler(PatchProcessor.GetOriginalInstructions(MoveNext)).ToList();

            Assert.Equal(
                new[] { typeof(mainScript.newDay), typeof(mainScript.newWeek), typeof(mainScript.timeTick) },
                codes.Where(c => c.operand is MethodInfo m && m.Name == nameof(mainScript_TimeProgress.Raise))
                    .Select(c => ((MethodInfo)c.operand).GetParameters()[0].ParameterType));
            Assert.DoesNotContain(codes, c => c.operand is MethodInfo m && m.Name == nameof(Action.Invoke) && typeof(Delegate).IsAssignableFrom(m.DeclaringType));
            Assert.Contains(Harmony.GetPatchInfo(MoveNext).Transpilers, p => p.PatchMethod.DeclaringType == typeof(mainScript_TimeProgress));
        }

        [Fact]
        public void TranspilerWithoutTheCallsWarnsAndChangesNothing()
        {
            List<CodeInstruction> codes = new() { new CodeInstruction(OpCodes.Ret) };

            Assert.Same(codes[0], Assert.Single(mainScript_TimeProgress.Transpiler(codes)));
            Assert.Equal(3, Log.Of(LogType.Warning).Count(m => m.Contains("left to guard")));
        }

        [Fact]
        public void ClockKeepsRunningAfterAFailingDayHandler()
        {
            // Sunday 23:55: the next tick is Monday, so the day and week events both run
            staticVars.dateTime = new DateTime(2023, 6, 4, 23, 55, 0);
            main.onNewDay += Throw;
            main.onNewDay += First;
            main.onNewWeek += Week;
            main.onTimeTick += Tick;
            IEnumerator clock = (IEnumerator)AccessTools.Method(typeof(mainScript), "TimeProgress").Invoke(main, null);

            Assert.True(clock.MoveNext()); // waits for the game to be unpaused
            Assert.True(clock.MoveNext()); // the tick with the day change
            Assert.Equal(new DateTime(2023, 6, 5, 0, 7, 30), staticVars.dateTime);
            Assert.Equal(new[] { "first", "week", "tick" }, ran);
            Assert.Equal(new[] { NotificationText }, Seams.Notifications);

            Assert.True(clock.MoveNext()); // waits for unpause again
            Assert.True(clock.MoveNext()); // the next tick
            Assert.Equal(new DateTime(2023, 6, 5, 0, 20, 0), staticVars.dateTime);
        }
    }
}
