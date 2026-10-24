using HarmonyLib;
using EventType = SpecialEvents_Manager._type;
using SpecialEvent = SpecialEvents_Manager._specialEvent;

namespace NationalTour
{
    /// <summary>
    /// The national tour's cooldown and "in production" record, separate from the World Tour's. It is a game
    /// record of type WorldTour, so the game gives it the same 3-month cooldown, but it is held by the mod and
    /// never added to the game's saved list.
    /// </summary>
    public static class NationalRecord
    {
        /// <summary>
        /// The Special Events tab number of the National Tour tab. The game has three (0-2).
        /// </summary>
        public const EventType Tab = (EventType)1200;

        public static SpecialEvent Record { get; private set; } = New();

        private static SpecialEvent New() => new() { Type = EventType.WorldTour, Status = SpecialEvent._status.normal };

        /// <summary>
        /// A new game or save starts with no national cooldown.
        /// </summary>
        public static void Reset() => Record = New();

        public static bool IsNationalRecord(SpecialEvent record) => ReferenceEquals(record, Record);

        /// <summary>
        /// Whether a tour of this kind is planned or in production (any tour of that kind not finished yet).
        /// </summary>
        public static bool AnyUnfinished(bool national)
        {
            foreach (SEvent_Tour.tour tour in SEvent_Tour.Tours)
            {
                if (tour.Status != SEvent_Tour.tour._status.finished && Prefectures.IsNational(tour) == national)
                    return true;
            }
            return false;
        }
    }


    // The game makes its records for a new game here; the national one starts over with them
    [HarmonyPatch(typeof(SpecialEvents_Manager), "Start")]
    public class SpecialEvents_Manager_Start
    {
        public static void Postfix() => NationalRecord.Reset();
    }


    // The National Tour tab's number finds the national record; the game has no record for it
    [HarmonyPatch(typeof(SpecialEvents_Manager), nameof(SpecialEvents_Manager.GetEvent))]
    public class SpecialEvents_Manager_GetEvent
    {
        public static void Postfix(EventType type, ref SpecialEvent __result)
        {
            if (__result == null && type == NationalRecord.Tab && Readiness.IsReady(Readiness.Module.NationalRecord))
                __result = NationalRecord.Record;
        }
    }


    // Each tour record is in production only while a tour of its own kind is unfinished. The game counts every
    // tour for the World Tour's record. Concerts and elections are left as the game set them.
    [HarmonyPatch(typeof(SpecialEvent), nameof(SpecialEvent.UpdateStatus))]
    public class SpecialEvents_Manager_specialEvent_UpdateStatus
    {
        public static void Postfix(SpecialEvent __instance)
        {
            bool national = NationalRecord.IsNationalRecord(__instance);
            if (!national && __instance.Type != EventType.WorldTour)
                return;
            __instance.Status = NationalRecord.AnyUnfinished(national) ? SpecialEvent._status.production : SpecialEvent._status.normal;
        }
    }
}
