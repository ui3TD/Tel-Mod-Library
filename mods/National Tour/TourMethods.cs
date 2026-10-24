using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Module = NationalTour.Readiness.Module;

namespace NationalTour
{
    /// <summary>
    /// The game's SetTour, StartTour, FinishTour and CancelTour each fetch the World Tour's record directly. On the
    /// national handler they get the national record instead, and two story calls run for world tours only: the
    /// World Tour achievement when a tour starts, and the world_traveler scene and Aya's task when it finishes.
    /// On the game's handler every call stays as it is.
    /// </summary>
    public static class TourMethods
    {
        private static readonly MethodInfo GetEvent = AccessTools.Method(typeof(SpecialEvents_Manager), nameof(SpecialEvents_Manager.GetEvent));
        private static readonly MethodInfo Unlock = AccessTools.Method(typeof(Achievements), nameof(Achievements.Unlock));
        private static readonly MethodInfo OnWorldTour = AccessTools.Method(typeof(tasks), nameof(tasks.OnWorldTour));

        public static SpecialEvents_Manager._specialEvent EventFor(SpecialEvents_Manager._type type, SEvent_Tour handler) =>
            NationalHandler.IsNationalHandler(handler) ? NationalRecord.Record : SpecialEvents_Manager.GetEvent(type);

        public static void UnlockFor(Achievements.ID id, SEvent_Tour handler)
        {
            if (!NationalHandler.IsNationalHandler(handler))
                Achievements.Unlock(id);
        }

        public static void OnWorldTourFor(SEvent_Tour.tour tour, SEvent_Tour handler)
        {
            if (!NationalHandler.IsNationalHandler(handler))
                tasks.OnWorldTour(tour);
        }

        /// <summary>
        /// The call, with the handler (this) passed to its stand-in.
        /// </summary>
        private static IEnumerable<CodeInstruction> WithHandler(CodeInstruction call, string standIn) => new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(call).MoveBlocksFrom(call),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TourMethods), standIn)),
        };

        public static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, string method, bool achievement = false, bool story = false)
        {
            List<ILEdit.Edit> edits = new()
            {
                new ILEdit.Edit(i => ILEdit.IsCall(i, GetEvent), 1, call => WithHandler(call, nameof(EventFor))),
            };
            if (achievement)
                edits.Add(new ILEdit.Edit(i => ILEdit.IsCall(i, Unlock), 1, call => WithHandler(call, nameof(UnlockFor))));
            if (story)
                edits.Add(new ILEdit.Edit(i => ILEdit.IsCall(i, OnWorldTour), 1, call => WithHandler(call, nameof(OnWorldTourFor))));
            return ILEdit.Rewrite(instructions, Module.TourMethods, method, edits.ToArray());
        }

        /// <summary>
        /// How many national tours have finished.
        /// </summary>
        public static int FinishedNationalTours()
        {
            int count = 0;
            foreach (SEvent_Tour.tour tour in SEvent_Tour.Tours)
            {
                if (tour.Status == SEvent_Tour.tour._status.finished && Prefectures.IsNational(tour))
                    count++;
            }
            return count;
        }
    }


    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.SetTour))]
    public class SEvent_Tour_SetTour
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            TourMethods.Rewrite(instructions, nameof(SEvent_Tour.SetTour));
    }


    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.StartTour))]
    public class SEvent_Tour_StartTour_Methods
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            TourMethods.Rewrite(instructions, nameof(SEvent_Tour.StartTour), achievement: true);
    }


    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.FinishTour))]
    public class SEvent_Tour_FinishTour
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            TourMethods.Rewrite(instructions, nameof(SEvent_Tour.FinishTour), story: true);
    }


    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.CancelTour))]
    public class SEvent_Tour_CancelTour
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            TourMethods.Rewrite(instructions, nameof(SEvent_Tour.CancelTour));
    }


    // Only the "has gone on a world tour" dialogue condition reads this, and it's about visiting other countries
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.GetWorldTourCount))]
    public class SEvent_Tour_GetWorldTourCount
    {
        public static void Postfix(ref int __result) => __result -= TourMethods.FinishedNationalTours();
    }
}
