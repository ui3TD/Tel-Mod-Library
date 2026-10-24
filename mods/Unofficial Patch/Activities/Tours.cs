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
        public static void Postfix(SEvent_Tour.tour __instance, bool __runOriginal)
        {
            // Another mod that skipped the game's cost calculation keeps its saving.
            if (!__runOriginal || __instance.Saving <= 0)
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
    // level, so the penalty never applied; compare the fame level, as the tour description says. Can be turned off
    // in ModMenus (Settings.TourFame).
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

        // The fame level, in place of resources.Get(type, useTotal) for fame; the game's fame points while the fix
        // is turned off.
        public static long FameLevel(resources.type type, bool useTotal)
        {
            long points = resources.Get(type, useTotal);
            return Settings.On(Settings.TourFame) ? resources.FamePointsToLevel(points) : points;
        }
    }

    // Repairs a save with an unfinished tour that no tour handler holds: a "leftover", such as the second tour in
    // production when a mod that runs two tours at once is removed. The game's event cards act on the tour in the
    // handler's one slot, not on their own: the leftover's Launch and Cancel launched or cancelled the other tour,
    // dropping it on a room staffed the other tour, and with the slot empty its Launch crashed and its Cancel
    // ended an unrelated room's job. Now each card acts on its own tour. A leftover moves into an empty slot; while
    // the slot holds another tour, its Cancel cancels it alone, its Launch waits, and a drop staffs it.
    internal static class LeftoverTours
    {
        public const string SlotTakenKey = "UNOFFICIALPATCH__TOUR_SLOT_TAKEN";
        public const string SlotTakenFallback = "Finish or cancel the World Tour in production before you launch this one.";

        private static readonly AccessTools.FieldRef<SEvent_Button_Tour, SEvent_Tour> CardHandler =
            AccessTools.FieldRefAccess<SEvent_Button_Tour, SEvent_Tour>("_Sevent_Tour");
        private static readonly AccessTools.FieldRef<SEvent_Button_Tour, bool> CardDisabled =
            AccessTools.FieldRefAccess<SEvent_Button_Tour, bool>("disabled");

        // Every tour handler next to this one (a mod can add more), the agency's rooms, and the event list's redraw.
        // The tests replace them.
        internal static Func<SEvent_Tour, SEvent_Tour[]> Handlers = handler => handler.GetComponents<SEvent_Tour>();
        internal static Func<List<agency._room>> Rooms = () => Camera.main.GetComponent<mainScript>().Data.GetComponent<agency>().allRooms();
        internal static Action<SEvent_Tour> RedrawEventList = handler => handler.GetComponent<SpecialEvents_Manager>().RenderTab();

        // The tour being dropped on a room, from DragAndDropManager.OnMouseUp to its end.
        internal static SEvent_Tour.tour Dropped;

        // An unfinished tour in the game's list that no tour handler has in its slot.
        public static bool IsLeftover(SEvent_Tour.tour tour, SEvent_Tour handler)
        {
            if (tour == null || handler is null || tour.Status == SEvent_Tour.tour._status.finished || !SEvent_Tour.Tours.Contains(tour))
                return false;
            foreach (SEvent_Tour each in Handlers(handler) ?? new SEvent_Tour[0])
            {
                if (each is not null && ReferenceEquals(each.Tour, tour))
                    return false;
            }
            return true;
        }

        // The card's handler and tour, when its tour is a leftover and the card isn't disabled.
        private static bool LeftoverCard(SEvent_Button_Tour card, out SEvent_Tour handler, out SEvent_Tour.tour tour)
        {
            handler = card is null ? null : CardHandler(card);
            tour = card is null ? null : card.Tour;
            return card is not null && !CardDisabled(card) && IsLeftover(tour, handler);
        }

        // Launch: moves a leftover into an empty slot, then the game launches it. While the slot holds another tour,
        // the game's Launch is skipped and the player is told why.
        public static bool OnLaunch(SEvent_Button_Tour card)
        {
            if (!LeftoverCard(card, out SEvent_Tour handler, out SEvent_Tour.tour tour))
                return true;
            if (handler.Tour == null)
            {
                handler.Tour = tour;
                return true;
            }
            Notify();
            return false;
        }

        // Cancel: moves a leftover into an empty slot, then the game cancels it. While the slot holds another tour,
        // the leftover is cancelled on its own and the game's Cancel is skipped.
        public static bool OnCancel(SEvent_Button_Tour card)
        {
            if (!LeftoverCard(card, out SEvent_Tour handler, out SEvent_Tour.tour tour))
                return true;
            if (handler.Tour == null)
            {
                handler.Tour = tour;
                return true;
            }
            Cancel(handler, tour);
            return false;
        }

        // The game's CancelTour for a tour that isn't in the slot. It ends every room's job on the tour, where the
        // game stops at the first, and the World Tour's record stays in production for the slot's tour.
        public static void Cancel(SEvent_Tour handler, SEvent_Tour.tour tour)
        {
            foreach (agency._room room in Rooms() ?? new List<agency._room>())
            {
                if (room != null && room.tour == tour)
                    room.CancelJob();
            }
            SEvent_Tour.Tours.Remove(tour);
            SpecialEvents_Manager.GetEvent(SpecialEvents_Manager._type.WorldTour)?.UpdateStatus();
            RedrawEventList(handler);
        }

        // Assign during a drop: a leftover moves into an empty slot, then the game assigns it. While the slot holds
        // another tour, the room gets the leftover, as the game's Assign would give it the slot's tour.
        public static bool OnAssign(SEvent_Tour handler, agency._room room)
        {
            SEvent_Tour.tour tour = Dropped;
            if (tour == null || room == null || !IsLeftover(tour, handler))
                return true;
            if (handler.Tour == null)
            {
                handler.Tour = tour;
                return true;
            }
            SEvent_Tour.activeRoom = room;
            room.assign(tour);
            return false;
        }

        public static void Notify()
        {
            string text = Language.Data.TryGetValue(SlotTakenKey, out string translated) ? translated : SlotTakenFallback;
            NotificationManager.AddNotification(text, mainScript.red32, NotificationManager._notification._type.other);
        }

        // An error in one of these is logged and named in a red notification once per session; the game's own
        // method then runs as it would without the fix.
        private static readonly HashSet<string> Reported = new();

        public static bool Failed<TPatch>(Exception e)
        {
            try
            {
                if (Reported.Add(typeof(TPatch).Name))
                {
                    PatchLog.Error<TPatch>("failed; the game's own method ran instead: " + e);
                    mainScript_TimeProgress.Report(typeof(TPatch).Name);
                }
            }
            catch (Exception reportError)
            {
                Debug.LogError("[UnofficialPatch] couldn't report a failed tour fix: " + reportError);
            }
            return true;
        }

        internal static void ResetReports() => Reported.Clear();
    }

    [HarmonyPatch(typeof(SEvent_Button_Tour), nameof(SEvent_Button_Tour.OnLaunch))]
    public class SEvent_Button_Tour_OnLaunch
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Button_Tour_OnLaunch>(ex);

        // Last, so it sees whether another mod's prefix already skipped the game's Launch.
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(SEvent_Button_Tour __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return true;
            try
            {
                return LeftoverTours.OnLaunch(__instance);
            }
            catch (Exception e)
            {
                return LeftoverTours.Failed<SEvent_Button_Tour_OnLaunch>(e);
            }
        }
    }

    [HarmonyPatch(typeof(SEvent_Button_Tour), nameof(SEvent_Button_Tour.OnCancel))]
    public class SEvent_Button_Tour_OnCancel
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Button_Tour_OnCancel>(ex);

        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(SEvent_Button_Tour __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return true;
            try
            {
                return LeftoverTours.OnCancel(__instance);
            }
            catch (Exception e)
            {
                return LeftoverTours.Failed<SEvent_Button_Tour_OnCancel>(e);
            }
        }
    }

    // Notes which tour is being dropped, for the Assign prefix below; the rest of the drop is the game's.
    [HarmonyPatch(typeof(DragAndDropManager), nameof(DragAndDropManager.OnMouseUp), new[] { typeof(SEvent_Tour.tour) })]
    public class DragAndDropManager_OnMouseUp_Tour
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<DragAndDropManager_OnMouseUp_Tour>(ex);

        // The game's parameter is named show
        public static void Prefix(SEvent_Tour.tour show) => LeftoverTours.Dropped = show;

        // Forgotten even if the drop throws
        public static Exception Finalizer(Exception __exception)
        {
            LeftoverTours.Dropped = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.Assign))]
    public class SEvent_Tour_Assign
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SEvent_Tour_Assign>(ex);

        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(SEvent_Tour __instance, agency._room room, bool __runOriginal)
        {
            if (!__runOriginal)
                return true;
            try
            {
                return LeftoverTours.OnAssign(__instance, room);
            }
            catch (Exception e)
            {
                return LeftoverTours.Failed<SEvent_Tour_Assign>(e);
            }
        }
    }
}
