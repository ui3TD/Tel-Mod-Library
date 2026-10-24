using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;

namespace UnofficialPatch
{
    // Fixes the monthly event never happening: the game declares mainScript.onNewMonth but never raises it, so its
    // subscribers never run: Awards.OnNewMonth (Best Employer strikes, so the award was won every year) and
    // data_girls.OnNewMonth (monthly earnings history, which salary expectations average over).
    // Raised on the first tick of the 1st of each month, after onNewDay, as the time loop would have.
    // Compatible without knowing other mods: if anything else raises onNewMonth that day, ours doesn't; and if a mod
    // already ran the Best Employer check that day, the Awards subscriber is skipped.
    public static class MonthlyEvent
    {
        private static readonly FieldInfo OnNewMonthField = AccessTools.Field(typeof(mainScript), "onNewMonth");

        private static mainScript Main;
        // Set by a real day change to the 1st (not by loading a save), cleared once the month is raised.
        internal static bool Pending;
        // Best Employer strikes before anyone ran the check today; -1 when not recorded.
        internal static int StrikesAtDayStart = -1;

        public static void Attach(mainScript main)
        {
            Detach();
            Main = main;
            Main.onNewDay += OnNewDay;
            Main.onNewMonth += OnNewMonthRaised;
            Main.onTimeTick += OnTimeTick;
        }

        public static void Detach()
        {
            if (Main is object)
            {
                Main.onNewDay -= OnNewDay;
                Main.onNewMonth -= OnNewMonthRaised;
                Main.onTimeTick -= OnTimeTick;
            }
            Main = null;
            Pending = false;
        }

        public static void OnNewDay()
        {
            Pending = staticVars.dateTime.Day == 1;
        }

        public static void OnNewMonthRaised()
        {
            Pending = false;
        }

        public static void OnTimeTick()
        {
            if (!Pending)
                return;
            Pending = false;
            Raise(Main);
        }

