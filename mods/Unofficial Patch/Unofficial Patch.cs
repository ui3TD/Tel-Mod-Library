using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.U2D;
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

        // Set by the tests, so a patch that can't be applied fails the test instead of being logged.
        internal static bool RethrowPatchFailures = false;

        // Harmony's Cleanup for every patch class. A patch that can't be applied (e.g. another mod's transpiler
        // changed the method first) is logged and skipped, so the mod's other fixes still apply.
        public static Exception PatchFailed<TPatch>(Exception ex)
        {
            if (ex == null || RethrowPatchFailures)
                return ex;
            Error<TPatch>("couldn't be applied, so this fix is off: " + (ex.InnerException?.Message ?? ex.Message));
            return null;
        }

        // Warns, once per patch, that a result another mod had already changed was left as it was.
        public static void WarnResultChanged<TPatch>()
        {
            WarnOncePerPatch<TPatch>("the game's result was already changed by another mod; left unchanged.");
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
    [HarmonyPatch(typeof(Profile_Fans_Pies), nameof(Profile_Fans_Pies.Render_Pies))]
    public class Profile_Fans_Pies_Render_Pies
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Profile_Fans_Pies_Render_Pies>(ex);

        // No-fan sentinel for early exit.
        private const long NoFans = 0L;
        // Cap for cumulative pie fills.
        private const float MaxPieFill = 1f;

        // Fixes age-pie rendering so the stacked slices match the teen/YA/adult ratios.
        // Last, so the pie ends as UP draws it whatever other mods' patches drew.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Profile_Fans_Pies __instance, data_girls.girls ___Girl)
        {
            // Skip when there are no fans to render, avoiding divide-by-zero ratios in vanilla code.
            if (___Girl.GetFans_Total() == NoFans)
                return;

            // The ratios the game draws from, not the fills it left, so the result is the same whatever
            // drew the pie before.
            float teenRatio = Math.Max(0f, ___Girl.GetFan_Ratio(resources.fanType.teen));
            float yaRatio = Math.Max(0f, ___Girl.GetFan_Ratio(resources.fanType.youngAdult));
            float adultRatio = Math.Max(0f, ___Girl.GetFan_Ratio(resources.fanType.adult));
            SetFills(__instance, teenRatio, yaRatio, adultRatio);
        }

        // Sets the cumulative fills for these slice ratios.
        public static void SetFills(Profile_Fans_Pies pies, float teenRatio, float yaRatio, float adultRatio)
        {
            Image teenImage = pies.Fans_Pie_Teen.GetComponent<Image>();
            Image yaImage = pies.Fans_Pie_YA.GetComponent<Image>();
            Image adultImage = pies.Fans_Pie_Adult.GetComponent<Image>();

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
            yaImage.fillAmount = yaRatio;
            adultImage.fillAmount = adultRatio + yaRatio;
            teenImage.fillAmount = adultRatio + yaRatio + teenRatio;
        }
    }

    // Keeps tour expected revenue text color aligned with profitability.
    [HarmonyPatch(typeof(Tour_New_Popup), nameof(Tour_New_Popup.Render))]
    public class Tour_New_Popup_Render
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Tour_New_Popup_Render>(ex);

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

    // Fixes the show and concert editors' idol pickers letting you pick an idol who is already in the cast, which put
    // her in two slots. The picker greys out cast idols when it opens, but its list is still being built for a
    // moment after the editor opens, and every idol button enables itself when it is drawn. Keep cast idols greyed
    // out whenever a picker button is drawn. It only greys buttons, so other greying (injured idols) is kept.
    [HarmonyPatch(typeof(GirlButtonSmall), nameof(GirlButtonSmall.UpdateData))]
    public class GirlButtonSmall_UpdateData
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<GirlButtonSmall_UpdateData>(ex);

        // The faded look the concert picker gives cast idols (Concert_New_Popup.UpdateGirlsList).
        private const float ConcertGreyAlpha = 0.4f;

        // The casts the editors grey out: the show's slots, and the concert setlist item being cast.
        private static readonly AccessTools.FieldRef<Show_Popup, data_girls.girls[]> ShowGirls =
            AccessTools.FieldRefAccess<Show_Popup, data_girls.girls[]>("girls");
        private static readonly AccessTools.FieldRef<Concert_New_Popup, SEvent_Concerts._concert.ISetlistItem> ConcertItem =
            AccessTools.FieldRefAccess<Concert_New_Popup, SEvent_Concerts._concert.ISetlistItem>("girl_setListItem");

        // Applies the editor's own greying rule to the button the game just drew.
        public static void Postfix(GirlButtonSmall __instance)
        {
            try
            {
                ButtonDefault button = __instance.buttonDefault;
                if (__instance.girl == null || button == null || !button.active || !__instance.gameObject.activeInHierarchy)
                    return;
                Transform grid = __instance.transform.parent;
                if (grid == null)
                    return;

                Show_Popup show = grid.GetComponentInParent<Show_Popup>();
                if (show != null && show.Girl_Select_Grid != null && grid == show.Girl_Select_Grid.transform)
                {
                    if (InCast(__instance.girl, ShowCast(show)))
                        button.Activate(val: false);
                    return;
                }

                Concert_New_Popup concert = grid.GetComponentInParent<Concert_New_Popup>();
                if (concert != null && concert.Girl_Select_Grid != null && grid == concert.Girl_Select_Grid.transform
                    && InCast(__instance.girl, ConcertCast(concert)))
                {
                    button.Activate(val: false);
                    CanvasGroup group = __instance.GetComponent<CanvasGroup>();
                    if (group != null)
                        group.alpha = ConcertGreyAlpha;
                }
            }
            catch (Exception ex)
            {
                PatchLog.WarnOncePerPatch<GirlButtonSmall_UpdateData>("failed: " + ex);
            }
        }

        public static IEnumerable<data_girls.girls> ShowCast(Show_Popup popup) => ShowGirls(popup);

        // Null until the concert picker is first opened.
        public static IEnumerable<data_girls.girls> ConcertCast(Concert_New_Popup popup) => ConcertItem(popup)?.GetGirls();

        public static bool InCast(data_girls.girls girl, IEnumerable<data_girls.girls> cast) => girl != null && cast != null && cast.Contains(girl);
    }

    // Fixes tour savings with the Most Prolific Group award or nomination, or the cheaper events choice. The game
    // cut the production cost but took the regional saving off at full size, so extra stops in a discounted region
    // could cost nothing or less. Cut the saving by the same share, so the discounts stack.
    [HarmonyPatch(typeof(SEvent_Tour.tour), nameof(SEvent_Tour.tour.RecalcProductionCost))]
    public class SEvent_Tour_tour_RecalcProductionCost
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Tour_tour_RecalcProductionCost>(ex);

        // Last, so the share includes any other change to the production cost.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SEvent_Tour.tour __instance)
        {
            if (__instance.Saving <= 0)
                return;

            // The production cost before any cut.
            long fullCost = 0L;
            foreach (SEvent_Tour.tour.selectedCountry selected in __instance.SelectedCountries)
            {
                fullCost += selected.Country.GetCostByLevel(selected.Level);
            }
            if (fullCost <= 0L || __instance.ProductionCost == fullCost)
                return;

            // A saving that isn't the full-size one was already cut by something else.
            if (__instance.Saving != __instance.GetSaving())
                return;

            __instance.Saving = Mathf.RoundToInt((float)((double)__instance.Saving * __instance.ProductionCost / fullCost));
        }
    }

    // Fixes the tour attendance fame penalty. The game compared the agency's fame points with twice the venue
    // level, so the penalty never applied; compare the fame level, as the tour description says.
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.GetFamePenalty))]
    public class SEvent_Tour_GetFamePenalty
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Tour_GetFamePenalty>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo get = AccessTools.Method(typeof(resources), nameof(resources.Get), new[] { typeof(resources.type), typeof(bool) });
            List<CodeInstruction> codes = instructions.ToList();

            List<int> reads = Enumerable.Range(0, codes.Count).Where(i => IlHelpers.IsCallTo(codes[i], get)).ToList();
            if (reads.Count == 0)
            {
                // Already reads the fame level: patched twice, or the game fixed it.
                return codes;
            }
            if (reads.Count > 1 || reads[0] < 2 || !IlHelpers.IsLdcI4(codes[reads[0] - 2], (int)resources.type.fame))
            {
                PatchLog.WarnOncePerPatch<SEvent_Tour_GetFamePenalty>("expected one read of the fame points; left unchanged.");
                return instructions;
            }

            // Same arguments and return type, so the code around the call stays as it is.
            codes[reads[0]].opcode = OpCodes.Call;
            codes[reads[0]].operand = AccessTools.Method(typeof(SEvent_Tour_GetFamePenalty), nameof(FameLevel));
            return codes;
        }

        // The fame level, in place of resources.Get(type, useTotal) for fame.
        public static long FameLevel(resources.type type, bool useTotal)
        {
            return resources.FamePointsToLevel(resources.Get(type, useTotal));
        }
    }

    // Restores stamina costs for theater schedules. The game works out the cost (5 for a performance, 2 for
    // manzai, doubled on hard), then returns 0 instead of it; return the cost it worked out.
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

            // Load the cost instead, keeping any jump into this instruction.
            CodeInstruction loadCost = IlHelpers.LoadOfStore(storeCost);
            matcher.Instruction.opcode = loadCost.opcode;
            matcher.Instruction.operand = loadCost.operand;
            return matcher.InstructionEnumeration();
        }
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

	// Fixed Cafe so that money tooltip includes 7 days instead of 6
    [HarmonyPatch(typeof(Cafes), nameof(Cafes.GetLastWeekEarning))]
    public class Cafes_GetLastWeekEarning
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Cafes_GetLastWeekEarning>(ex);

        // Tooltip is intended to show a full week.
        private const int DaysInWeek = 7;

        // Last, so it sees what other mods' patches did.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            // A result that isn't the game's was already changed by another mod.
            if (__result != Vanilla())
            {
                PatchLog.WarnResultChanged<Cafes_GetLastWeekEarning>();
                return;
            }

            // Recount the week in 64 bits: the game's int total wraps past ~2.1 billion.
            long output = 0L;
            foreach (Cafes._cafe cafe in Cafes.Cafes_)
            {
                if (cafe == null || cafe.Stats == null)
                    continue;

                // The last 7 days of each cafe; the game counts only 6.
                for (int i = Math.Max(cafe.Stats.Count - DaysInWeek, 0); i < cafe.Stats.Count; i++)
                {
                    output += cafe.Stats[i].Profit;
                }
            }
            // The tooltip reads an int, so cap the total instead of wrapping.
            __result = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, output));
        }

        // The game's own GetLastWeekEarning, without any mod's patches.
        [HarmonyReversePatch]
        public static int Vanilla()
        {
            throw new NotImplementedException("Replaced by a reverse patch");
        }
    }

    // Fixes the novelty of an idol's new cafe dish. The game added the novelty her dishes had lost instead of
    // taking it away, so every new dish started at 100%; take it away, so a new dish starts lower when her other
    // dishes are stale.
    [HarmonyPatch(typeof(Cafes._cafe), nameof(Cafes._cafe.GetNovelty))]
    public class Cafes__cafe_GetNovelty
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Cafes__cafe_GetNovelty>(ex);

        // Full novelty of one dish.
        private const int FullNovelty = 100;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions);

            // (float)(dishes * 100) - total novelty
            matcher.MatchStartForward(
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, FullNovelty)),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(OpCodes.Conv_R4),
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(OpCodes.Sub));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<Cafes__cafe_GetNovelty>("lost novelty not found.");
                return instructions;
            }

            matcher.Advance(6);
            if (matcher.Instruction.opcode == OpCodes.Neg)
            {
                // Already negated: patched twice, or the game fixed it.
                return matcher.InstructionEnumeration();
            }

            // Negate the lost novelty before the formula adds 100.
            matcher.Insert(new CodeInstruction(OpCodes.Neg));
            return matcher.InstructionEnumeration();
        }
    }

    // Cafe workers get the "practice" status when a shift starts and "normal" when it ends. A depressed idol can work
    // (the game's rule is only that depressed idols can't practice and their stats are halved), but either change
    // replaced her depressed status, so one shift cured her depression. Leave a depressed idol's status alone.
    public static class CafeShiftStatus
    {
        private static readonly MethodInfo SetStatus = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.SetStatus));
        private static readonly MethodInfo SetUnlessDepressedMethod = AccessTools.Method(typeof(CafeShiftStatus), nameof(SetUnlessDepressed));

        public static void SetUnlessDepressed(data_girls.girls girl, data_girls._status status)
        {
            if (girl.status != data_girls._status.depressed)
                girl.SetStatus(status);
        }

        // Replaces each "girl.SetStatus(status)" with SetUnlessDepressed(girl, status), which takes the same stack.
        public static IEnumerable<CodeInstruction> Wrap<TPatch>(IEnumerable<CodeInstruction> instructions, data_girls._status status)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int wrapped = 0;
            for (int i = 1; i < codes.Count; i++)
            {
                if (IlHelpers.IsLdcI4(codes[i - 1], (int)status) && IlHelpers.IsCallTo(codes[i], SetStatus))
                {
                    codes[i] = new CodeInstruction(OpCodes.Call, SetUnlessDepressedMethod).MoveLabelsFrom(codes[i]).MoveBlocksFrom(codes[i]);
                    wrapped++;
                }
            }
            // Already wrapped: patched twice
            if (wrapped == 0 && !codes.Any(ci => IlHelpers.IsCallTo(ci, SetUnlessDepressedMethod)))
                PatchLog.WarnOncePerPatch<TPatch>(status + " status change not found.");
            return codes;
        }
    }

    // Shift start: a depressed idol keeps her status while she works.
    [HarmonyPatch(typeof(Cafes), nameof(Cafes.RenderCafe))]
    public class Cafes_RenderCafe
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Cafes_RenderCafe>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => CafeShiftStatus.Wrap<Cafes_RenderCafe>(instructions, data_girls._status.practice);
    }

    // Shift end: a depressed idol isn't set back to normal.
    [HarmonyPatch(typeof(Cafes._cafe), nameof(Cafes._cafe.StopWorking))]
    public class Cafes__cafe_StopWorking
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Cafes__cafe_StopWorking>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => CafeShiftStatus.Wrap<Cafes__cafe_StopWorking>(instructions, data_girls._status.normal);
    }


    // Fixes clinics taking an idol who is already working in another room: canAssign returns true for clinics
    // before the busy check other rooms make. A room link alone doesn't mean she is busy (the game leaves it set
    // after some jobs end), so only a room that still holds her counts.
    [HarmonyPatch(typeof(agency._room), nameof(agency._room.canAssign), new[] { typeof(data_girls.girls), typeof(data_girls._paramType?) })]
    public class agency__room_canAssign
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<agency__room_canAssign>(ex);

        public static void Postfix(agency._room __instance, data_girls.girls _girl, ref bool __result)
        {
            if (!__result || __instance.type != agency._type.doctorsOffice || _girl == null)
                return;
            if (_girl.status == data_girls._status.practice || (_girl.room != __instance && Holds(_girl.room, _girl)))
                __result = false;
        }

        // The room is working with her: its own idol, or a cafe shift she is still on (a sick idol isn't working).
        public static bool Holds(agency._room room, data_girls.girls girl)
        {
            if (room == null)
                return false;
            if (room.girl == girl)
                return true;
            return room.type == agency._type.cafeAndShop && !girl.IsSick() && room.GetCafe()?.WorkingGirls.Contains(girl) == true;
        }
    }

    // Fixes idols stuck in a Break Room scene after the room is demolished: CancelJob sets the room back to normal
    // without FinishScene, so its idols keep the scene status with no scene behind it, and no automatic room or new
    // scene takes them. End the scene first with the game's FinishScene, which resets only idols still in it.
    // Runs after other prefixes so they still see the scene. Idempotent: a finished scene leaves the room normal.
    [HarmonyPatch(typeof(agency._room), nameof(agency._room.CancelJob))]
    public class agency__room_CancelJob
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<agency__room_CancelJob>(ex);

        // The game's FinishScene draws sprites; tests replace it
        public static Action<agency._room> FinishScene = room => room.FinishScene();

        [HarmonyPriority(Priority.Last)]
        public static void Prefix(agency._room __instance, bool __runOriginal)
        {
            if (__runOriginal && __instance.status == agency._room._status.scene && __instance.sceneGirls != null)
                FinishScene(__instance);
        }
    }

    // Fixes Break Room scenes starting with idols who are no longer free. OnNewDay picks free idols and a free room,
    // and AssignSceneWithDelay starts the scene up to 19 hours later without checking either again. An idol injured,
    // depressed, sent on hiatus, graduated or put in training meanwhile was put in the scene (and set back to normal
    // when it ended, curing her), and a scene whose room was demolished, or that was picked before a save was
    // loaded, stranded its idols. Start the scene only if it can still happen; otherwise the next day picks new ones.
    // Pattern-based, so it does nothing if another mod has replaced the call.
    [HarmonyPatch]
    public class Scenes_AssignSceneWithDelay
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Scenes_AssignSceneWithDelay>(ex);

        private static readonly MethodInfo Assign = AccessTools.Method(typeof(agency._room), nameof(agency._room.assign),
            new[] { typeof(Scenes.type), typeof(List<data_girls.girls>) });
        private static readonly MethodInfo StartMethod = AccessTools.Method(typeof(Scenes_AssignSceneWithDelay), nameof(StartDelayedScene));
        private static readonly FieldInfo CurrentAgency = AccessTools.Field(typeof(agency), nameof(agency._this));

        public static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(Scenes), nameof(Scenes.AssignSceneWithDelay)));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int replaced = 0;
            foreach (CodeInstruction code in codes)
            {
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && ReferenceEquals(code.operand, Assign))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = StartMethod;
                    replaced++;
                }
            }
            if (replaced == 0)
                PatchLog.WarnOncePerPatch<Scenes_AssignSceneWithDelay>("no scene start left to check (another mod may have changed it).");
            return codes;
        }

        public static void StartDelayedScene(agency._room room, Scenes.type type, List<data_girls.girls> girls)
        {
            if (CanStart(room, girls))
                room.assign(type, girls);
        }

        // The room is still one of the agency's and free, and every idol is still free by the test OnNewDay chose
        // them with. Rooms and idols from before a load aren't in the current lists.
        public static bool CanStart(agency._room room, List<data_girls.girls> girls)
        {
            if (room == null || girls == null || room.status != agency._room._status.normal)
                return false;
            if (CurrentAgency.GetValue(null) is agency owner && !owner.allRooms().Contains(room))
                return false;
            List<data_girls.girls> free = data_girls.GetFreeGirls(includeSceneGirls: false);
            return girls.All(free.Contains);
        }
    }

    // Repairs idols already stuck in the scene status (from saves made before these fixes, or anything else that ends
    // a scene room's work without FinishScene): sets back to normal every idol in the scene status that no room's
    // scene holds, before the day's scenes are picked. Idempotent: once nobody is stuck, it changes nothing.
    [HarmonyPatch(typeof(Scenes), nameof(Scenes.OnNewDay))]
    public class Scenes_OnNewDay
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Scenes_OnNewDay>(ex);

        // Leaving the scene status makes the game look for the idol's scene through Camera.main; tests replace it
        public static Action<data_girls.girls> SetNormal = girl => girl.SetStatus(data_girls._status.normal);

        public static void Prefix(agency ___Agency) => Repair(___Agency);

        public static void Repair(agency owner)
        {
            if (owner is null || data_girls.girl == null)
                return;
            List<agency._room> scenes = owner.allRooms().Where(r => r.status == agency._room._status.scene && r.sceneGirls != null).ToList();
            foreach (data_girls.girls girl in data_girls.girl.ToList())
            {
                if (girl != null && girl.status == data_girls._status.scene && !scenes.Any(r => r.sceneGirls.Contains(girl)))
                    SetNormal(girl);
            }
        }
    }

	// Fixed so that when girls dating within the group break up, their relationship status is no longer known
    [HarmonyPatch(typeof(Relationships._relationship), nameof(Relationships._relationship.BreakUp))]
    public class Relationships__relationship_BreakUp
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Relationships__relationship_BreakUp>(ex);

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
            // Only clear known status when the pair was dating and has now broken up (another mod may have
            // skipped the breakup).
            if (!__state || __instance == null || __instance.Dating || __instance.Girls == null || __instance.Girls.Count < 2)
                return;

            // Hide partner status for both sides after breakup.
            if (__instance.Girls[FirstPartnerIndex]?.DatingData != null)
            {
                __instance.Girls[FirstPartnerIndex].DatingData.Is_Partner_Status_Known = false;
            }
            if (__instance.Girls[SecondPartnerIndex]?.DatingData != null)
            {
                __instance.Girls[SecondPartnerIndex].DatingData.Is_Partner_Status_Known = false;
            }
        }
    }

        // Fixed Concert revenue formula so that it shows accurate estimated values
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetRevenue))]
    public class SEvent_Concerts__concert__projectedValues_GetRevenue
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Concerts__concert__projectedValues_GetRevenue>(ex);

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
            MethodInfo getHype = AccessTools.Method(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetHype));
            MethodInfo infix   = AccessTools.Method(typeof(SEvent_Concerts__concert__projectedValues_GetRevenue), nameof(Infix));

            // Abort if Harmony lookup fails so we don't corrupt IL.
            if (getHype == null || infix == null)
            {
                PatchLog.WarnOncePerPatch<SEvent_Concerts__concert__projectedValues_GetRevenue>("GetHype/Infix method lookup failed.");
                return instructions;
            }

            // Read once: another mod's transpiler may hand over a sequence that rebuilds on every read.
            instructions = instructions.ToList();
            if (instructions.Any(ci => IlHelpers.IsCallTo(ci, infix)))
            {
                // Already calls the Infix: patched twice.
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
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), nameof(SEvent_Concerts._concert._projectedValues.GetString))]
    public class SEvent_Concerts__concert__projectedValues_GetString
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Concerts__concert__projectedValues_GetString>(ex);

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

    // Fixes concerts in production losing their place in the Special Events list after loading: loading runs Initiate
    // on every concert, which sets FinishDate to today for unfinished ones, and the list sorts by that date. Keep the
    // loaded date. Idempotent: it only puts back the date the concert had before Initiate.
    [HarmonyPatch(typeof(SEvent_Concerts), nameof(SEvent_Concerts.LoadFunction))]
    public class SEvent_Concerts_LoadFunction
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Concerts_LoadFunction>(ex);

        internal static bool Loading;

        public static void Prefix() => Loading = true;

        public static void Finalizer() => Loading = false;
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Initiate))]
    public class SEvent_Concerts__concert_Initiate
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Concerts__concert_Initiate>(ex);

        public static void Prefix(SEvent_Concerts._concert __instance, out DateTime __state)
        {
            __state = __instance.FinishDate;
        }

        public static void Postfix(SEvent_Concerts._concert __instance, DateTime __state)
        {
            if (SEvent_Concerts_LoadFunction.Loading && __state != default && __instance.Status != SEvent_Tour.tour._status.finished)
                __instance.FinishDate = __state;
        }
    }

    // Fixes Cancel in the concert editor keeping every change: the editor edits the real concert. Remember what the
    // editor can change when it opens an existing concert, and put it back when the editor is closed with Cancel.
    // Idempotent: restoring the same snapshot again gives the same concert.
    [HarmonyPatch(typeof(Concert_New_Popup), nameof(Concert_New_Popup.Reset))]
    public class Concert_New_Popup_Reset
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Concert_New_Popup_Reset>(ex);

        internal static ConcertSnapshot Snapshot;

        public static void Prefix(SEvent_Concerts._concert _Concert)
        {
            Snapshot = _Concert != null ? new ConcertSnapshot(_Concert) : null;
        }
    }

    [HarmonyPatch(typeof(Concert_New_Popup), nameof(Concert_New_Popup.OnCancel))]
    public class Concert_New_Popup_OnCancel
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Concert_New_Popup_OnCancel>(ex);

        public static void Prefix(Concert_New_Popup __instance, bool ___showing_PanelGirls, bool ___showing_PanelSingle, bool ___IsEditing)
        {
            // Cancel inside the idol or single picker only closes the picker
            if (___showing_PanelGirls || ___showing_PanelSingle || !___IsEditing)
                return;
            ConcertSnapshot snapshot = Concert_New_Popup_Reset.Snapshot;
            if (snapshot != null && snapshot.Concert == __instance.Concert)
                snapshot.Restore();
            Concert_New_Popup_Reset.Snapshot = null;
        }
    }

    // What the concert editor can change on an existing concert.
    public class ConcertSnapshot
    {
        public readonly SEvent_Concerts._concert Concert;
        private readonly List<SEvent_Concerts._concert.ISetlistItem> items;
        private readonly List<string> titles;
        private readonly List<List<data_girls.girls>> girls;
        private readonly SEvent_Concerts._venue venue;
        private readonly int ticketPrice;
        private readonly string title;
        private readonly bool castChanged;

        public ConcertSnapshot(SEvent_Concerts._concert concert)
        {
            Concert = concert;
            items = concert.SetListItems.ToList();
            titles = items.Select(i => i.GetTitle()).ToList();
            girls = items.Select(i => i.GetGirls().ToList()).ToList();
            venue = concert.Venue;
            ticketPrice = concert.ProjectedValues.TicketPrice;
            title = concert.Title;
            castChanged = concert.Cast_Changed;
        }

        public void Restore()
        {
            Concert.SetListItems.Clear();
            Concert.SetListItems.AddRange(items);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].isMC())
                    items[i].SetTitle(titles[i]);
                for (int slot = 0; slot < girls[i].Count; slot++)
                    items[i].SetGirl(girls[i][slot], slot);
            }
            Concert.Venue = venue;
            Concert.ProjectedValues.TicketPrice = ticketPrice;
            Concert.Title = title;
            Concert.Cast_Changed = castChanged;
            Concert.RecalcProjectedValues();
            Concert.RecalcFanAppeal();
        }
    }

    // Stores the result of DateTime.AddMonths/AddDays calls whose result the game throws away (DateTime is immutable).
    // Compatible without knowing other mods: it only rewrites the discarded-call pattern, so once any mod has fixed a
    // call site there is nothing left to match and this does nothing there.
    public static class DiscardedDate
    {
        private static readonly MethodInfo AddMonths = AccessTools.Method(typeof(DateTime), nameof(DateTime.AddMonths));
        private static readonly MethodInfo AddDays = AccessTools.Method(typeof(DateTime), nameof(DateTime.AddDays));
        private static readonly MethodInfo AddMonthsToMethod = AccessTools.Method(typeof(DiscardedDate), nameof(AddMonthsTo));
        private static readonly MethodInfo AddDaysToMethod = AccessTools.Method(typeof(DiscardedDate), nameof(AddDaysTo));

        public static void AddMonthsTo(ref DateTime date, int months) => date = date.AddMonths(months);

        public static void AddDaysTo(ref DateTime date, double days) => date = date.AddDays(days);

        // Rewrites `<address>; <argument>; call AddMonths/AddDays; pop` where the address load matches isDate.
        public static IEnumerable<CodeInstruction> Store(IEnumerable<CodeInstruction> instructions, Func<CodeInstruction, bool> isDate, Type patchType)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int stored = 0;
            for (int i = 2; i < codes.Count - 1; i++)
            {
                MethodInfo replacement = ReferenceEquals(codes[i].operand, AddMonths) ? AddMonthsToMethod
                    : ReferenceEquals(codes[i].operand, AddDays) ? AddDaysToMethod : null;
                if (replacement == null || codes[i].opcode != OpCodes.Call || codes[i + 1].opcode != OpCodes.Pop || !isDate(codes[i - 2]))
                    continue;

                codes[i].operand = replacement;
                codes[i + 1].opcode = OpCodes.Nop;
                stored++;
            }
            if (stored == 0)
                PatchLog.WarnOncePerPatch(patchType, "no discarded date change left to fix (another mod may have fixed it).");
            return codes;
        }
    }

    // Fixes the show tooltip's next episode date: GetNextEpisodeDate throws away its first AddDays(1), so on the
    // show's weekday the next episode shown is today rather than next week (also used for the cancellation date).
    // Pattern-based, so it does nothing once another mod has fixed the call.
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.GetNextEpisodeDate), new[] { typeof(DateTime) })]
    public class Shows__show_GetNextEpisodeDate
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Shows__show_GetNextEpisodeDate>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return DiscardedDate.Store(instructions, ci => ci.opcode == OpCodes.Ldarga || ci.opcode == OpCodes.Ldarga_S, typeof(Shows__show_GetNextEpisodeDate));
        }
    }

    // Fixes an idol taking two slots of the same show cast: when an idol leaves a permanent cast, the game refills
    // her slot with a random active idol who may already be in another slot, so she then counts twice everywhere the
    // show uses its cast. Swap such a duplicate for an idol who isn't in the cast, or leave the slot empty if there
    // is none. State-based: does nothing when the refill isn't a duplicate, and refills a slot another mod emptied
    // after the game's refill. When another mod skips the game's RemoveGirl, its result is kept.
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.RemoveGirl))]
    public class Shows__show_RemoveGirl
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Shows__show_RemoveGirl>(ex);

        private static readonly System.Random Rng = new System.Random();

        public static void Prefix(Shows._show __instance, data_girls.girls _Girl, out List<int> __state)
        {
            __state = new List<int>();
            if (__instance.castType != Shows._show._castType.permanentCast)
                return;
            for (int i = 0; i < __instance.girls.Length; i++)
            {
                if (__instance.girls[i] == _Girl)
                    __state.Add(i);
            }
        }

        public static void Postfix(Shows._show __instance, data_girls.girls _Girl, List<int> __state, bool __runOriginal)
        {
            if (!__runOriginal)
                return;
            bool changed = false;
            foreach (int slot in __state)
            {
                data_girls.girls refill = __instance.girls[slot];
                if (refill != null && !IsElsewhere(__instance.girls, slot, refill))
                    continue;
                List<data_girls.girls> free = data_girls.GetActiveGirls().Where(g => g != _Girl && !__instance.girls.Contains(g)).ToList();
                data_girls.girls replacement = free.Count > 0 ? free[Rng.Next(free.Count)] : null;
                changed |= replacement != refill;
                __instance.girls[slot] = replacement;
            }
            if (changed)
                __instance.Update?.Invoke();
        }

        public static bool IsElsewhere(data_girls.girls[] cast, int slot, data_girls.girls girl)
        {
            for (int i = 0; i < cast.Length; i++)
            {
                if (i != slot && cast[i] == girl)
                    return true;
            }
            return false;
        }
    }

	// Fixed senbatsu stats calculation so it doesn't punish you if you don't have enough idols to fill all rows
    [HarmonyPatch(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam))]
    public class singles__single_SenbatsuCalcParam
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<singles__single_SenbatsuCalcParam>(ex);

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

            // Read once: another mod's transpiler may hand over a sequence that rebuilds on every read.
            instructions = instructions.ToList();
            if (instructions.Any(ci => IlHelpers.IsCallTo(ci, infix)))
            {
                // Already calls the Infix: patched twice.
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
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<singles__single_GetSenbatsuParamValue>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo calcParam = AccessTools.Method(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam),
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

    // Edgy and Artsy PVs now change sales and new fans. The game checks the wrong fan field for them
    // (gender == teen, hardcoreness == female, ...), which is never true, so they did nothing on success or
    // failure. Compare the field each fan type belongs to, the targets the marketing spinner shows.
    [HarmonyPatch(typeof(singles), nameof(singles.ValueAfterMarketing))]
    public class singles_ValueAfterMarketing
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<singles_ValueAfterMarketing>(ex);

        // Edgy and Artsy each check 2 fields on success and 2 on failure.
        public const int ExpectedFixes = 8;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo gender = AccessTools.Field(typeof(resources._fan), nameof(resources._fan.gender));
            FieldInfo hardcoreness = AccessTools.Field(typeof(resources._fan), nameof(resources._fan.hardcoreness));
            FieldInfo age = AccessTools.Field(typeof(resources._fan), nameof(resources._fan.age));

            List<CodeInstruction> codes = instructions.ToList();
            List<int> fixes = new List<int>();
            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode != OpCodes.Ldfld || !(codes[i].operand is FieldInfo field) || (field != gender && field != hardcoreness))
                    continue;

                FieldInfo owner = OwnerOfComparedType(codes[i + 1], gender, hardcoreness, age);
                if (owner != null && owner != field)
                    fixes.Add(i);
            }

            if (fixes.Count == 0)
            {
                // Already compares the right fields: patched twice, or the game fixed it.
                return codes;
            }
            if (fixes.Count != ExpectedFixes)
            {
                PatchLog.WarnOncePerPatch<singles_ValueAfterMarketing>("found " + fixes.Count + " wrong fan fields, expected " + ExpectedFixes + "; left unchanged.");
                return codes;
            }

            foreach (int i in fixes)
                codes[i].operand = OwnerOfComparedType(codes[i + 1], gender, hardcoreness, age);
            return codes;
        }

        // The fan field holding the fan type an instruction compares against: ldc.i4 <type>, or brtrue/brfalse
        // for male (0). Null if it doesn't compare against a fan type.
        private static FieldInfo OwnerOfComparedType(CodeInstruction compare, FieldInfo gender, FieldInfo hardcoreness, FieldInfo age)
        {
            if (IlHelpers.IsBranchTrue(compare) || compare.opcode == OpCodes.Brfalse || compare.opcode == OpCodes.Brfalse_S)
                return gender;
            if (IlHelpers.IsLdcI4(compare, (int)resources.fanType.male) || IlHelpers.IsLdcI4(compare, (int)resources.fanType.female))
                return gender;
            if (IlHelpers.IsLdcI4(compare, (int)resources.fanType.casual) || IlHelpers.IsLdcI4(compare, (int)resources.fanType.hardcore))
                return hardcoreness;
            if (IlHelpers.IsLdcI4(compare, (int)resources.fanType.teen) || IlHelpers.IsLdcI4(compare, (int)resources.fanType.youngAdult) || IlHelpers.IsLdcI4(compare, (int)resources.fanType.adult))
                return age;
            return null;
        }
    }

    // Fixed a freeze when generating some platinum and gold idols (auditions, Aya, hiring a rival).
    // The stat roll reserves up to 3 stats, then spends the rest of the points on the other stats, up to 99 each.
    // When the points can't fit (about 1 in 12 platinum rolls), the spending loop never ends.
    // The roll that can't fit is thrown away and the game rolls again, so every idol is one the game could make.
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GenerateParams))]
    public class data_girls_GenerateParams
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_GenerateParams>(ex);

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

            // Read once: another mod's transpiler may hand over a sequence that rebuilds on every read.
            instructions = instructions.ToList();
            if (instructions.Any(ci => IlHelpers.IsCallTo(ci, shouldReroll)))
            {
                // Already rerolls: patched twice.
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


    // Fixes the English birthday title, which says "@th birthday" for every age ("21th"). In English, uses this mod's
    // UNOFFICIALPATCH__BD_TITLE_1/2 text instead, which needs no ordinal. Other languages, and a title another mod
    // already changed, are left alone.
    [HarmonyPatch(typeof(Birthday_Popup), nameof(Birthday_Popup.RenderTitle))]
    public class Birthday_Popup_RenderTitle
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Birthday_Popup_RenderTitle>(ex);

        public const string TextLanguage = "en";
        public const string Title1 = "UNOFFICIALPATCH__BD_TITLE_1";
        public const string Title2 = "UNOFFICIALPATCH__BD_TITLE_2";

        public static void Postfix(Birthday_Popup __instance)
        {
            if (__instance.Title == null || __instance.Girl == null)
                return;
            string title = NewTitle(staticVars.Settings.Language, ExtensionMethods.GetText(__instance.Title),
                __instance.Girl.GetName(), __instance.Girl.GetAge().ToString());
            if (title != null)
                ExtensionMethods.SetText(__instance.Title, title);
        }

        /// <summary>The title from this mod's text; null to keep the current title.</summary>
        public static string NewTitle(string language, string title, string name, string age)
        {
            if (language != TextLanguage || !Language.Data.ContainsKey(Title1) || !Language.Data.ContainsKey(Title2))
                return null;
            if (title != Language.Insert("BD__TITLE_1", name) + " " + Language.Insert("BD__TITLE_2", age))
                return null;
            return Language.Insert(Title1, name) + " " + Language.Insert(Title2, age);
        }
    }

    // Dating status is visible for underage members. The game returns "" unless the idol is an adult
    // (Is_AOC); treat every idol as one, so the rest of the game's own method builds the text.
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetPartnerString))]
    public class data_girls_girls_GetPartnerString
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_girls_GetPartnerString>(ex);

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
    [HarmonyPatch(typeof(resources._fanOpinion), nameof(resources._fanOpinion.Add))]
    public class resources__fanOpinion_Add
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<resources__fanOpinion_Add>(ex);

        // The change, and the opinions of the idols' fans it applies to, before anything else ran.
        public class Before
        {
            public float Val;
            public List<KeyValuePair<resources._fan, Opinion>> Opinions = new List<KeyValuePair<resources._fan, Opinion>>();
        }

        // A fan's opinion: what AddOpinion changes.
        public struct Opinion
        {
            public int[] Vals;
            public float Temp;

            public static Opinion Of(resources._fan fan)
            {
                return new Opinion { Vals = fan.Vals != null ? fan.Vals.ToArray() : null, Temp = fan.Temp };
            }

            public bool SameAs(resources._fan fan)
            {
                if (Temp != fan.Temp)
                    return false;
                if (Vals == null || fan.Vals == null)
                    return Vals == null && fan.Vals == null;
                return Vals.SequenceEqual(fan.Vals);
            }
        }

        // First, so the opinions are read before any other mod's patch changes them.
        // The game counts val down to its fraction as it records whole points, so keep the full change.
        [HarmonyPriority(Priority.First)]
        public static void Prefix(resources._fanOpinion __instance, float val, out Before __state)
        {
            __state = new Before { Val = val };
            foreach (data_girls.girls girl in Eligible())
            {
                foreach (resources._fan fan in girl.Fans)
                {
                    if (fan != null && fan.IsType(__instance.type))
                        __state.Opinions.Add(new KeyValuePair<resources._fan, Opinion>(fan, Opinion.Of(fan)));
                }
            }
        }

        // Last, so it sees what other mods' patches did.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(resources._fanOpinion __instance, Before __state, bool __runOriginal)
        {
            // Another mod skipped the change.
            if (!__runOriginal || __state == null)
                return;
            // The game doesn't touch idols' fans here, so a changed opinion means the change already reached them.
            if (__state.Opinions.Any(entry => !entry.Value.SameAs(entry.Key)))
                return;

            // Propagate global fan opinion changes to each active girl.
            foreach (data_girls.girls girl in Eligible())
            {
                // Apply the appeal delta for the matching fan type.
                girl.AddAppeal(__instance.type, __state.Val);
            }
        }

        // Idols who gain appeal: not null, sick or graduated.
        private static IEnumerable<data_girls.girls> Eligible()
        {
            if (data_girls.girl == null)
                yield break;
            foreach (data_girls.girls girl in data_girls.girl)
            {
                if (girl != null && girl.Fans != null && !girl.IsSick() && girl.status != data_girls._status.graduated)
                    yield return girl;
            }
        }
    }


    // Fixed gossip so girl doesn't gossip about herself
    [HarmonyPatch(typeof(Date_Gossip), nameof(Date_Gossip.GetAvailableGossips))]
    public class Date_Gossip_GetAvailableGossips
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Date_Gossip_GetAvailableGossips>(ex);

        public static void Postfix(ref List<Date_Gossip._gossip> __result, data_girls.girls Snitch)
        {
            // Nothing to filter if the list is empty.
            if (__result == null || __result.Count == 0)
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
    [HarmonyPatch(typeof(vn_requirements), nameof(vn_requirements.CheckGirl), new Type[] { typeof(data_girls.girls), typeof(string), typeof(string) })]
    public class vn_requirements_CheckGirl
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<vn_requirements_CheckGirl>(ex);

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
    [HarmonyPatch(typeof(vn_requirements), nameof(vn_requirements.CheckGirl), new Type[] { typeof(data_girls.girls), typeof(string), typeof(string) })]
    public class vn_requirements_CheckGirl_Variable
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<vn_requirements_CheckGirl_Variable>(ex);

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
    [HarmonyPatch(typeof(Activities._activity), nameof(Activities._activity.GetDescription))]
    public class Activities__activity_GetDescription
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Activities__activity_GetDescription>(ex);

        // The Energetic policy's stamina cost, as the game's Activities.GetStaminaCost charges it (-4f). Used when
        // the scene's Activities component can't be reached; the unit tests check the two match.
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
                    StaminaCost(),
                    Language.Data[PointsKey],
                    CostSeparator,
                    Language.Data[StaminaKey].ToLower()
                });
            }
        }

        // The cost the game charges, including any other mod's change to it.
        private static int StaminaCost()
        {
            try
            {
                return ChargedStaminaCost();
            }
            catch (Exception)
            {
                // No scene (main menu, unit tests): the game's own cost.
                return EnergeticStaminaCost;
            }
        }

        // Separate, so a failure to reach the scene is caught by the caller.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int ChargedStaminaCost()
        {
            return Mathf.RoundToInt(-Camera.main.GetComponent<mainScript>().Data.GetComponent<Activities>().GetStaminaCost());
        }
    }


    // Fix custom event check for "hired a staffer of type"
    [HarmonyPatch(typeof(vn_requirements), nameof(vn_requirements.CheckMeta))]
    public class vn_requirements_CheckMeta
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<vn_requirements_CheckMeta>(ex);

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

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(bool) })]
    public class SaveManager_LoadData_Bool_NullGuard
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_LoadData_Bool_NullGuard>(ex);

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
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(string) })]
    public class SaveManager_LoadData_Path_NullGuard
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_LoadData_Path_NullGuard>(ex);

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

    // Fixes saving and loading pushing back the bankruptcy deadline: BankruptcyDanger isn't saved, so loading a save
    // in the red clears it, and the next day the game sets it again with a fresh one-month deadline. Restore the flag
    // after a load, keeping the loaded deadline (OnMoneyChange sets it whenever money is below zero).
    // Idempotent: it sets the flag from the same condition however often it runs.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(bool) })]
    public class SaveManager_LoadData_Bool_Bankruptcy
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_LoadData_Bool_Bankruptcy>(ex);

        public static void Postfix() => Bankruptcy_Restore.AfterLoad();
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(string) })]
    public class SaveManager_LoadData_Path_Bankruptcy
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_LoadData_Path_Bankruptcy>(ex);

        public static void Postfix() => Bankruptcy_Restore.AfterLoad();
    }

    public static class Bankruptcy_Restore
    {
        public static void AfterLoad()
        {
            if (resources.Money() < 0)
                loans.BankruptcyDanger = true;
        }
    }

    // Marks loads the main menu makes into the game scene it has just opened. Starting another game
    // from the main menu also ends an earlier scene's autosave block.
    [HarmonyPatch(typeof(MainMenu_LoadGameManager), nameof(MainMenu_LoadGameManager.LoadAsync))]
    public class MainMenu_LoadGameManager_LoadAsync
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<MainMenu_LoadGameManager_LoadAsync>(ex);

        public static void Prefix(bool loadSave)
        {
            FailedLoadGuard.MenuLoadPending = loadSave;
            FailedLoadGuard.BlockedSaveManager = null;
        }
    }

    // Skips autosaves in a game scene whose save failed to load from the main menu.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveData), new Type[] { typeof(bool), typeof(bool) })]
    public class SaveManager_SaveData
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_SaveData>(ex);

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

        private static readonly FieldInfo TexturesInstance = AccessTools.Field(typeof(data_girls_textures), nameof(data_girls_textures._this));
        private static readonly FieldInfo QueueField = AccessTools.Field(typeof(data_girls_textures), nameof(data_girls_textures.Queue));
        private static readonly FieldInfo QueueGirl = AccessTools.Field(AccessTools.Inner(typeof(data_girls_textures), nameof(data_girls_textures._queue)), nameof(data_girls_textures._queue.Girl));
        private static readonly FieldInfo GoldenCardPortrait = AccessTools.Field(typeof(Audition_Golden_Card), nameof(Audition_Golden_Card.Portrait_));

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
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.LoadCards))]
    public class Popup_Audition_LoadCards
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Popup_Audition_LoadCards>(ex);

        public static void Prefix()
        {
            PortraitLoading.cardsLoadedAt = PortraitLoading.Now();
        }
    }

    // After 2 s the audition's cards unlock; those still without a portrait get the clear placeholder.
    // Each card's loader keeps running and sets her portrait when it arrives.
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.PortraitsLoaded))]
    public class Popup_Audition_PortraitsLoaded
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Popup_Audition_PortraitsLoaded>(ex);

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
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Popup_Audition_OpenCard>(ex);

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
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Popup_Audition_Close>(ex);

        private static readonly FieldInfo DataField = AccessTools.Field(typeof(Popup_Audition), nameof(Popup_Audition.Data));

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
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_textures_setPortrait>(ex);

        public static void Postfix(List<GameObject> target, ref IEnumerator __result)
        {
            if (__result != null && target != null && target.Count > 0)
                __result = PortraitLoading.WhileTargetsExist(__result, target);
        }
    }

    // Skips setting a portrait on a destroyed object. The queue's job that retries a failed portrait load sets
    // it before starting the next job, so the error stopped every later portrait for the rest of the session.
    [HarmonyPatch(typeof(data_girls_textures), nameof(data_girls_textures.SetSprite))]
    public class data_girls_textures_SetSprite
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_textures_SetSprite>(ex);

        public static bool Prefix(GameObject obj)
        {
            return obj != null;
        }
    }

    // A portrait render that takes over 2 s lets the queue move on, and finishes in the background.
    // Each renderer has its own camera and render texture and is placed apart from the others, so renders
    // can run side by side.
    [HarmonyPatch(typeof(data_girls_textures), nameof(data_girls_textures.NEW_Cache_Portrait), MethodType.Enumerator)]
    public class data_girls_textures_NEW_Cache_Portrait
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_textures_NEW_Cache_Portrait>(ex);

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

    // Fixes a promoted male producer's blank small portrait: the staff thumbnail atlas has Player_Female_Pro but no
    // Player_Male_Pro. Both patches below fill in this mod's ready-made picture (assets/Images) only when the atlas
    // lookup came back empty, so a mod that supplies the sprite or takes over LoadPortrait_Small wins.
    public static class PlayerThumbnail
    {
        public const string ModTitle = "Unofficial Patch";
        public static readonly string RelativePath = Path.Combine("Images", "Player_Male_Pro.png");

        private static Sprite sprite;
        private static bool loadFailed;

        /// <summary>True for the one thumbnail the atlas lacks: small, promoted, the male producer, not unique staff.</summary>
        public static bool IsMissingThumbnail(staff._type type, bool big, bool pro, staff._staff._unique_type uniqueType)
        {
            return !big && pro && uniqueType == staff._staff._unique_type.NONE && type == staff._type.player;
        }

        /// <summary>The picture's path: next to the DLL, else in a mod folder with this mod's title; null if neither has it.</summary>
        public static string FindFile(string assemblyDir, IEnumerable<string> modDirs)
        {
            return new[] { assemblyDir }.Concat(modDirs)
                .Where(dir => !string.IsNullOrEmpty(dir))
                .Select(dir => Path.Combine(dir, RelativePath))
                .FirstOrDefault(File.Exists);
        }

        /// <summary>The picture, loaded once; null (logged once) if it can't be loaded, which leaves the game's blank portrait.</summary>
        public static Sprite Get()
        {
            if (sprite != null || loadFailed)
                return sprite;

            string path = FindFile(Path.GetDirectoryName(typeof(PlayerThumbnail).Assembly.Location),
                Mods._Mods.Where(mod => mod.Title == ModTitle).Select(mod => mod.GetPath()));
            sprite = path != null ? IMG2Sprite.instance.LoadNewSprite(path, 100f) : null;
            if (sprite != null)
                sprite.name = "Player_Male_Pro";
            else
            {
                loadFailed = true;
                PatchLog.Error<staff_GetPortrait>("couldn't load " + RelativePath + "; a promoted male producer keeps the game's blank thumbnail.");
            }
            return sprite;
        }
    }

    // Thumbnails once the atlas is loaded: LoadPortrait_Small uses GetPortrait's sprite if it isn't null.
    [HarmonyPatch(typeof(staff), nameof(staff.GetPortrait))]
    public class staff_GetPortrait
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<staff_GetPortrait>(ex);

        public static void Postfix(staff._type Type, bool Big, bool Pro, staff._staff._unique_type UniqueType, SpriteAtlas ___Portrait_Thumbs, ref Sprite __result)
        {
            // A null atlas means it isn't loaded yet: LoadPortrait_Small then loads it, and the patch below takes over
            if (__result == null && ___Portrait_Thumbs != null && PlayerThumbnail.IsMissingThumbnail(Type, Big, Pro, UniqueType))
                __result = PlayerThumbnail.Get();
        }
    }

    // The first thumbnail: LoadPortrait_Small loads the atlas and sets the sprite in a callback (a compiler-made closure).
    [HarmonyPatch]
    public class staff_LoadPortrait_Small_AtlasLoaded
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<staff_LoadPortrait_Small_AtlasLoaded>(ex);

        public static MethodBase TargetMethod()
        {
            return typeof(staff).GetNestedTypes(AccessTools.all)
                .SelectMany(type => type.GetMethods(AccessTools.all))
                .Single(method => method.Name.Contains("<" + nameof(staff.LoadPortrait_Small) + ">"));
        }

        public static void Postfix(Image ___target, staff._type ___Type, bool ___Pro, staff._staff._unique_type ___UniqueType)
        {
            if (___target != null && ___target.sprite == null && PlayerThumbnail.IsMissingThumbnail(___Type, false, ___Pro, ___UniqueType))
                ___target.sprite = PlayerThumbnail.Get();
        }
    }

}
