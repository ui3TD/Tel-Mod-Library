using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;

namespace UnofficialPatch
{
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
}