        // Calls each onNewMonth subscriber. One failing subscriber is logged and doesn't stop the others or the clock.
        public static void Raise(mainScript main)
        {
            if (main is null || !(OnNewMonthField.GetValue(main) is Delegate handlers))
                return;

            bool employerCheckDone = StrikesAtDayStart >= 0 && Awards.Checks.Best_Employer_Counter != StrikesAtDayStart;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                if (handler.Target is Awards && employerCheckDone)
                    continue;
                try
                {
                    ((mainScript.newWeek)handler)();
                }
                catch (Exception e)
                {
                    PatchLog.Error<Awards_Awake>("onNewMonth subscriber " + handler.Method.DeclaringType?.Name + "." + handler.Method.Name + " failed: " + e);
                }
            }
        }
    }

    // Starts the monthly event with the game's awards, which the game creates with each game scene.
    [HarmonyPatch(typeof(Awards), nameof(Awards.Awake))]
    public class Awards_Awake
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Awards_Awake>(ex);

        public static void Postfix()
        {
            MonthlyEvent.Attach(Camera.main.GetComponent<mainScript>());
        }
    }

    // Stops it with them; unpatching doesn't remove event subscriptions.
    [HarmonyPatch(typeof(Awards), nameof(Awards.OnDestroy))]
    public class Awards_OnDestroy
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Awards_OnDestroy>(ex);

        public static void Postfix()
        {
            MonthlyEvent.Detach();
        }
    }

    // Records the strike count before anything runs Awards' day logic (including other mods' postfixes).
    [HarmonyPatch(typeof(Awards), nameof(Awards.OnNewDay))]
    public class Awards_OnNewDay
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Awards_OnNewDay>(ex);

        public static void Prefix()
        {
            MonthlyEvent.StrikesAtDayStart = staticVars.dateTime.Day == 1 ? Awards.Checks.Best_Employer_Counter : -1;
        }
    }

    // Base game bugfix:
    // The game's clock is one coroutine, mainScript.TimeProgress, which raises onNewDay, onNewWeek and onTimeTick.
    // When any subscriber throws (a game bug, bad save data or a mod's patch on a subscriber), the subscribers after
    // it are skipped and the exception ends the coroutine, so the date never moves again until a save is loaded.
    //
    // Fix:
    // Each subscriber is called on its own. One that throws is logged and named in a red notification, and the
    // other subscribers and the clock carry on. Nothing changes while no subscriber throws: the same handlers run
    // in the same order, from the same snapshot a multicast call uses.
    // Compatible without knowing other mods: runs after other transpilers and wraps whatever event calls are left;
    // if another mod already replaced them, there's nothing to wrap and the game is left as that mod made it.
    [HarmonyPatch(typeof(mainScript), nameof(mainScript.TimeProgress), MethodType.Enumerator)]
    public class mainScript_TimeProgress
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<mainScript_TimeProgress>(ex);

        // Handler + exception type pairs whose full exception was logged.
        private static readonly HashSet<string> Logged = new HashSet<string>();
        // The in-game day each failing tick handler was last reported on.
        private static readonly Dictionary<string, DateTime> TickReported = new Dictionary<string, DateTime>();

        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            foreach (Type eventType in new[] { typeof(mainScript.newDay), typeof(mainScript.newWeek), typeof(mainScript.timeTick) })
            {
                MethodInfo invoke = AccessTools.Method(eventType, nameof(Action.Invoke));
                MethodInfo raise = AccessTools.Method(typeof(mainScript_TimeProgress), nameof(Raise), new[] { eventType });
                int replaced = 0;
                foreach (CodeInstruction code in codes)
                {
                    if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && ReferenceEquals(code.operand, invoke))
                    {
                        code.opcode = OpCodes.Call;
                        code.operand = raise;
                        replaced++;
                    }
                }
                if (replaced == 0)
                    PatchLog.WarnOnce<mainScript_TimeProgress>("no " + eventType.Name + " call left to guard (another mod may have changed it).");
            }
            return codes;
        }

        public static void Raise(mainScript.newDay handlers)
        {
            if (handlers is null)
                return;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((mainScript.newDay)handler)();
                }
                catch (Exception e)
                {
                    Failed(handler, e, false);
                }
            }
        }

        public static void Raise(mainScript.newWeek handlers)
        {
            if (handlers is null)
                return;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((mainScript.newWeek)handler)();
                }
                catch (Exception e)
                {
                    Failed(handler, e, false);
                }
            }
        }

        public static void Raise(mainScript.timeTick handlers)
        {
            if (handlers is null)
                return;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((mainScript.timeTick)handler)();
                }
                catch (Exception e)
                {
                    Failed(handler, e, true);
                }
            }
        }

        // Day and week failures are reported every time. A tick handler runs several times a second, so its
        // failures are reported once per in-game day.
        internal static void Failed(Delegate handler, Exception e, bool tick)
        {
            try
            {
                string name = HandlerName(handler);
                if (tick)
                {
                    DateTime today = staticVars.dateTime.Date;
                    if (TickReported.TryGetValue(name, out DateTime reported) && reported == today)
                        return;
                    TickReported[name] = today;
                }

                if (Logged.Add(name + "|" + e.GetType().FullName))
                    PatchLog.Error<mainScript_TimeProgress>(name + " failed; the game kept running: " + e);
                else
                    PatchLog.Error<mainScript_TimeProgress>(name + " failed again: " + e.GetType().Name + ": " + e.Message);

                Report(name);
            }
            catch (Exception reportError)
            {
                Debug.LogError("[UnofficialPatch] couldn't report a failed time handler: " + reportError);
            }
        }

        internal static void Report(string name)
        {
            NotificationManager.AddNotification("Unofficial Patch: error in " + name + ". The game kept running. Details in Player.log.",
                mainScript.red32, NotificationManager._notification._type.other);
        }

        internal static string HandlerName(Delegate handler) => handler.Method.DeclaringType?.Name + "." + handler.Method.Name;

        internal static void ResetReports()
        {
            Logged.Clear();
            TickReported.Clear();
        }
    }
}
