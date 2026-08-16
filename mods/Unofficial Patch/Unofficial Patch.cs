using HarmonyLib;
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
        // Empty-count sentinel for collection checks.
        private const int EmptyCount = 0;

        // Protects the once-only warning cache.
        private static readonly object OnceLock = new object();
        // Tracks warnings that were already emitted to prevent log spam.
        private static readonly HashSet<string> OnceKeys = new HashSet<string>();

        // Logs a warning for the given patch type.
        public static void Warn<TPatch>(string message)
        {
            Warn(typeof(TPatch), message);
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

        // Builds a readable Harmony target name, falling back to UnknownTarget if metadata is missing.
        private static string GetTargetName(Type patchType, out bool usedFallback)
        {
            usedFallback = true;
            if (patchType == null)
                return UnknownTarget;

            object[] attrs = patchType.GetCustomAttributes(typeof(HarmonyPatch), true);
            if (attrs == null || attrs.Length == EmptyCount)
                return UnknownTarget;

            List<string> targets = new List<string>();
            foreach (object attr in attrs)
            {
                string target = GetTargetName(attr);
                if (!string.IsNullOrEmpty(target))
                {
                    targets.Add(target);
                }
            }

            if (targets.Count == EmptyCount)
                return UnknownTarget;

            usedFallback = false;
            return string.Join(", ", targets.Distinct());
        }

        // Extracts HarmonyPatch info without hard dependency on a specific Harmony version.
        private static string GetTargetName(object patchAttribute)
        {
            if (patchAttribute == null)
                return null;

            object info = GetMember(patchAttribute, "info");
            Type declaringType = GetMember<Type>(info, "declaringType") ?? GetMember<Type>(patchAttribute, "declaringType");
            string methodName = GetMember<string>(info, "methodName") ?? GetMember<string>(patchAttribute, "methodName");
            Type[] argumentTypes = GetMember<Type[]>(info, "argumentTypes") ?? GetMember<Type[]>(patchAttribute, "argumentTypes");
            object methodType = GetMember(info, "methodType") ?? GetMember(patchAttribute, "methodType");

            string typeName = declaringType != null ? (declaringType.FullName ?? declaringType.Name) : null;
            string signature = FormatArgs(argumentTypes);

            if (!string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(methodName))
                return typeName + "." + methodName + signature;

            if (!string.IsNullOrEmpty(typeName))
                return typeName;

            if (!string.IsNullOrEmpty(methodName))
                return methodName + signature;

            if (methodType != null)
                return methodType.ToString();

            return null;
        }

        // Reads a private field/property by name using reflection.
        private static object GetMember(object instance, string name)
        {
            if (instance == null)
                return null;

            Type type = instance.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
                return property.GetValue(instance, null);

            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(instance);

            return null;
        }

        // Strongly-typed wrapper around GetMember.
        private static T GetMember<T>(object instance, string name) where T : class
        {
            return GetMember(instance, name) as T;
        }

        // Formats argument type lists to help log target signatures.
        private static string FormatArgs(Type[] argumentTypes)
        {
            if (argumentTypes == null)
                return string.Empty;
            if (argumentTypes.Length == EmptyCount)
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
        public static void Postfix(ref Tour_New_Popup __instance)
        {
            // Apply savings before comparing against expected revenue.
            long effectiveCost = __instance.Tour.ProductionCost - __instance.Tour.Saving;
            // Positive profit should be green; losses should be red.
            bool profitable = __instance.Tour.ExpectedRevenue > effectiveCost;
            ExtensionMethods.SetColor(__instance.ExpectedRevenue, profitable ? mainScript.green32 : mainScript.red32);
        }
    }

    // Restores stamina costs for theater schedules.
    [HarmonyPatch(typeof(Theaters), "GetStaminaCost")]
    public class Theaters_GetStaminaCost
    {
        // Stamina costs for theater schedules.
        private const float NoStaminaCost = 0f;
        private const float PerformanceStaminaCost = 5f;
        private const float ManzaiStaminaCost = 2f;
        private const float HardModeMultiplier = 2f;

        // Fixes the vanilla method which always returned 0.
        public static void Postfix(Theaters._theater._schedule._type Type, ref float __result)
        {
            // Start with no stamina cost by default.
            float staminaCost = NoStaminaCost;
            if (Type == Theaters._theater._schedule._type.performance)
            {
                staminaCost = PerformanceStaminaCost;
            }
            else if (Type == Theaters._theater._schedule._type.manzai)
            {
                staminaCost = ManzaiStaminaCost;
            }
            // Hard mode doubles the cost.
            if (staticVars.IsHard())
            {
                staminaCost *= HardModeMultiplier;
            }
            // Return the corrected stamina cost.
            __result = staminaCost;
        }
    }

    // Aligns theater revenue timing and payout distribution with the schedule.
    [HarmonyPatch(typeof(Theaters), "CompleteDay")]
    public class Theaters_CompleteDay
    {
        // Day-of-month used by the base game for subscription revenue.
        private const int FirstDayOfMonth = 1;
        // Sentinel values for revenue and counts.
        private const long NoRevenue = 0L;
        private const int NoGirls = 0;
        // Offset for accessing the latest stats entry.
        private const int LastIndexOffset = 1;

        // Fixes revenue accounting and income distribution after the base method completes.
        public static void Postfix()
        {
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                // Important: do not rewrite Doing_Now before vanilla runs.
                // Vanilla uses yesterday's Doing_Now at the beginning of CompleteDay to settle previous-day revenue.
                // We only read the stat row vanilla just wrote for "today" and correct payouts from that.
                if (theater == null || theater.Stats == null || theater.Stats.Count == 0)
                {
                    continue;
                }

                Theaters._theater._stat latestStat = theater.Stats[theater.Stats.Count - LastIndexOffset];
                if (latestStat == null || latestStat.Schedule == null)
                {
                    continue;
                }

                // Days off should contribute zero revenue to stats.
                if (latestStat.Schedule.Type == Theaters._theater._schedule._type.day_off)
                {
                    latestStat.Revenue = NoRevenue;
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
                int girlCount = girls != null ? girls.Count : NoGirls;
                if (girlCount <= NoGirls)
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

                if (payout <= NoRevenue)
                {
                    continue;
                }

                long split = payout / (long)girlCount;
                if (split <= NoRevenue)
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

	// Fixed Theater so that average stats ignore days off
    [HarmonyPatch(typeof(Theaters._theater), "GetAvgAttendance")]
    public class Theaters__theater_GetAvgAttendance
    {
        // Rolling window size for averages.
        private const int DaysInWeek = 7;
        private const int NoStats = 0;
        private const int NoDaysCounted = 0;
        private const int LastIndexOffset = 1;
        private const float ZeroAverage = 0f;

        public static void Postfix(ref int __result, Theaters._theater __instance)
        {
            // Skip if there are no stats to average.
            if (__instance.Stats.Count == NoStats)
                return;

            // Only inspect the most recent week (or fewer days if not enough data).
            int daysToCheck = Mathf.Min(__instance.Stats.Count, DaysInWeek);
            float totalAttendance = ZeroAverage;
            int countedDays = NoDaysCounted;
            int index = __instance.Stats.Count - LastIndexOffset;
            while (index >= __instance.Stats.Count - daysToCheck)
            {
                // Ignore day-off entries so the average reflects performance days.
                if (__instance.Stats[index].Schedule.Type != Theaters._theater._schedule._type.day_off)
                {
                    totalAttendance += __instance.Stats[index].Attendance;
                    countedDays++;
                }
                index--;
            }
            // Only divide if at least one valid day was counted.
            if (countedDays != NoDaysCounted)
            {
                totalAttendance /= countedDays;
            }
            // Return a rounded attendance average.
            __result = Mathf.RoundToInt(totalAttendance);
        }

    }

	// Fixed Theater so that average stats ignore days off
    [HarmonyPatch(typeof(Theaters._theater), "GetAvgRevenue")]
    public class Theaters__theater_GetAvgRevenue
    {
        // Rolling window size for averages.
        private const int DaysInWeek = 7;
        private const int NoStats = 0;
        private const int NoDaysCounted = 0;
        private const int LastIndexOffset = 1;
        private const float ZeroAverage = 0f;

        public static void Postfix(ref int __result, Theaters._theater __instance)
        {
            // Skip if there are no stats to average.
            if (__instance.Stats.Count == NoStats)
                return;

            // Only inspect the most recent week (or fewer days if not enough data).
            int daysToCheck = Mathf.Min(__instance.Stats.Count, DaysInWeek);
            float totalRevenue = ZeroAverage;
            int countedDays = NoDaysCounted;
            int index = __instance.Stats.Count - LastIndexOffset;
            while (index >= __instance.Stats.Count - daysToCheck)
            {
                // Ignore day-off entries so the average reflects earning days.
                if (__instance.Stats[index].Schedule.Type != Theaters._theater._schedule._type.day_off)
                {
                    totalRevenue += __instance.Stats[index].Revenue;
                    countedDays++;
                }
                index--;
            }
            // Only divide if at least one valid day was counted.
            if (countedDays != NoDaysCounted)
            {
                totalRevenue /= countedDays;
            }
            // Return a rounded revenue average.
            __result = Mathf.RoundToInt(totalRevenue);
        }
    }


	// Fixed Theater so that money tooltip includes 7 days instead of 6, and include sub revenue
    [HarmonyPatch(typeof(Theaters), "GetLastWeekEarning")]
    public class Theaters_GetLastWeekEarning
    {
        // Tooltip is intended to show a full week.
        private const int DaysInWeek = 7;
        // Approximate weeks per month used by the base game for sub revenue.
        private const float SubRevenueWeeksPerMonth = 4.35f;

        public static void Postfix(ref long __result)
        {
            // Start with the base value from the game.
            long output = __result;
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                // Add the missing 7th day for each theater.
                if (theater.Stats.Count >= DaysInWeek)
                {
                    output += theater.Stats[theater.Stats.Count - DaysInWeek].Revenue;
                }
                if (theater.AreSubsUnlocked())
                {
                    // Include subscription revenue spread across an average month.
                    output += (long)Mathf.Round(theater.GetSubRevenue() / SubRevenueWeeksPerMonth);
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
            // Start with the base value from the game.
            int output = __result;
            foreach (Cafes._cafe cafe in Cafes.Cafes_)
            {
                // Add the missing 7th day for each cafe.
                if (cafe.Stats.Count >= DaysInWeek)
                {
                    output += cafe.Stats[cafe.Stats.Count - DaysInWeek].Profit;
                }
            }
            // Return the corrected tooltip total.
            __result = output;
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

        public static bool Prefix(ref float _val)
        {
            // Clamp ratios so hype does not exceed its intended 200% cap.
            if (_val > MaxRatio)
            {
                _val = MaxRatio;
            }
            return true;
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
        private const int FirstIndex = 0;
        // Constants for triangular number inversion.
        private const float TriangularScale = 8f;
        private const float TriangularOffset = 1f;
        private const float TriangularDivisor = 2f;
        private const float ZeroPercent = 0f;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Resolve the infix method that computes percent based on actual filled rows.
            MethodInfo infix = AccessTools.Method(typeof(singles__single_SenbatsuCalcParam), nameof(Infix));
            if (infix == null)
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("Infix method lookup failed.");
                return instructions;
            }

            var matcher = new CodeMatcher(instructions);
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

            // Replace 100f / num2 with Infix(_girls) to derive rows from actual filled slots.
            var labels = matcher.Instruction.labels.ToList();
            var blocks = matcher.Instruction.blocks.ToList();
            matcher.RemoveInstructions(DivPatternLength);
            // Ldarg_1 loads the _girls list from the original SenbatsuCalcParam signature.
            var loadGirls = new CodeInstruction(OpCodes.Ldarg_1);
            loadGirls.labels.AddRange(labels);
            loadGirls.blocks.AddRange(blocks);
            matcher.Insert(loadGirls, new CodeInstruction(OpCodes.Call, infix));
            return matcher.InstructionEnumeration();
        }

        public static float Infix(List<data_girls.girls> girls)
        {
            // Count only filled slots to determine how many rows are actually used.
            int idolCount = NoIdols;
            if (girls != null)
            {
                for (int i = FirstIndex; i < girls.Count; i++)
                {
                    if (girls[i] != null)
                    {
                        idolCount++;
                    }
                }
            }

            // Total rows in the senbatsu formation:
            // 1, 2, 3, 4, 5  (total capacity = 15)
            // Safety: if no idols, don't divide by zero.
            // (The game probably never passes 0, but this prevents Infinity/NaN.)
            if (idolCount <= NoIdols)
                return ZeroPercent;

            // Triangular number inversion:
            // Assume that r represents the minimum required number of rows to fit all our idols represented by n
            // Find the smallest r such that r(r+1)/2 >= idolCount
            //
            // r = ceil((sqrt(8N + 1) - 1) / 2)
            float n = idolCount;
            float r = (Mathf.Sqrt(TriangularScale * n + TriangularOffset) - TriangularOffset) / TriangularDivisor;

            // Round up to the next whole row to ensure all idols fit.
            int rowsUsed = Mathf.CeilToInt(r);

            // Clamp to the real formation size:
            // Anything above 15 idols still just uses all 5 rows.
            rowsUsed = Mathf.Clamp(rowsUsed, MinRows, MaxRows);

            // The game wants a "percentage per used row" kind of factor.
            return PercentScale / rowsUsed;
        }

    }

    // Fix senbatsu parameter queries to use the requested param type.
    [HarmonyPatch(typeof(singles._single), "GetSenbatsuParamValue")]
    public class singles__single_GetSenbatsuParamValue
    {
        // Cache the private calculator so we can call it with the correct param type.
        private static readonly MethodInfo SenbatsuCalcParam = AccessTools.Method(
            typeof(singles._single),
            "SenbatsuCalcParam",
            new Type[] { typeof(List<data_girls.girls>), typeof(data_girls._paramType), typeof(Groups._group) });

        public static bool Prefix(singles._single __instance, data_girls._paramType Type, ref float __result)
        {
            // Fall back to vanilla behavior if reflection fails.
            if (SenbatsuCalcParam == null)
                return true;

            try
            {
                // Compute the value using the requested param type (instead of always "cute").
                var param = (data_girls.girls.param)SenbatsuCalcParam.Invoke(__instance, new object[] { __instance.girls, Type, null });
                __result = param.val;
                return false;
            }
            catch (Exception ex)
            {
                // Log once so repeated failures do not spam the log.
                PatchLog.WarnOncePerPatch<singles__single_GetSenbatsuParamValue>("failed: " + ex);
                return true;
            }
        }
    }


    // Dating status is visible for underage members.
    // A postfix is safer than a transpiler here and avoids invalid IL after upstream changes.
    [HarmonyPatch(typeof(data_girls.girls), "GetPartnerString")]
    public class data_girls_girls_GetPartnerString
    {
        public static void Postfix(data_girls.girls __instance, ref string __result)
        {
            if (__instance == null)
            {
                return;
            }

            // Keep vanilla behavior for AOC members.
            if (__instance.Is_AOC())
            {
                return;
            }

            __result = BuildPartnerString(__instance);
        }

        private static string BuildPartnerString(data_girls.girls girl)
        {
            string text = "";
            if (!girl.DatingData.Is_Partner_Status_Known)
            {
                text += Language.Data["PROFILE__DATING_UNKNOWN"];
            }
            else if (girl.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.free)
            {
                text += Language.Data["PROFILE__DATING_NOT_DATING"];
            }
            else if (girl.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_idol)
            {
                data_girls.girls girlfriend = girl.GetGirlfriend();
                if (girlfriend != null)
                {
                    text += Language.Insert("PROFILE__DATING_IDOL", new string[]
                    {
                        girlfriend.GetName(true)
                    });
                }
                else
                {
                    text += Language.Data["PROFILE__DATING_IDOL_UNKNOWN"];
                }
            }
            else if (girl.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_outside_bf)
            {
                text += Language.Data["PROFILE__DATING_HAS_BF"];
            }
            else if (girl.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_outside_gf)
            {
                text += Language.Data["PROFILE__DATING_HAS_GF"];
            }
            else if (girl.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_player)
            {
                text += Language.Data["PROFILE__DATING_YOU"];
            }

            text += "\n";
            if (girl.DatingData.Is_Sexuality_Known)
            {
                if (girl.sexuality == data_girls.girls._sexuality.straight)
                {
                    text += Language.Data["PROFILE__DATING_STRAIGHT"];
                }
                else if (girl.sexuality == data_girls.girls._sexuality.lesbian)
                {
                    text += Language.Data["PROFILE__DATING_LESBIAN"];
                }
                else
                {
                    text += Language.Data["PROFILE__DATING_BI"];
                }
            }
            else
            {
                text += Language.Data["PROFILE__DATING_PREF_UNKNOWN"];
            }

            if (girl.DatingData.Previous_Attempt != Date_Flirt._flirt._category.NONE
                && girl.DatingData.Partner_Status_Known_To_Player != data_girls.girls._dating_data._partner_status.taken_player)
            {
                text += "\n";
                if (girl.DatingData.Is_Uninterested || (girl.DatingData.Is_Sexuality_Known && !Date_Flirt.IsCompatibleSexuality(girl)))
                {
                    text += Language.Data["PROFILE__DATING_NOT_INTERESTED"];
                }
                else
                {
                    text += Language.Data["PROFILE__DATING_INTERESTED"];
                }
            }

            return text;
        }
    }


    // Fixed fan opinion to be impacted by concerts, SSK/show cancellation and random events
    [HarmonyPatch(typeof(resources._fanOpinion), "Add")]
    public class resources__fanOpinion_Add
    {
        public static void Postfix(resources._fanOpinion __instance, float val)
        {
            // Propagate global fan opinion changes to each active girl.
            foreach (data_girls.girls girl in data_girls.girl)
            {
                // Skip null entries, sick girls, and graduates who should not gain appeal.
                if (girl != null && !girl.IsSick() && girl.status != data_girls._status.graduated)
                {
                    // Apply the appeal delta for the matching fan type.
                    girl.AddAppeal(__instance.type, val);
                }
            }
        }
    }


    // Fixed gossip so girl doesn't gossip about herself
    [HarmonyPatch(typeof(Date_Gossip), "GetAvailableGossips")]
    public class Date_Gossip_GetAvailableGossips
    {
        // Sentinel values for list bounds.
        private const int NoGossips = 0;
        private const int LastIndexOffset = 1;

        public static void Postfix(ref List<Date_Gossip._gossip> __result, data_girls.girls Snitch)
        {
            // Nothing to filter if the list is empty.
            if (__result.Count == NoGossips)
            {
                return;
            }
            // Walk backwards so removals do not affect remaining indices.
            for (int i = __result.Count - LastIndexOffset; i >= NoGossips; i--)
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
        // Enum values from Relationships_Player._type used in the original IL.
        private const int RelationshipFriendshipValue = 1;
        private const int RelationshipInfluenceValue = 2;
        // Sentinel for FindIndex failures.
        private const int NotFoundIndex = -1;
        // Offset to move from a marker instruction to the next instruction.
        private const int NextInstructionOffset = 1;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Resolve the relationship checker to ensure we update the correct call site.
            MethodInfo checkRelationship = AccessTools.Method(
                typeof(vn_requirements),
                "CheckRelationship",
                new Type[] { typeof(data_girls.girls), typeof(string), typeof(Relationships_Player._type) });
            if (checkRelationship == null)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("CheckRelationship method lookup failed.");
                return instructions;
            }

            List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);
            // Find the "influence" branch marker in the IL.
            int influenceIndex = instructionList.FindIndex(ci =>
                ci.opcode == OpCodes.Ldstr && ci.operand is string text && text == InfluenceParameter);

            if (influenceIndex == NotFoundIndex)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("\"influence\" marker not found.");
                return instructionList.AsEnumerable();
            }

            // Locate the call to CheckRelationship that follows the influence branch.
            int callIndex = NotFoundIndex;
            for (int i = influenceIndex + NextInstructionOffset; i < instructionList.Count; i++)
            {
                if (IlHelpers.IsCallTo(instructionList[i], checkRelationship))
                {
                    callIndex = i;
                    break;
                }
            }

            if (callIndex == NotFoundIndex)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("CheckRelationship call not found after \"influence\" marker.");
                return instructionList.AsEnumerable();
            }

            // The relationship enum should be the last integer pushed before the call.
            int enumIndex = callIndex - NextInstructionOffset;
            if (enumIndex <= influenceIndex)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("enum constant not found before CheckRelationship call.");
                return instructionList.AsEnumerable();
            }

            CodeInstruction enumInstruction = instructionList[enumIndex];
            if (IlHelpers.IsLdcI4(enumInstruction, RelationshipFriendshipValue))
            {
                // Replace Friendship with Influence.
                enumInstruction.opcode = OpCodes.Ldc_I4_2;
                // Clear the operand to match the ldc.i4.2 opcode form.
                enumInstruction.operand = null;
                instructionList[enumIndex] = enumInstruction;
                return instructionList.AsEnumerable();
            }

            if (IlHelpers.IsLdcI4(enumInstruction, RelationshipInfluenceValue))
            {
                // Already patched or game fixed it upstream.
                return instructionList.AsEnumerable();
            }

            PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("unexpected enum opcode before CheckRelationship call.");
            return instructionList.AsEnumerable();
        }
    }

    // Fix "variable" requirements to respect leading negation.
    [HarmonyPatch(typeof(vn_requirements), "CheckGirl", new Type[] { typeof(data_girls.girls), typeof(string), typeof(string) })]
    public class vn_requirements_CheckGirl_Variable
    {
        // Prefix used by dialogue scripts to negate variable requirements.
        private const char NegationPrefix = '!';
        private const int PrefixIndex = 0;
        private const int NegationPrefixLength = 1;

        public static bool Prefix(data_girls.girls girl, string parameter, string formula, ref bool __result)
        {
            // Only override the "variable" branch; let the rest of CheckGirl run normally.
            if (parameter != "variable")
                return true;

            // Preserve base behavior for graduated girls.
            if (girl.status == data_girls._status.graduated)
            {
                __result = false;
                return false;
            }

            bool negate = false;
            if (!string.IsNullOrEmpty(formula) && formula[PrefixIndex] == NegationPrefix)
            {
                negate = true;
                formula = formula.Substring(NegationPrefixLength);
            }

            // Evaluate the variable and apply negation if requested.
            bool hasVariable = girl.IsVariable(formula);
            __result = negate ? !hasVariable : hasVariable;
            return false;
        }
    }


    // Fix stamina cost of performance thumbnail if Energetic policy
    [HarmonyPatch(typeof(Activities._activity), "GetDescription")]
    public class Activities__activity_GetDescription
    {
        // Energetic policy overrides the displayed stamina cost.
        private const int EnergeticStaminaCost = 4;
        private const string PointsKey = "PT";
        private const string StaminaKey = "STAMINA";
        private const string CostPrefix = "-";
        private const string CostSeparator = " ";

        public static void Postfix(ref Activities._activity __instance, ref string __result)
        {
            // Only adjust performance activities when Energetic policy is active.
            if (__instance.type == Activity._type.performance && policies.GetSelectedPolicyValue(policies._type.performances).Value == policies._value.performances_energy)
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
    // Preserve the previous in-memory SaveManager.Data before LoadData runs; if LoadData exits with null Data,
    // restore the previous data (or a fresh SavedData fallback) so autosave and SaveEvent subscribers remain safe.
    [HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] { typeof(bool) })]
    public class SaveManager_LoadData_Bool_NullGuard
    {
        private const string WarningMessage =
            "LoadData(bool) left SaveManager.Data null. Restored previous in-memory save data to prevent SaveEvent/autosave null crash.";

        public static void Prefix(SaveManager __instance, ref SaveManager.SavedData __state)
        {
            __state = __instance != null ? __instance.Data : null;
        }

        public static void Postfix(SaveManager __instance, SaveManager.SavedData __state)
        {
            if (__instance == null || __instance.Data != null)
                return;

            __instance.Data = __state ?? new SaveManager.SavedData();
            PatchLog.WarnOncePerPatch<SaveManager_LoadData_Bool_NullGuard>(WarningMessage);
        }
    }

    // Same null-guard for the string-path overload used by some load flows and mods.
    [HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] { typeof(string) })]
    public class SaveManager_LoadData_Path_NullGuard
    {
        private const string WarningMessage =
            "LoadData(string) left SaveManager.Data null. Restored previous in-memory save data to prevent SaveEvent/autosave null crash.";

        public static void Prefix(SaveManager __instance, ref SaveManager.SavedData __state)
        {
            __state = __instance != null ? __instance.Data : null;
        }

        public static void Postfix(SaveManager __instance, SaveManager.SavedData __state)
        {
            if (__instance == null || __instance.Data != null)
                return;

            __instance.Data = __state ?? new SaveManager.SavedData();
            PatchLog.WarnOncePerPatch<SaveManager_LoadData_Path_NullGuard>(WarningMessage);
        }
    }

}
