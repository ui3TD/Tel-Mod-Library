using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnofficialPatch
{

    // Centralized logging utilities for Unofficial Patch patches.
    internal static class PatchLog
    {
        // Shared logging prefix for Unofficial Patch output.
        private const string LogPrefix = "[UnofficialPatch] ";
        // Marker used when Harmony metadata cannot be read.
        private const string UnknownTarget = "UnknownTarget";

        // Protects the once-only warning cache.
        private static readonly object OnceLock = new object();
        // Tracks warnings that were already emitted to prevent log spam.
        private static readonly HashSet<string> OnceKeys = new HashSet<string>();

        // Logs a warning for the given patch type.
        public static void Warn<TPatch>(string message)
        {
            Warn(typeof(TPatch), message);
        }

        // Logs an error for the given patch type, including the resolved Harmony target.
        public static void Error<TPatch>(string message)
        {
            bool usedFallback;
            string target = GetTargetName(typeof(TPatch), out usedFallback);
            Debug.LogError(LogPrefix + target + ": " + message);
        }

        // Logs a warning once per patch + message.
        public static void WarnOnce<TPatch>(string message)
        {
            WarnOnce(typeof(TPatch), message);
        }

        // Logs a warning once per patch type, regardless of message.
        public static void WarnOncePerPatch<TPatch>(string message)
        {
            WarnOncePerPatch(typeof(TPatch), message);
        }

        // Logs a warning for the given patch type, including the resolved Harmony target if available.
        public static void Warn(Type patchType, string message)
        {
            bool usedFallback;
            string target = GetTargetName(patchType, out usedFallback);
            string prefix = LogPrefix + target;
            if (usedFallback && patchType != null)
            {
                prefix += " (patch: " + patchType.Name + ")";
            }
            Debug.LogWarning(prefix + ": " + message);
        }

        // Logs a warning once per patch + message combination.
        public static void WarnOnce(Type patchType, string message)
        {
            string key = GetOnceKey(patchType, message);
            lock (OnceLock)
            {
                if (OnceKeys.Contains(key))
                    return;
                OnceKeys.Add(key);
            }
            Warn(patchType, message);
        }

        // Logs a warning once per patch type to avoid repeated spam.
        public static void WarnOncePerPatch(Type patchType, string message)
        {
            string key = GetOnceKey(patchType, string.Empty);
            lock (OnceLock)
            {
                if (OnceKeys.Contains(key))
                    return;
                OnceKeys.Add(key);
            }
            Warn(patchType, message);
        }

        // Builds a readable Harmony target name from the patch class's [HarmonyPatch] attributes, falling back to
        // UnknownTarget if it has none.
        private static string GetTargetName(Type patchType, out bool usedFallback)
        {
            usedFallback = true;
            if (patchType == null)
                return UnknownTarget;

            List<string> targets = patchType.GetCustomAttributes(typeof(HarmonyPatch), true)
                .Cast<HarmonyPatch>()
                .Select(attribute => GetTargetName(attribute.info))
                .Where(target => !string.IsNullOrEmpty(target))
                .Distinct()
                .ToList();
            if (targets.Count == 0)
                return UnknownTarget;

            usedFallback = false;
            return string.Join(", ", targets.ToArray());
        }

        // One attribute's target, from Harmony's public HarmonyMethod info.
        private static string GetTargetName(HarmonyMethod info)
        {
            if (info == null)
                return null;

            string typeName = info.declaringType != null ? (info.declaringType.FullName ?? info.declaringType.Name) : null;
            string signature = FormatArgs(info.argumentTypes);

            if (!string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(info.methodName))
                return typeName + "." + info.methodName + signature;

            if (!string.IsNullOrEmpty(typeName))
                return typeName;

            if (!string.IsNullOrEmpty(info.methodName))
                return info.methodName + signature;

            return info.methodType?.ToString();
        }

        // Formats argument type lists to help log target signatures.
        private static string FormatArgs(Type[] argumentTypes)
        {
            if (argumentTypes == null)
                return string.Empty;
            if (argumentTypes.Length == 0)
                return "()";
            return "(" + string.Join(", ", argumentTypes.Select(t => t != null ? t.Name : "null")) + ")";
        }

        // Builds a stable cache key for once-only warnings.
        private static string GetOnceKey(Type patchType, string message)
        {
            string typeName = patchType != null ? patchType.FullName : "null";
            return typeName + "|" + message;
        }
    }

    // Shared IL helper predicates to keep opcode checks consistent and reusable.
    internal static class IlHelpers
    {
        // Identifies call/callvirt instructions targeting a specific method.
        public static bool IsCallTo(CodeInstruction instruction, MethodInfo target)
        {
            if (instruction == null || target == null)
                return false;

            if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
                return false;

            return instruction.operand is MethodInfo method && method == target;
        }

        // Checks whether an instruction loads an int constant with any ldc.i4 opcode form.
        public static bool IsLdcI4(CodeInstruction instruction, int value)
        {
            if (instruction == null)
                return false;

            // Match the specific ldc.i4 opcode variants that encode constant -1..8 directly.
            if (instruction.opcode == OpCodes.Ldc_I4_M1)
                return value == -1;
            if (instruction.opcode == OpCodes.Ldc_I4_0)
                return value == 0;
            if (instruction.opcode == OpCodes.Ldc_I4_1)
                return value == 1;
            if (instruction.opcode == OpCodes.Ldc_I4_2)
                return value == 2;
            if (instruction.opcode == OpCodes.Ldc_I4_3)
                return value == 3;
            if (instruction.opcode == OpCodes.Ldc_I4_4)
                return value == 4;
            if (instruction.opcode == OpCodes.Ldc_I4_5)
                return value == 5;
            if (instruction.opcode == OpCodes.Ldc_I4_6)
                return value == 6;
            if (instruction.opcode == OpCodes.Ldc_I4_7)
                return value == 7;
            if (instruction.opcode == OpCodes.Ldc_I4_8)
                return value == 8;
            if (instruction.opcode == OpCodes.Ldc_I4_S)
                return instruction.operand is sbyte sbyteValue && sbyteValue == value;
            if (instruction.opcode == OpCodes.Ldc_I4)
                return instruction.operand is int intValue && intValue == value;

            return false;
        }

        // Checks whether an instruction loads a local variable.
        public static bool IsLdloc(CodeInstruction instruction)
        {
            if (instruction == null)
                return false;

            return instruction.opcode == OpCodes.Ldloc ||
                instruction.opcode == OpCodes.Ldloc_S ||
                instruction.opcode == OpCodes.Ldloc_0 ||
                instruction.opcode == OpCodes.Ldloc_1 ||
                instruction.opcode == OpCodes.Ldloc_2 ||
                instruction.opcode == OpCodes.Ldloc_3;
        }

        // Checks whether an instruction stores a local variable.
        public static bool IsStloc(CodeInstruction instruction)
        {
            if (instruction == null)
                return false;

            return instruction.opcode == OpCodes.Stloc ||
                instruction.opcode == OpCodes.Stloc_S ||
                instruction.opcode == OpCodes.Stloc_0 ||
                instruction.opcode == OpCodes.Stloc_1 ||
                instruction.opcode == OpCodes.Stloc_2 ||
                instruction.opcode == OpCodes.Stloc_3;
        }

        // Builds the instruction that loads the local a stloc instruction stores to.
        public static CodeInstruction LoadOfStore(CodeInstruction store)
        {
            if (store.opcode == OpCodes.Stloc_0)
                return new CodeInstruction(OpCodes.Ldloc_0);
            if (store.opcode == OpCodes.Stloc_1)
                return new CodeInstruction(OpCodes.Ldloc_1);
            if (store.opcode == OpCodes.Stloc_2)
                return new CodeInstruction(OpCodes.Ldloc_2);
            if (store.opcode == OpCodes.Stloc_3)
                return new CodeInstruction(OpCodes.Ldloc_3);
            if (store.opcode == OpCodes.Stloc_S)
                return new CodeInstruction(OpCodes.Ldloc_S, store.operand);
            return new CodeInstruction(OpCodes.Ldloc, store.operand);
        }

        // Checks whether an instruction loads the argument at this index (static methods count from 0).
        public static bool IsLdarg(CodeInstruction instruction, int index)
        {
            if (instruction == null)
                return false;

            if (instruction.opcode == OpCodes.Ldarg_0) return index == 0;
            if (instruction.opcode == OpCodes.Ldarg_1) return index == 1;
            if (instruction.opcode == OpCodes.Ldarg_2) return index == 2;
            if (instruction.opcode == OpCodes.Ldarg_3) return index == 3;
            if (instruction.opcode == OpCodes.Ldarg_S || instruction.opcode == OpCodes.Ldarg)
                return Convert.ToInt32(instruction.operand) == index;

            return false;
        }

        // Checks for conditional branches that jump when the stack value is true.
        public static bool IsBranchTrue(CodeInstruction instruction)
        {
            if (instruction == null)
                return false;

            return instruction.opcode == OpCodes.Brtrue || instruction.opcode == OpCodes.Brtrue_S;
        }
    }

    // Fixes fan pie rendering so the adult slice accounts for YA/Teen stacking.
    [HarmonyPatch(typeof(Profile_Fans_Pies), "Render_Pies")]
    public class Profile_Fans_Pies_Render_Pies
    {
        // No-fan sentinel for early exit.
        private const long NoFans = 0L;
        // Cap for cumulative pie fills.
        private const float MaxPieFill = 1f;

        // Fixes age-pie rendering so the stacked slices match the teen/YA/adult ratios.
        public static void Postfix(Profile_Fans_Pies __instance, data_girls.girls ___Girl)
        {
            // Skip when there are no fans to render, avoiding divide-by-zero ratios in vanilla code.
            if (___Girl.GetFans_Total() == NoFans)
                return;

            Image teenImage = __instance.Fans_Pie_Teen.GetComponent<Image>();
            Image yaImage = __instance.Fans_Pie_YA.GetComponent<Image>();
            Image adultImage = __instance.Fans_Pie_Adult.GetComponent<Image>();

            float teenRatio = teenImage.fillAmount;
            float yaRatio = yaImage.fillAmount;
            // Base game sets Adult fill to adult + teen, so subtract teen to recover the adult slice.
            float adultRatio = adultImage.fillAmount - teenRatio;
            if (adultRatio < 0f)
            {
                adultRatio = 0f;
            }

            float totalRatio = teenRatio + yaRatio + adultRatio;
            // Guard against rounding overshoot by scaling to the expected 0..1 range.
            if (totalRatio > MaxPieFill && totalRatio > 0f)
            {
                float scale = MaxPieFill / totalRatio;
                teenRatio *= scale;
                yaRatio *= scale;
                adultRatio *= scale;
            }

            // The prefab renders Teen as the base image with Adult and YA as child images on top.
            // Use cumulative fills so the visible slices match the ratios (YA, then Adult, then Teen).
            adultImage.fillAmount = adultRatio + yaRatio;
            teenImage.fillAmount = adultRatio + yaRatio + teenRatio;
        }
    }

    // Keeps tour expected revenue text color aligned with profitability.
    [HarmonyPatch(typeof(Tour_New_Popup), "Render")]
    public class Tour_New_Popup_Render
    {
        // Keeps the expected revenue color consistent by always evaluating profitability after savings.
        public static void Postfix(Tour_New_Popup __instance)
        {
            // Apply savings before comparing against expected revenue.
            long effectiveCost = __instance.Tour.ProductionCost - __instance.Tour.Saving;
            // Positive profit should be green; losses should be red.
            bool profitable = __instance.Tour.ExpectedRevenue > effectiveCost;
            ExtensionMethods.SetColor(__instance.ExpectedRevenue, profitable ? mainScript.green32 : mainScript.red32);
        }
    }

    // Restores stamina costs for theater schedules. The game works out the cost (5 for a performance, 2 for
    // manzai, doubled on hard), then returns 0 instead of it; return the cost it worked out.
    [HarmonyPatch(typeof(Theaters), nameof(Theaters.GetStaminaCost))]
    public class Theaters_GetStaminaCost
    {
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

            // Load the cost instead, keeping any jump into this instruction.
            CodeInstruction loadCost = IlHelpers.LoadOfStore(storeCost);
            matcher.Instruction.opcode = loadCost.opcode;
            matcher.Instruction.operand = loadCost.operand;
            return matcher.InstructionEnumeration();
        }
    }

    // Aligns theater revenue timing and payout distribution with the schedule.
    [HarmonyPatch(typeof(Theaters), "CompleteDay")]
    public class Theaters_CompleteDay
    {
        // Day-of-month used by the base game for subscription revenue.
        private const int FirstDayOfMonth = 1;

        // Fixed Theater so that revenue stats are not offset by one day
        public static void Prefix()
        {
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                // Fix so that auto schedules contribute revenue on the day of
                theater.Doing_Now = theater.GetSchedule().Type;
            }
        }

        // Fixes revenue accounting and income distribution after the base method completes.
        public static void Postfix()
        {
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                if (theater == null || theater.Stats == null || theater.Stats.Count == 0)
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
            if (theater.Stats.Count == 0)
                return null;

            double total = 0;
            int countedDays = 0;
            for (int index = theater.Stats.Count - 1; index >= Math.Max(0, theater.Stats.Count - DaysInWeek); index--)
            {
                Theaters._theater._stat stat = theater.Stats[index];
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
    [HarmonyPatch(typeof(Theaters._theater), "GetAvgAttendance")]
    public class Theaters__theater_GetAvgAttendance
    {
        public static void Postfix(ref int __result, Theaters._theater __instance)
        {
            int? average = TheaterAverages.OfShowDays(__instance, stat => stat.Attendance);
            if (average != null)
                __result = average.Value;
        }
    }

	// Fixed Theater so that average stats ignore days off
    [HarmonyPatch(typeof(Theaters._theater), "GetAvgRevenue")]
    public class Theaters__theater_GetAvgRevenue
    {
        public static void Postfix(ref int __result, Theaters._theater __instance)
        {
            int? average = TheaterAverages.OfShowDays(__instance, stat => stat.Revenue);
            if (average != null)
                __result = average.Value;
        }
    }


	// Fixed Theater so that money tooltip includes 7 days instead of 6, and include sub revenue
    [HarmonyPatch(typeof(Theaters), "GetLastWeekEarning")]
    public class Theaters_GetLastWeekEarning
    {
        // Tooltip is intended to show a full week.
        private const int DaysInWeek = 7;
        // Average weeks per month, to spread the monthly subscription revenue over a week.
        private const double SubRevenueWeeksPerMonth = 4.35;

        public static void Postfix(ref long __result)
        {
            // Start with the base value from the game.
            long output = __result;
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
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
    }

	// Fixed Cafe so that money tooltip includes 7 days instead of 6
    [HarmonyPatch(typeof(Cafes), "GetLastWeekEarning")]
    public class Cafes_GetLastWeekEarning
    {
        // Tooltip is intended to show a full week.
        private const int DaysInWeek = 7;

        public static void Postfix(ref int __result)
        {
            // Recount the week in 64 bits: the game's int total wraps past ~2.1 billion.
            long output = 0L;
            foreach (Cafes._cafe cafe in Cafes.Cafes_)
            {
                // The last 7 days of each cafe; the game counts only 6.
                for (int i = Math.Max(cafe.Stats.Count - DaysInWeek, 0); i < cafe.Stats.Count; i++)
                {
                    output += cafe.Stats[i].Profit;
                }
            }
            // The tooltip reads an int, so cap the total instead of wrapping.
            __result = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, output));
        }
    }


	// Fixed so that when girls dating within the group break up, their relationship status is no longer known
    [HarmonyPatch(typeof(Relationships._relationship), "BreakUp")]
    public class Relationships__relationship_BreakUp
    {
        // Indices for the two relationship participants.
        private const int FirstPartnerIndex = 0;
        private const int SecondPartnerIndex = 1;

        public static void Prefix(Relationships._relationship __instance, ref bool __state)
        {
            // Capture whether the pair was dating before BreakUp clears the flag.
            __state = __instance != null && __instance.Dating;
        }

        public static void Postfix(Relationships._relationship __instance, bool __state)
        {
            // Only clear known status when the pair was actually dating.
            if (!__state || __instance == null || __instance.Girls == null || __instance.Girls.Count < 2)
                return;

            // Hide partner status for both sides after breakup.
            if (__instance.Girls[FirstPartnerIndex] != null)
            {
                __instance.Girls[FirstPartnerIndex].DatingData.Is_Partner_Status_Known = false;
            }
            if (__instance.Girls[SecondPartnerIndex] != null)
            {
                __instance.Girls[SecondPartnerIndex].DatingData.Is_Partner_Status_Known = false;
            }
        }
    }

        // Fixed Concert revenue formula so that it shows accurate estimated values
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), "GetRevenue")]
    public class SEvent_Concerts__concert__projectedValues_GetRevenue
    {
        // Hype curve configuration used by the adjusted revenue formula.
        private const float HypeBaseline = 1f;
        private const float LinearPointX0 = 0f;
        private const float LinearPointY0 = 0.5f;
        private const float LinearPointX1 = 1f;
        private const float LinearPointY1 = 0.25f;
        // Variable flag and multiplier for FUJI ticket bonus.
        private const string FujiTicketsVariable = "FUJI_3_TICKETS";
        private const string TrueValue = "true";
        private const float FujiTicketsMultiplier = 1.05f;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Locate the original GetHype call and our replacement method.
            MethodInfo getHype = AccessTools.Method(typeof(SEvent_Concerts._concert._projectedValues), "GetHype");
            MethodInfo infix   = AccessTools.Method(typeof(SEvent_Concerts__concert__projectedValues_GetRevenue), nameof(Infix));

            // Abort if Harmony lookup fails so we don't corrupt IL.
            if (getHype == null || infix == null)
            {
                PatchLog.WarnOncePerPatch<SEvent_Concerts__concert__projectedValues_GetRevenue>("GetHype/Infix method lookup failed.");
                return instructions;
            }

            var matcher = new CodeMatcher(instructions);
            matcher.MatchForward(false, new CodeMatch(ci => IlHelpers.IsCallTo(ci, getHype)));

            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<SEvent_Concerts__concert__projectedValues_GetRevenue>("GetHype call not found.");
                return instructions;
            }

            // Swap to our Infix method to apply the adjusted hype curve.
            matcher.Set(OpCodes.Call, infix);
            return matcher.InstructionEnumeration();
        }

        public static float Infix(SEvent_Concerts._concert._projectedValues __this)
        {
            // Start with the game's base hype calculation.
            float hype = __this.GetHype();

            // Club venues never use the post-100% hype curve in the base game.
            bool isClubVenue = __this.Parent != null && __this.Parent.Venue == SEvent_Concerts._venue.club;

            // Only reshape hype above the baseline (1.0) for non-club venues.
            if (!isClubVenue && hype > HypeBaseline)
            {
                // Avoid target-typed new() for max compatibility
                LinearFunction._function function = new LinearFunction._function();
                // Configure a linear mapping with points (0, 0.5) and (1, 0.25).
                function.Init(LinearPointX0, LinearPointY0, LinearPointX1, LinearPointY1);

                // Convert "hype above 1" into a scaled bonus, then re-add the baseline.
                float excessHype = hype - HypeBaseline;
                hype = excessHype * function.GetY(excessHype) + HypeBaseline;
            }

            // Apply the FUJI ticket bonus if enabled.
            if (variables.Get(FujiTicketsVariable) == TrueValue)
            {
                hype *= FujiTicketsMultiplier;
            }

            // Return the adjusted hype value.
            return hype;
        }
    }



	// Fixed Concert revenue formula so that it shows accurate estimated values
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), "GetString")]
    public class SEvent_Concerts__concert__projectedValues_GetString
    {
        // Hype is capped at 200% (2.0) by the base game.
        private const float MaxRatio = 2f;
        private const float PercentScale = 100f;
        // Absorbs float error so exact percents (e.g. 0.29) don't floor one point low.
        private const float FloorTolerance = 0.0001f;

        public static void Prefix(ref float _val)
        {
            // Clamp ratios so hype does not exceed its intended 200% cap.
            if (_val > MaxRatio)
            {
                _val = MaxRatio;
            }
            // The game rounds to the nearest percent, so 99.5-99.9% attendance showed as 100% (sold out).
            // Round down instead; the game's rounding then keeps the whole percent.
            _val = Mathf.Floor(_val * PercentScale + FloorTolerance) / PercentScale;
        }
    }

	// Fixed senbatsu stats calculation so it doesn't punish you if you don't have enough idols to fill all rows
    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class singles__single_SenbatsuCalcParam
    {
        // Percent scaling constant used by the base formula.
        private const float PercentScale = 100f;
        // IL pattern length for the 100f / rows divisor.
        private const int DivPatternLength = 4;
        // Senbatsu row limits and guard values.
        private const int NoIdols = 0;
        private const int MinRows = 1;
        private const int MaxRows = 5;
        // Constants for triangular number inversion.
        private const float TriangularScale = 8f;
        private const float TriangularOffset = 1f;
        private const float TriangularDivisor = 2f;
        private const float ZeroPercent = 0f;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Resolve the infix method that computes percent based on the rows the agency can fill.
            MethodInfo infix = AccessTools.Method(typeof(singles__single_SenbatsuCalcParam), nameof(Infix));
            MethodInfo countGirls = AccessTools.Method(typeof(Groups._group), nameof(Groups._group.GetNumberOfNonGraduatedGirls));
            if (infix == null || countGirls == null)
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("Infix or GetNumberOfNonGraduatedGirls lookup failed.");
                return instructions;
            }

            var matcher = new CodeMatcher(instructions);
            // Find the local holding the agency's idol count (num): the group's count, plus the other groups' for the main group.
            matcher.MatchForward(false,
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, countGirls)),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("idol count local not found.");
                return instructions;
            }
            CodeInstruction storeIdolCount = matcher.InstructionAt(1);

            matcher.Start();
            // Find the sequence that divides 100f by the row count (num2).
            matcher.MatchForward(false,
                new CodeMatch(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float value && value == PercentScale),
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(OpCodes.Conv_R4),
                new CodeMatch(OpCodes.Div));

            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("100/rows divisor not found.");
                return instructions;
            }

            // Ensure the divisor result is immediately stored in a local.
            CodeInstruction storeInstruction = matcher.InstructionAt(DivPatternLength);
            if (storeInstruction == null || !IlHelpers.IsStloc(storeInstruction))
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("100/rows divisor found, but store opcode not found.");
                return instructions;
            }

            // Replace 100f / num2 with Infix(num) to count only the rows the agency can fill completely.
            var labels = matcher.Instruction.labels.ToList();
            var blocks = matcher.Instruction.blocks.ToList();
            matcher.RemoveInstructions(DivPatternLength);
            var loadIdolCount = IlHelpers.LoadOfStore(storeIdolCount);
            loadIdolCount.labels.AddRange(labels);
            loadIdolCount.blocks.AddRange(blocks);
            matcher.Insert(loadIdolCount, new CodeInstruction(OpCodes.Call, infix));
            return matcher.InstructionEnumeration();
        }

        public static float Infix(int idolCount)
        {
            // Total rows in the senbatsu formation:
            // 1, 2, 3, 4, 5  (total capacity = 15)
            // Safety: if no idols, don't divide by zero.
            if (idolCount <= NoIdols)
                return ZeroPercent;

            // Triangular number inversion:
            // r represents the number of rows the agency's idols (n) can fill completely:
            // the largest r such that r(r+1)/2 <= n.
            // A row the agency can't fill doesn't count against the single; idols placed in it add on top.
            //
            // r = floor((sqrt(8N + 1) - 1) / 2)
            float n = idolCount;
            float r = (Mathf.Sqrt(TriangularScale * n + TriangularOffset) - TriangularOffset) / TriangularDivisor;

            // Round down to the last complete row.
            int rowsUsed = Mathf.FloorToInt(r);

            // Clamp to the real formation size:
            // Anything above 15 idols still just uses all 5 rows.
            rowsUsed = Mathf.Clamp(rowsUsed, MinRows, MaxRows);

            // The game wants a "percentage per used row" kind of factor.
            return PercentScale / rowsUsed;
        }

    }

    // Fix senbatsu parameter queries to use the requested param type. The game passes cute (0) to
    // SenbatsuCalcParam whatever type was asked for; pass the Type argument instead.
    [HarmonyPatch(typeof(singles._single), nameof(singles._single.GetSenbatsuParamValue))]
    public class singles__single_GetSenbatsuParamValue
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo calcParam = AccessTools.Method(typeof(singles._single), "SenbatsuCalcParam",
                new Type[] { typeof(List<data_girls.girls>), typeof(data_girls._paramType), typeof(Groups._group) });
            if (calcParam == null)
            {
                PatchLog.WarnOncePerPatch<singles__single_GetSenbatsuParamValue>("SenbatsuCalcParam lookup failed.");
                return instructions;
            }

            // SenbatsuCalcParam(girls, cute, null): the type is the second argument pushed before the call.
            var matcher = new CodeMatcher(instructions).MatchStartForward(
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, (int)data_girls._paramType.cute)),
                new CodeMatch(OpCodes.Ldnull),
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, calcParam)));
            if (matcher.IsInvalid)
            {
                // Already passes the type: patched twice, or the game fixed it.
                return instructions;
            }

            // GetSenbatsuParamValue(Type) is an instance method, so Type is argument 1.
            matcher.Instruction.opcode = OpCodes.Ldarg_1;
            matcher.Instruction.operand = null;
            return matcher.InstructionEnumeration();
        }
    }

    // Fixed a freeze when generating some platinum and gold idols (auditions, Aya, hiring a rival).
    // The stat roll reserves up to 3 stats, then spends the rest of the points on the other stats, up to 99 each.
    // When the points can't fit (about 1 in 12 platinum rolls), the spending loop never ends.
    // The roll that can't fit is thrown away and the game rolls again, so every idol is one the game could make.
    [HarmonyPatch(typeof(data_girls), "GenerateParams")]
    public class data_girls_GenerateParams
    {
        // Highest value a stat can hold.
        private const int MaxStat = 99;
        // Points the game takes off for the 8 stats starting at 1.
        private const int StatCount = 8;
        // Rerolls before giving up and dropping the points that can't fit. Only reached if another mod
        // raises the points so far that no roll can fit; the game's points fail about 1 time in 12.
        private const int MaxRerolls = 100;

        // Rerolls in a row for the idol being generated.
        private static int rerolls;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            MethodInfo getPoints = AccessTools.Method(typeof(Auditions), nameof(Auditions.GetPointsByType));
            ConstructorInfo newList = AccessTools.Constructor(typeof(List<int>), Type.EmptyTypes);
            MethodInfo shouldReroll = AccessTools.Method(typeof(data_girls_GenerateParams), nameof(ShouldReroll));
            MethodInfo fitBudget = AccessTools.Method(typeof(data_girls_GenerateParams), nameof(FitBudget));
            if (getPoints == null || newList == null || shouldReroll == null || fitBudget == null)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("method lookup failed.");
                return instructions;
            }

            var matcher = new CodeMatcher(instructions, generator);

            // The points left to spend: GetPointsByType(Type) - 8.
            matcher.MatchForward(false,
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, getPoints)),
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, StatCount)),
                new CodeMatch(OpCodes.Sub),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("points local not found.");
                return instructions;
            }
            CodeInstruction storeBudget = matcher.InstructionAt(3);

            // The stat list.
            matcher.Start();
            matcher.MatchForward(false,
                new CodeMatch(ci => ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor == newList),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("stat list local not found.");
                return instructions;
            }
            CodeInstruction storeList = matcher.InstructionAt(1);

            // The end of the loop that adds a 1 for each stat that isn't reserved: i < 8 - reserved.
            // The spending loop starts right after it.
            matcher.Start();
            matcher.MatchForward(false,
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, StatCount)),
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(OpCodes.Sub),
                new CodeMatch(ci => ci.opcode == OpCodes.Blt || ci.opcode == OpCodes.Blt_S));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("spending loop not found.");
                return instructions;
            }
            CodeInstruction loadReserved = matcher.InstructionAt(1);
            matcher.Advance(4);
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("spending loop start not found.");
                return instructions;
            }

            // A reroll jumps back to the method's first instruction.
            Label start = generator.DefineLabel();
            matcher.InstructionAt(-matcher.Pos).labels.Add(start);

            // Before spending: if (ShouldReroll(list, reserved, points)) start again;
            // then points = FitBudget(list, reserved, points).
            var labels = matcher.Instruction.labels.ToList();
            matcher.Instruction.labels.Clear();
            var firstInserted = IlHelpers.LoadOfStore(storeList);
            firstInserted.labels.AddRange(labels);
            matcher.Insert(
                firstInserted,
                new CodeInstruction(loadReserved.opcode, loadReserved.operand),
                IlHelpers.LoadOfStore(storeBudget),
                new CodeInstruction(OpCodes.Call, shouldReroll),
                new CodeInstruction(OpCodes.Brtrue, start),
                IlHelpers.LoadOfStore(storeList),
                new CodeInstruction(loadReserved.opcode, loadReserved.operand),
                IlHelpers.LoadOfStore(storeBudget),
                new CodeInstruction(OpCodes.Call, fitBudget),
                new CodeInstruction(storeBudget.opcode, storeBudget.operand));
            return matcher.InstructionEnumeration();
        }

        // How many more points the stats that aren't reserved can hold.
        public static int Room(List<int> stats, int reserved)
        {
            int room = 0;
            for (int i = reserved; i < stats.Count; i++)
            {
                room += Math.Max(0, MaxStat - stats[i]);
            }
            return room;
        }

        // True when the points can't fit, so the game should roll this idol again.
        public static bool ShouldReroll(List<int> stats, int reserved, int budget)
        {
            if (budget <= Room(stats, reserved))
            {
                rerolls = 0;
                return false;
            }

            if (rerolls >= MaxRerolls)
            {
                // FitBudget drops the points that can't fit instead.
                rerolls = 0;
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("no roll fit its points after " + MaxRerolls + " rerolls; dropped the extra points.");
                return false;
            }

            rerolls++;
            return true;
        }

        // The points to spend: unchanged when they fit, otherwise only what fits.
        public static int FitBudget(List<int> stats, int reserved, int budget)
        {
            return Math.Min(budget, Room(stats, reserved));
        }
    }


    // Dating status is visible for underage members. The game returns "" unless the idol is an adult
    // (Is_AOC); treat every idol as one, so the rest of the game's own method builds the text.
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetPartnerString))]
    public class data_girls_girls_GetPartnerString
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo isAoc = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.Is_AOC));
            var matcher = new CodeMatcher(instructions).MatchStartForward(new CodeMatch(ci => IlHelpers.IsCallTo(ci, isAoc)));
            if (matcher.IsInvalid)
            {
                // Already gone: patched twice, or the game fixed it.
                return instructions;
            }

            // this.Is_AOC() becomes true: drop "this", push 1.
            matcher.Instruction.opcode = OpCodes.Pop;
            matcher.Instruction.operand = null;
            matcher.Advance(1).Insert(new CodeInstruction(OpCodes.Ldc_I4_1));
            return matcher.InstructionEnumeration();
        }
    }


    // Fixed fan opinion to be impacted by concerts, SSK/show cancellation and random events
    [HarmonyPatch(typeof(resources._fanOpinion), "Add")]
    public class resources__fanOpinion_Add
    {
        // The game counts val down to its fraction as it records whole points, so keep the full change.
        public static void Prefix(float val, out float __state)
        {
            __state = val;
        }

        public static void Postfix(resources._fanOpinion __instance, float __state)
        {
            // Propagate global fan opinion changes to each active girl.
            foreach (data_girls.girls girl in data_girls.girl)
            {
                // Skip null entries, sick girls, and graduates who should not gain appeal.
                if (girl != null && !girl.IsSick() && girl.status != data_girls._status.graduated)
                {
                    // Apply the appeal delta for the matching fan type.
                    girl.AddAppeal(__instance.type, __state);
                }
            }
        }
    }


    // Fixed gossip so girl doesn't gossip about herself
    [HarmonyPatch(typeof(Date_Gossip), "GetAvailableGossips")]
    public class Date_Gossip_GetAvailableGossips
    {
        public static void Postfix(ref List<Date_Gossip._gossip> __result, data_girls.girls Snitch)
        {
            // Nothing to filter if the list is empty.
            if (__result.Count == 0)
            {
                return;
            }
            // Walk backwards so removals do not affect remaining indices.
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                // Remove any gossip targeting the snitch herself.
                if (__result[i].BullyingTarget == Snitch)
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }


    // Fixed event and dialogue checks for Influence to check for Influence instead of Friendship
    [HarmonyPatch(typeof(vn_requirements), "CheckGirl", new Type[] { typeof(data_girls.girls), typeof(string), typeof(string) })]
    public class vn_requirements_CheckGirl
    {
        // Requirement key used by the game for influence checks.
        private const string InfluenceParameter = "influence";

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Resolve the relationship checker to ensure we update the correct call site.
            MethodInfo checkRelationship = AccessTools.Method(
                typeof(vn_requirements),
                "CheckRelationship",
                new Type[] { typeof(data_girls.girls), typeof(string), typeof(Relationships_Player._type) });
            MethodInfo stringEquals = AccessTools.Method(typeof(string), "op_Equality", new Type[] { typeof(string), typeof(string) });
            if (checkRelationship == null || stringEquals == null)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("CheckRelationship or string equality lookup failed.");
                return instructions;
            }

            List<CodeInstruction> code = new List<CodeInstruction>(instructions);

            // The switch compares the parameter with "influence", then branches: to the case on true, or past
            // it on false (the case then follows the branch).
            int compare = code.FindIndex(ci => ci.opcode == OpCodes.Ldstr && ci.operand is string text && text == InfluenceParameter);
            if (compare < 0 || compare + 2 >= code.Count || !IlHelpers.IsCallTo(code[compare + 1], stringEquals))
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("\"influence\" comparison not found.");
                return code;
            }
            CodeInstruction branch = code[compare + 2];
            int caseStart;
            if (IlHelpers.IsBranchTrue(branch))
            {
                caseStart = code.FindIndex(ci => ci.labels.Contains((Label)branch.operand));
            }
            else if (branch.opcode == OpCodes.Brfalse || branch.opcode == OpCodes.Brfalse_S)
            {
                caseStart = compare + 3;
            }
            else
            {
                caseStart = -1;
            }
            if (caseStart < 0)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("\"influence\" case not found.");
                return code;
            }

            // The case's call: CheckRelationship(girl, formula, Friendship), the type pushed just before it.
            int call = code.FindIndex(caseStart, ci => IlHelpers.IsCallTo(ci, checkRelationship) || ci.opcode == OpCodes.Ret);
            if (call <= caseStart || code[call].opcode == OpCodes.Ret)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("CheckRelationship call not found in the \"influence\" case.");
                return code;
            }

            CodeInstruction type = code[call - 1];
            if (IlHelpers.IsLdcI4(type, (int)Relationships_Player._type.Friendship))
            {
                // Replace Friendship with Influence.
                type.opcode = OpCodes.Ldc_I4_2;
                type.operand = null;
                return code;
            }

            if (IlHelpers.IsLdcI4(type, (int)Relationships_Player._type.Influence))
            {
                // Already patched or game fixed it upstream.
                return code;
            }

            PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("unexpected type before the \"influence\" CheckRelationship call.");
            return code;
        }
    }

    // Fix "variable" requirements to respect leading negation. The game sees the "!", then throws away
    // formula.Substring(1), so "!met_fan" checks a variable named "!met_fan". Store it back into formula.
    [HarmonyPatch(typeof(vn_requirements), "CheckGirl", new Type[] { typeof(data_girls.girls), typeof(string), typeof(string) })]
    public class vn_requirements_CheckGirl_Variable
    {
        // CheckGirl(girl, parameter, formula) is static, so formula is argument 2.
        private const int FormulaArgument = 2;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo substring = AccessTools.Method(typeof(string), nameof(string.Substring), new Type[] { typeof(int) });

            // formula.Substring(1); with the result dropped.
            var matcher = new CodeMatcher(instructions).MatchStartForward(
                new CodeMatch(ci => IlHelpers.IsLdarg(ci, FormulaArgument)),
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, 1)),
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, substring)),
                new CodeMatch(OpCodes.Pop));
            if (matcher.IsInvalid)
            {
                // Already stored: patched twice, or the game fixed it.
                return instructions;
            }

            // formula = formula.Substring(1);
            matcher.Advance(3);
            matcher.Instruction.opcode = OpCodes.Starg_S;
            matcher.Instruction.operand = (byte)FormulaArgument;
            return matcher.InstructionEnumeration();
        }
    }


    // Fix stamina cost of performance thumbnail if Energetic policy
    [HarmonyPatch(typeof(Activities._activity), "GetDescription")]
    public class Activities__activity_GetDescription
    {
        // The Energetic policy's stamina cost, as Activities.GetStaminaCost charges it (-4f). The unit tests
        // check the two match; that method needs the scene's Activities component, which a description can't reach.
        private const int EnergeticStaminaCost = 4;
        private const string PointsKey = "PT";
        private const string StaminaKey = "STAMINA";
        private const string CostPrefix = "-";
        private const string CostSeparator = " ";

        public static void Postfix(Activities._activity __instance, ref string __result)
        {
            // Only adjust performance activities when Energetic policy is active.
            if (__instance.type == Activity._type.performance && policies.GetSelectedPolicyValue(policies._type.performances)?.Value == policies._value.performances_energy)
            {
                // Build a localized "-4 PT stamina" string for the thumbnail.
                __result = string.Concat(new object[]
                {
                    CostPrefix,
                    EnergeticStaminaCost,
                    Language.Data[PointsKey],
                    CostSeparator,
                    Language.Data[StaminaKey].ToLower()
                });
            }
        }
    }


    // Fix custom event check for "hired a staffer of type"
    [HarmonyPatch(typeof(vn_requirements), "CheckMeta")]
    public class vn_requirements_CheckMeta
    {
        // Metadata parameter and formula keys.
        private const string StaffParameter = "staff";
        private const string VocalFormula = "vocal";
        private const string DanceFormula = "dance";
        private const string OfficeFormula = "office";
        private const string StyleFormula = "style";
        private const int MinimumStaffCount = 0;

        public static void Postfix(string parameter, string formula, ref bool __result)
        {
            // Only handle staff-type checks; let other meta parameters remain unchanged.
            if (parameter != StaffParameter)
                return;

            // Map formula keys to their matching staff room types.
            if (formula == VocalFormula && staff.CountStaffersOfType(agency._type.recordingStudio) > MinimumStaffCount)
            {
                __result = true;
            }
            else if(formula == DanceFormula && staff.CountStaffersOfType(agency._type.danceStudio) > MinimumStaffCount)
            {
                __result = true;
            }
            else if(formula == OfficeFormula && staff.CountStaffersOfType(agency._type.office) > MinimumStaffCount)
            {
                __result = true;
            }
            else if(formula == StyleFormula && staff.CountStaffersOfType(agency._type.dressingRoom) > MinimumStaffCount)
            {
                __result = true;
            }

            
        }
    }

    // Base game bugfix:
    // SaveManager.LoadData(...) assigns SaveManager.Data = null when the target file is missing/corrupt,
    // then returns early ("huh"). Autosave later calls SaveEvent, and staticVars.SaveFunction crashes
    // because Camera.main.GetComponent<mainScript>().GetSavedData() is null.
    //
    // Fix:
    // During play, a failed load leaves the game untouched, so the previous SaveManager.Data is restored
    // and autosave keeps saving the game being played.
    // From the main menu, the game scene opens before the file is read, so a failed load leaves an empty game.
    // Data stays null and that scene's autosaves are skipped, so the empty game can't overwrite a save.
    public static class FailedLoadGuard
    {
        private const string EmptyGameError =
            "The save could not be loaded, so this game is empty. Autosave is off for this session so it can't overwrite your saves. Return to the main menu and load another save.";

        // Set while the main menu loads a save into the game scene it has just opened.
        internal static bool MenuLoadPending;

        // The save manager of a game scene whose save failed to load from the main menu.
        // Cleared by the next successful load, or when the main menu starts another game.
        internal static SaveManager BlockedSaveManager;

        // What the LoadData prefixes record for their postfixes.
        public struct LoadState
        {
            public SaveManager.SavedData Previous;
            public bool FromMenu;
        }

        internal static LoadState BeforeLoad(SaveManager manager)
        {
            LoadState state = new LoadState
            {
                Previous = manager != null ? manager.Data : null,
                FromMenu = MenuLoadPending,
            };
            MenuLoadPending = false;
            return state;
        }

        internal static void AfterLoad<TPatch>(SaveManager manager, LoadState state, string restoredWarning)
        {
            if (manager == null)
                return;

            if (manager.Data != null)
            {
                // The load worked, so this scene holds a real game again.
                if (ReferenceEquals(manager, BlockedSaveManager))
                    BlockedSaveManager = null;
                return;
            }

            if (!state.FromMenu && state.Previous != null)
            {
                manager.Data = state.Previous;
                PatchLog.WarnOncePerPatch<TPatch>(restoredWarning);
                return;
            }

            BlockedSaveManager = manager;
            PatchLog.Error<TPatch>(EmptyGameError);
        }
    }

    [HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] { typeof(bool) })]
    public class SaveManager_LoadData_Bool_NullGuard
    {
        private const string WarningMessage =
            "LoadData(bool) left SaveManager.Data null. Restored previous in-memory save data to prevent SaveEvent/autosave null crash.";

        public static void Prefix(SaveManager __instance, ref FailedLoadGuard.LoadState __state)
        {
            __state = FailedLoadGuard.BeforeLoad(__instance);
        }

        public static void Postfix(SaveManager __instance, FailedLoadGuard.LoadState __state)
        {
            FailedLoadGuard.AfterLoad<SaveManager_LoadData_Bool_NullGuard>(__instance, __state, WarningMessage);
        }
    }

    // Same null-guard for the string-path overload used by some load flows and mods.
    [HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] { typeof(string) })]
    public class SaveManager_LoadData_Path_NullGuard
    {
        private const string WarningMessage =
            "LoadData(string) left SaveManager.Data null. Restored previous in-memory save data to prevent SaveEvent/autosave null crash.";

        public static void Prefix(SaveManager __instance, ref FailedLoadGuard.LoadState __state)
        {
            __state = FailedLoadGuard.BeforeLoad(__instance);
        }

        public static void Postfix(SaveManager __instance, FailedLoadGuard.LoadState __state)
        {
            FailedLoadGuard.AfterLoad<SaveManager_LoadData_Path_NullGuard>(__instance, __state, WarningMessage);
        }
    }

    // Marks loads the main menu makes into the game scene it has just opened. Starting another game
    // from the main menu also ends an earlier scene's autosave block.
    [HarmonyPatch(typeof(MainMenu_LoadGameManager), "LoadAsync")]
    public class MainMenu_LoadGameManager_LoadAsync
    {
        public static void Prefix(bool loadSave)
        {
            FailedLoadGuard.MenuLoadPending = loadSave;
            FailedLoadGuard.BlockedSaveManager = null;
        }
    }

    // Skips autosaves in a game scene whose save failed to load from the main menu.
    [HarmonyPatch(typeof(SaveManager), "SaveData", new Type[] { typeof(bool), typeof(bool) })]
    public class SaveManager_SaveData
    {
        public static bool Prefix(SaveManager __instance, bool autoSave)
        {
            return !autoSave || !ReferenceEquals(__instance, FailedLoadGuard.BlockedSaveManager);
        }
    }

    // Base game bugfix:
    // The audition popup keeps every card locked until all the candidates' portraits have loaded, and
    // portraits render one at a time through a queue the whole game shares. A render that never finished
    // kept the popup locked, and stopped every later portrait in the game for the rest of the session.
    //
    // Fix:
    // After 2 s the cards unlock. A card whose portrait hasn't loaded shows a clear placeholder: an unopened
    // card has no silhouette, an opened card no face, and the stats panel no image. Each portrait fills in
    // wherever it's shown when it arrives.
    // A render that takes over 2 s lets the queue move on and finishes in the background (at most 3 at once,
    // each for up to 60 s).
    // Cards can now close before their portraits load, so portrait loaders stop once everything they'd set a
    // portrait on is destroyed, and closing an audition drops the queued portraits of candidates who weren't hired.
    internal static class PortraitLoading
    {
        // Seconds the audition waits for portraits before it unlocks the cards.
        internal const float UnlockSeconds = 2f;
        // Seconds a portrait render can hold the shared queue.
        internal const float RenderSeconds = 2f;
        // Renders that can keep running after letting the queue move on.
        internal const int MaxLateRenders = 3;
        // Seconds such a render can keep running before it's stopped.
        internal const float LateRenderSeconds = 60f;

        private static readonly FieldInfo TexturesInstance = AccessTools.Field(typeof(data_girls_textures), "_this");
        private static readonly FieldInfo QueueField = AccessTools.Field(typeof(data_girls_textures), "Queue");
        private static readonly FieldInfo QueueGirl = AccessTools.Field(AccessTools.Inner(typeof(data_girls_textures), "_queue"), "Girl");
        private static readonly FieldInfo GoldenCardPortrait = AccessTools.Field(typeof(Audition_Golden_Card), "Portrait_");

        // The clear sprite on cards whose portrait hasn't loaded. Made the first time it's needed.
        internal static Sprite placeholder;
        // When the audition popup last laid out its cards.
        internal static float cardsLoadedAt;
        // Renderers still running after letting the queue move on.
        internal static readonly List<GameObject> lateRenderers = new List<GameObject>();

        internal static Sprite Placeholder
        {
            get
            {
                if (placeholder == null)
                    placeholder = CreatePlaceholder();
                return placeholder;
            }
        }

        private static Sprite CreatePlaceholder()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();
            // Closing the audition popup unloads unused assets; the placeholder is kept for the session.
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        internal static bool IsPlaceholder(Sprite sprite)
        {
            return !ReferenceEquals(sprite, null) && ReferenceEquals(sprite, placeholder);
        }

        // Real time, so a paused game still counts. A lambda, so tests can replace the clock.
        internal static Func<float> Clock = () => Time.realtimeSinceStartup;

        internal static float Now()
        {
            return Clock();
        }

        // The game's portrait loader and queue.
        internal static data_girls_textures Textures()
        {
            return TexturesInstance != null ? TexturesInstance.GetValue(null) as data_girls_textures : null;
        }

        // Runs a portrait loader until it finishes or every object it would set the portrait on is destroyed.
        // Its waits are checked here each frame, so it also stops while it's still waiting for the render.
        internal static IEnumerator WhileTargetsExist(IEnumerator loader, List<GameObject> targets)
        {
            while (AnyExists(targets) && loader.MoveNext())
            {
                CustomYieldInstruction wait = loader.Current as CustomYieldInstruction;
                if (wait == null)
                {
                    yield return loader.Current;
                    continue;
                }
                while (wait.keepWaiting)
                {
                    if (!AnyExists(targets))
                        yield break;
                    yield return null;
                }
            }
        }

        private static bool AnyExists(List<GameObject> targets)
        {
            foreach (GameObject target in targets)
            {
                if (target != null)
                    return true;
            }
            return false;
        }

        // A portrait that arrived after its card was opened: the opened card keeps a copy to show again when
        // she's clicked, and the stats panel gets it if it still shows her.
        internal static void ShowOpenedPortrait(Popup_Audition popup, Auditions.data._girl girl, Audition_Golden_Card card)
        {
            if (card == null || card.Portrait == null)
                return;
            Image image = card.Portrait.GetComponent<Image>();
            Sprite sprite = image != null ? image.sprite : null;
            // A load that failed is queued again and later sets only the opened card.
            if (sprite == null || IsPlaceholder(sprite))
                return;

            if (GoldenCardPortrait != null)
                GoldenCardPortrait.SetValue(card, sprite);

            Audition_Data_Card panel = popup != null && popup.Card_Container != null ? popup.Card_Container.GetComponent<Audition_Data_Card>() : null;
            if (panel == null || !ReferenceEquals(panel.Girl, girl))
                return;
            SetSprite(panel.Portrait, sprite);
            SetSprite(panel.Portrait_Shadow, sprite);
        }

        private static void SetSprite(GameObject obj, Sprite sprite)
        {
            Image image = obj != null ? obj.GetComponent<Image>() : null;
            if (image != null)
                image.sprite = sprite;
        }

        // Removes the queued portrait jobs of these idols. A job that's already running finishes.
        internal static int DropQueuedPortraits(ICollection<data_girls.girls> girls)
        {
            IList queue = QueueField != null ? QueueField.GetValue(null) as IList : null;
            if (queue == null || QueueGirl == null || girls.Count == 0)
                return 0;

            int dropped = 0;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (queue[i] != null && QueueGirl.GetValue(queue[i]) is data_girls.girls girl && girls.Contains(girl))
                {
                    queue.RemoveAt(i);
                    dropped++;
                }
            }
            return dropped;
        }

        // Renders still running after letting the queue move on. A scene change destroys them.
        internal static int LateRenderCount()
        {
            lateRenderers.RemoveAll(r => r == null);
            return lateRenderers.Count;
        }

        // The queue's wait for a portrait render: until it finishes, or once 2 s have passed while fewer than
        // 3 renders are already running late.
        public static WaitUntil WaitForRender(Func<bool> rendered)
        {
            float deadline = Now() + RenderSeconds;
            return new WaitUntil(() => rendered() || (Now() >= deadline && LateRenderCount() < MaxLateRenders));
        }

        // Called where the queue destroys the renderer. A render that hasn't finished keeps running.
        public static void DestroyRenderer(GameObject renderer)
        {
            Portrait_Renderer portrait = renderer != null ? renderer.GetComponent<Portrait_Renderer>() : null;
            data_girls.girls girl = portrait != null ? portrait.Girl : null;
            data_girls_textures textures = Textures();
            if (girl == null || girl.texture == null || girl.texture.cached || textures == null)
            {
                UnityEngine.Object.Destroy(renderer);
                return;
            }

            lateRenderers.Add(renderer);
            PatchLog.WarnOncePerPatch<data_girls_textures_NEW_Cache_Portrait>(
                "a portrait took over " + RenderSeconds + " s to render, so the next portrait started while it finishes.");
            textures.StartCoroutine(FinishLateRender(renderer, girl));
        }

        private static IEnumerator FinishLateRender(GameObject renderer, data_girls.girls girl)
        {
            float stopAt = Now() + LateRenderSeconds;
            while (renderer != null && !girl.texture.cached && Now() < stopAt)
                yield return null;

            lateRenderers.RemoveAll(r => ReferenceEquals(r, renderer));
            if (renderer != null)
                UnityEngine.Object.Destroy(renderer);

            if (!girl.texture.cached)
            {
                PatchLog.Warn<data_girls_textures_NEW_Cache_Portrait>("a portrait render didn't finish in " + LateRenderSeconds + " s and was stopped.");
                yield break;
            }
            // What the queue does after a render.
            if (girl.Update != null)
                girl.Update();
            if (girl.TexturesUpdate != null)
                girl.TexturesUpdate();
        }
    }

    // Starts the 2 s wait for the audition's portraits.
    [HarmonyPatch(typeof(Popup_Audition), "LoadCards")]
    public class Popup_Audition_LoadCards
    {
        public static void Prefix()
        {
            PortraitLoading.cardsLoadedAt = PortraitLoading.Now();
        }
    }

    // After 2 s the audition's cards unlock; those still without a portrait get the clear placeholder.
    // Each card's loader keeps running and sets her portrait when it arrives.
    [HarmonyPatch(typeof(Popup_Audition), "PortraitsLoaded")]
    public class Popup_Audition_PortraitsLoaded
    {
        public static void Postfix(Popup_Audition __instance, ref bool __result)
        {
            try
            {
                if (__result || PortraitLoading.Now() - PortraitLoading.cardsLoadedAt < PortraitLoading.UnlockSeconds)
                    return;

                foreach (Audition_Closed_Card card in __instance.Cards_Container.GetComponentsInChildren<Audition_Closed_Card>(true))
                {
                    Image image = card != null && card.Portrait != null ? card.Portrait.GetComponent<Image>() : null;
                    if (image != null && image.sprite == null)
                        image.sprite = PortraitLoading.Placeholder;
                }
                __result = true;
            }
            catch (Exception ex)
            {
                PatchLog.WarnOncePerPatch<Popup_Audition_PortraitsLoaded>("failed: " + ex);
            }
        }
    }

    // A card opened before her portrait arrived starts empty. The card being opened is destroyed, which stops
    // its loader, so the opened card loads the portrait itself.
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.OpenCard))]
    public class Popup_Audition_OpenCard
    {
        public static void Postfix(Popup_Audition __instance, Auditions.data._girl girl, Sprite portrait)
        {
            try
            {
                if (!PortraitLoading.IsPlaceholder(portrait) || girl == null || girl.girl == null || girl.CardObject == null)
                    return;

                Audition_Golden_Card card = girl.CardObject.GetComponent<Audition_Golden_Card>();
                data_girls_textures textures = PortraitLoading.Textures();
                if (card == null || textures == null)
                    return;

                List<GameObject> targets = new List<GameObject> { card.Portrait, card.PortraitShadow };
                textures.StartCoroutine(textures.setPortrait(girl.girl, targets, 0f, () => PortraitLoading.ShowOpenedPortrait(__instance, girl, card)));
            }
            catch (Exception ex)
            {
                PatchLog.WarnOncePerPatch<Popup_Audition_OpenCard>("failed: " + ex);
            }
        }
    }

    // Closing an audition drops the queued portraits of candidates who weren't hired, so the queue doesn't
    // render faces nobody will see before the next portrait the game needs.
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.Close))]
    public class Popup_Audition_Close
    {
        private static readonly FieldInfo DataField = AccessTools.Field(typeof(Popup_Audition), "Data");

        public static void Prefix(Popup_Audition __instance)
        {
            try
            {
                Auditions.data data = DataField != null ? DataField.GetValue(__instance) as Auditions.data : null;
                if (data == null || data.Girls == null)
                    return;

                HashSet<data_girls.girls> notHired = new HashSet<data_girls.girls>();
                foreach (Auditions.data._girl candidate in data.Girls)
                {
                    if (candidate != null && candidate.girl != null && (data_girls.girl == null || !data_girls.girl.Contains(candidate.girl)))
                        notHired.Add(candidate.girl);
                }
                PortraitLoading.DropQueuedPortraits(notHired);
            }
            catch (Exception ex)
            {
                PatchLog.WarnOncePerPatch<Popup_Audition_Close>("failed: " + ex);
            }
        }
    }

    // Portrait loaders stop once everything they'd set the portrait on is destroyed. The game's loader kept
    // running, then threw when it reached the destroyed object.
    [HarmonyPatch(typeof(data_girls_textures), nameof(data_girls_textures.setPortrait))]
    public class data_girls_textures_setPortrait
    {
        public static void Postfix(List<GameObject> target, ref IEnumerator __result)
        {
            if (__result != null && target != null && target.Count > 0)
                __result = PortraitLoading.WhileTargetsExist(__result, target);
        }
    }

    // Skips setting a portrait on a destroyed object. The queue's job that retries a failed portrait load sets
    // it before starting the next job, so the error stopped every later portrait for the rest of the session.
    [HarmonyPatch(typeof(data_girls_textures), "SetSprite")]
    public class data_girls_textures_SetSprite
    {
        public static bool Prefix(GameObject obj)
        {
            return obj != null;
        }
    }

    // A portrait render that takes over 2 s lets the queue move on, and finishes in the background.
    // Each renderer has its own camera and render texture and is placed apart from the others, so renders
    // can run side by side.
    [HarmonyPatch(typeof(data_girls_textures), "NEW_Cache_Portrait", MethodType.Enumerator)]
    public class data_girls_textures_NEW_Cache_Portrait
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            ConstructorInfo newWait = AccessTools.Constructor(typeof(WaitUntil), new Type[] { typeof(Func<bool>) });
            MethodInfo destroy = AccessTools.Method(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Destroy), new Type[] { typeof(UnityEngine.Object) });
            MethodInfo waitForRender = AccessTools.Method(typeof(PortraitLoading), nameof(PortraitLoading.WaitForRender));
            MethodInfo destroyRenderer = AccessTools.Method(typeof(PortraitLoading), nameof(PortraitLoading.DestroyRenderer));
            if (newWait == null || destroy == null || waitForRender == null || destroyRenderer == null)
            {
                PatchLog.WarnOncePerPatch<data_girls_textures_NEW_Cache_Portrait>("method lookup failed.");
                return instructions;
            }

            List<CodeInstruction> codes = instructions.ToList();
            List<CodeInstruction> waits = codes.Where(ci => ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor == newWait).ToList();
            List<CodeInstruction> destroys = codes.Where(ci => IlHelpers.IsCallTo(ci, destroy)).ToList();
            if (waits.Count != 1 || destroys.Count != 1)
            {
                PatchLog.WarnOncePerPatch<data_girls_textures_NEW_Cache_Portrait>(
                    "expected one render wait and one renderer destroy, found " + waits.Count + " and " + destroys.Count + ".");
                return codes;
            }

            waits[0].opcode = OpCodes.Call;
            waits[0].operand = waitForRender;
            destroys[0].opcode = OpCodes.Call;
            destroys[0].operand = destroyRenderer;
            return codes;
        }
    }

}
