using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System;

namespace UnofficialPatch
{
    // Restores stamina costs for theater schedules. The game works out the cost (5 for a performance, 2 for
    // manzai, doubled on hard), then returns 0 instead of it; return the cost it worked out. Can be turned off in
    // ModMenus (Settings.TheaterStamina).
    [HarmonyPatch(typeof(Theaters), nameof(Theaters.GetStaminaCost))]
    public class Theaters_GetStaminaCost
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Theaters_GetStaminaCost>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions);

            // The cost local: float num = 0f, the method's first store.
            matcher.MatchStartForward(
                new CodeMatch(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float value && value == 0f),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<Theaters_GetStaminaCost>("cost local not found.");
                return instructions;
            }
            CodeInstruction storeCost = matcher.InstructionAt(1);

            // The last return: return 0f.
            matcher.End().MatchStartBackwards(
                new CodeMatch(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float value && value == 0f),
                new CodeMatch(OpCodes.Ret));
            if (matcher.IsInvalid)
            {
                // Already returns the cost: patched twice, or the game fixed it.
                return matcher.InstructionEnumeration();
            }

            // Return Cost(cost) instead, keeping any jump into this instruction.
            CodeInstruction loadCost = IlHelpers.LoadOfStore(storeCost);
            matcher.Instruction.opcode = loadCost.opcode;
            matcher.Instruction.operand = loadCost.operand;
            matcher.Advance(1).Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Theaters_GetStaminaCost), nameof(Cost))));
            return matcher.InstructionEnumeration();
        }

        // The cost the game worked out, or the game's 0 while the fix is turned off.
        public static float Cost(float cost) => Settings.On(Settings.TheaterStamina) ? cost : 0f;
    }

    // Aligns theater revenue timing and payout distribution with the schedule.
    [HarmonyPatch(typeof(Theaters), nameof(Theaters.CompleteDay))]
    public class Theaters_CompleteDay
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Theaters_CompleteDay>(ex);

        // Day-of-month used by the base game for subscription revenue.
        private const int FirstDayOfMonth = 1;

        // What each theater looked like before the day: its last stat row, and its idols' earnings this month.
        public class Before
        {
            public Theaters._theater._stat LastStat;
            public List<KeyValuePair<data_girls.girls, long>> Earnings = new List<KeyValuePair<data_girls.girls, long>>();
        }

        // Fixed Theater so that revenue stats are not offset by one day
        // Last, so it sees whether another mod's prefix skipped the day.
        [HarmonyPriority(Priority.Last)]
        public static void Prefix(bool __runOriginal, out Dictionary<Theaters._theater, Before> __state)
        {
            __state = null;
            // The day was skipped: leave the theaters as they are.
            if (!__runOriginal || Theaters.Theaters_ == null)
                return;

            __state = new Dictionary<Theaters._theater, Before>();
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                if (theater == null)
                    continue;

                Before before = new Before();
                if (theater.Stats != null && theater.Stats.Count > 0)
                    before.LastStat = theater.Stats[theater.Stats.Count - 1];
                List<data_girls.girls> girls = theater.GetGroup()?.GetGirls(true, false, null);
                if (girls != null)
                {
                    foreach (data_girls.girls girl in girls)
                    {
                        if (girl != null)
                            before.Earnings.Add(new KeyValuePair<data_girls.girls, long>(girl, girl.Earnings_CurrentMonth));
                    }
                }
                __state[theater] = before;

                // Fix so that auto schedules contribute revenue on the day of
                theater.Doing_Now = theater.GetSchedule().Type;
            }
        }

        // Fixes revenue accounting and income distribution after the base method completes.
        // Last, so it sees what other mods' patches did.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Dictionary<Theaters._theater, Before> __state)
        {
            if (__state == null)
                return;

            // The game pays idols nothing here, so theaters whose idols' earnings changed during the day were
            // already paid. Checked before paying any, in case idols share theaters.
            HashSet<Theaters._theater> alreadyPaid = new HashSet<Theaters._theater>(__state
                .Where(entry => entry.Value.Earnings.Any(earning => earning.Key.Earnings_CurrentMonth != earning.Value))
                .Select(entry => entry.Key));

            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                if (theater == null || theater.Stats == null || theater.Stats.Count == 0)
                {
                    continue;
                }

                // Only theaters whose day ran: the game adds a new stat row for each one.
                if (!__state.TryGetValue(theater, out Before before) || ReferenceEquals(theater.Stats[theater.Stats.Count - 1], before.LastStat))
                {
                    continue;
                }

                // Fix so that auto schedules contribute revenue on the day of
                if (theater.GetSchedule().Type == Theaters._theater._schedule._type.auto &&
                    (theater.Doing_Now == Theaters._theater._schedule._type.manzai || theater.Doing_Now == Theaters._theater._schedule._type.performance))
                {
                    long rev = theater.GetTicketSales();
                    if (rev > 0) resources.Add(resources.type.money, rev);
                    if (staticVars.dateTime.Day != 1)
                    {
                        theater.GetRoom().addFloat(Floats.type.icon_money, "", true, null, 0f, 1f, 0f, null);
                    }
                }

                Theaters._theater._stat latestStat = theater.Stats[theater.Stats.Count - 1];
                if (latestStat == null || latestStat.Schedule == null)
                {
                    continue;
                }

                // Days off should contribute zero revenue to stats.
                if (latestStat.Schedule.Type == Theaters._theater._schedule._type.day_off)
                {
                    latestStat.Revenue = 0L;
                    continue;
                }

                // Split ticket/subscription revenue among participants for actual show days.
                bool isShowDay =
                    latestStat.Schedule.Type == Theaters._theater._schedule._type.performance ||
                    latestStat.Schedule.Type == Theaters._theater._schedule._type.manzai;
                if (!isShowDay)
                {
                    continue;
                }

                Groups._group group = theater.GetGroup();
                if (group == null)
                {
                    continue;
                }

                List<data_girls.girls> girls = group.GetGirls(true, false, null);
                int girlCount = girls != null ? girls.Count : 0;
                if (girlCount <= 0)
                {
                    continue;
                }

                // Use the revenue value from the stat row vanilla just recorded for this day.
                long payout = latestStat.Revenue;
                if (theater.AreSubsUnlocked() && staticVars.dateTime.Day == FirstDayOfMonth)
                {
                    // Include monthly subscription revenue only on the first day, matching vanilla timing.
                    payout += theater.GetSubRevenue();
                }

                if (payout <= 0L)
                {
                    continue;
                }

                long split = payout / (long)girlCount;
                if (split <= 0L)
                {
                    continue;
                }

                if (alreadyPaid.Contains(theater))
                {
                    continue;
                }

                foreach (data_girls.girls girl in girls)
                {
                    if (girl != null)
                    {
                        girl.Earn(split);
                    }
                }
            }
        }
    }

    // Averages over a theater's last 7 days that leave out days off. The game counts them as 0.
    internal static class TheaterAverages
    {
        // Rolling window size for averages.
        private const int DaysInWeek = 7;

        // The rounded average of this value over the last week's show days, 0 if there were none, or null
        // without stats. Summed in double: revenue is a long, and a float drops whole yen above ~16.7 million.
        public static int? OfShowDays(Theaters._theater theater, Func<Theaters._theater._stat, double> value)
        {
            if (theater == null || theater.Stats == null || theater.Stats.Count == 0)
                return null;

            double total = 0;
            int countedDays = 0;
            for (int index = theater.Stats.Count - 1; index >= Math.Max(0, theater.Stats.Count - DaysInWeek); index--)
            {
                Theaters._theater._stat stat = theater.Stats[index];
                if (stat == null || stat.Schedule == null)
                    return null;
                if (stat.Schedule.Type != Theaters._theater._schedule._type.day_off)
                {
                    total += value(stat);
                    countedDays++;
                }
            }
            if (countedDays != 0)
            {
                total /= countedDays;
            }
            // Halves round to even, as Mathf.RoundToInt does.
            return (int)Math.Round(total);
        }
    }

	// Fixed Theater so that average stats ignore days off
    [HarmonyPatch(typeof(Theaters._theater), nameof(Theaters._theater.GetAvgAttendance))]
    public class Theaters__theater_GetAvgAttendance
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Theaters__theater_GetAvgAttendance>(ex);

        // Last, so it sees what other mods' patches did.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result, Theaters._theater __instance)
        {
            // A result that isn't the game's was already changed by another mod.
            if (__result != Vanilla(__instance))
            {
                PatchLog.WarnResultChanged<Theaters__theater_GetAvgAttendance>();
                return;
            }

            int? average = TheaterAverages.OfShowDays(__instance, stat => stat.Attendance);
            if (average != null)
                __result = average.Value;
        }

        // The game's own GetAvgAttendance, without any mod's patches.
        [HarmonyReversePatch]
        public static int Vanilla(Theaters._theater instance)
        {
            throw new NotImplementedException("Replaced by a reverse patch");
        }
    }

	// Fixed Theater so that average stats ignore days off
    [HarmonyPatch(typeof(Theaters._theater), nameof(Theaters._theater.GetAvgRevenue))]
    public class Theaters__theater_GetAvgRevenue
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Theaters__theater_GetAvgRevenue>(ex);

        // Last, so it sees what other mods' patches did.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result, Theaters._theater __instance)
        {
            // A result that isn't the game's was already changed by another mod.
            if (__result != Vanilla(__instance))
            {
                PatchLog.WarnResultChanged<Theaters__theater_GetAvgRevenue>();
                return;
            }

            int? average = TheaterAverages.OfShowDays(__instance, stat => stat.Revenue);
            if (average != null)
                __result = average.Value;
        }

        // The game's own GetAvgRevenue, without any mod's patches.
        [HarmonyReversePatch]
        public static int Vanilla(Theaters._theater instance)
        {
            throw new NotImplementedException("Replaced by a reverse patch");
        }
    }

	// Fixed Theater so that money tooltip includes 7 days instead of 6, and include sub revenue
    [HarmonyPatch(typeof(Theaters), nameof(Theaters.GetLastWeekEarning))]
    public class Theaters_GetLastWeekEarning
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Theaters_GetLastWeekEarning>(ex);

        // Tooltip is intended to show a full week.
        private const int DaysInWeek = 7;
        // Average weeks per month, to spread the monthly subscription revenue over a week.
        private const double SubRevenueWeeksPerMonth = 4.35;

        // Last, so it sees what other mods' patches did.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref long __result)
        {
            // A result that isn't the game's was already changed by another mod.
            if (__result != Vanilla())
            {
                PatchLog.WarnResultChanged<Theaters_GetLastWeekEarning>();
                return;
            }

            // Start with the base value from the game.
            long output = __result;
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                if (theater == null || theater.Stats == null)
                    continue;

                // Add the missing 7th day for each theater.
                if (theater.Stats.Count >= DaysInWeek)
                {
                    long seventhDayRevenue = theater.Stats[theater.Stats.Count - DaysInWeek].Revenue;
                    output += seventhDayRevenue;
                }
                if (theater.AreSubsUnlocked())
                {
                    // Include subscription revenue spread across an average month.
                    // In double: a float loses whole yen above ~16.7 million a month.
                    long subRevenue = theater.GetSubRevenue();
                    output += (long)Math.Round(subRevenue / SubRevenueWeeksPerMonth);
                }
            }
            // Return the corrected tooltip total.
            __result = output;
        }

        // The game's own GetLastWeekEarning, without any mod's patches.
        [HarmonyReversePatch]
        public static long Vanilla()
        {
            throw new NotImplementedException("Replaced by a reverse patch");
        }
    }
}
