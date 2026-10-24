using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Module = NationalTour.Readiness.Module;

namespace NationalTour
{
    /// <summary>
    /// The game's buttons reach "the" tour handler with GetComponent&lt;SEvent_Tour&gt;(). At each place where the
    /// tour is known, the national handler is used for a national tour: the popup's Continue, dropping a tour card
    /// on a room, the card's Launch and Cancel, and the results' Finish.
    /// </summary>
    public static class Routing
    {
        /// <summary>
        /// The handler that launched the results popup. The popup has forgotten its tour by the time Finish is clicked.
        /// </summary>
        public static SEvent_Tour Launcher;

        public static SEvent_Tour HandlerFor(SEvent_Tour game, SEvent_Tour.tour tour) => NationalHandler.For(game, tour);

        public static SEvent_Tour LauncherOr(SEvent_Tour game) => Launcher is not null ? Launcher : game;

        private static readonly AccessTools.FieldRef<SEvent_Button_Tour, SEvent_Tour> CardHandler =
            AccessTools.FieldRefAccess<SEvent_Button_Tour, SEvent_Tour>("_Sevent_Tour");

        /// <summary>
        /// A card for a national tour launches and cancels the national tour.
        /// </summary>
        public static void SetCardHandler(SEvent_Button_Tour card)
        {
            SEvent_Tour game = CardHandler(card);
            if (game is not null)
                CardHandler(card) = NationalHandler.For(game, card.Tour);
        }

        /// <summary>
        /// GetComponent&lt;SEvent_Tour&gt;() then the given instructions, which turn the game's handler into the one to use.
        /// </summary>
        public static List<CodeInstruction> RouteHandler(IEnumerable<CodeInstruction> instructions, string method, params CodeInstruction[] route) =>
            ILEdit.Rewrite(instructions, Module.Routing, method,
                new ILEdit.Edit(ILEdit.IsGetTourHandler, 1, call => new[] { call }.AddRangeToArray(route)));
    }


    // Continue on the new tour popup: SetTour on the tour's own handler
    [HarmonyPatch(typeof(Tour_New_Popup), nameof(Tour_New_Popup.OnContinue))]
    public class Tour_New_Popup_OnContinue
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Routing.RouteHandler(instructions, nameof(Tour_New_Popup.OnContinue),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Tour_New_Popup), nameof(Tour_New_Popup.Tour))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Routing), nameof(Routing.HandlerFor))));
    }


    // A tour card dropped on a room: Assign on the tour's own handler, which gives the room that handler's tour
    [HarmonyPatch(typeof(DragAndDropManager), nameof(DragAndDropManager.OnMouseUp), new[] { typeof(SEvent_Tour.tour) })]
    public class DragAndDropManager_OnMouseUp
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Routing.RouteHandler(instructions, nameof(DragAndDropManager.OnMouseUp),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Routing), nameof(Routing.HandlerFor))));
    }


    // Finish on the results popup: FinishTour on the handler that launched it
    [HarmonyPatch(typeof(Tour_Popup), nameof(Tour_Popup.OnFinish))]
    public class Tour_Popup_OnFinish_Routing
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Routing.RouteHandler(instructions, nameof(Tour_Popup.OnFinish),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Routing), nameof(Routing.LauncherOr))));
    }


    // Which handler launched the results popup
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.StartTour))]
    public class SEvent_Tour_StartTour_Routing
    {
        public static void Prefix(SEvent_Tour __instance) => Routing.Launcher = __instance;
    }


    // A tour card finds its handler in Start, after the game has given it its tour
    [HarmonyPatch(typeof(SEvent_Button_Tour), "Start")]
    public class SEvent_Button_Tour_Start
    {
        public static void Postfix(SEvent_Button_Tour __instance) => Routing.SetCardHandler(__instance);
    }
}
