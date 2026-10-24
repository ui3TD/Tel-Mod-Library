using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;

namespace UnofficialPatch
{
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
}
