using HarmonyLib;

namespace NeverGraduate
{
    // Hard stop for the normal weekly graduation pipeline.
    // Returning false here means the game never reaches Graduation_Set_Default_Date()
    // or Graduation_Date_Update() from UpdateGraduationDates(). That makes this mod
    // authoritative over graduation timing: Job Hopper and Worker Rights cannot move
    // an idol toward graduation while Never Graduate is loaded.
    [HarmonyPatch(typeof(data_girls), "UpdateGraduationDates")]
    public class data_girls_UpdateGraduationDates
    {
        public static bool Prefix()
        {
            return false;
        }
    }

    // Second lock for callers that bypass UpdateGraduationDates and invoke the per-idol
    // date check directly. If this mod is enabled on a save where an idol had already
    // announced graduation, restore her previous status before suppressing the check.
    [HarmonyPatch(typeof(data_girls.girls), "Graduation_Date_Update")]
    public static class data_girls_girls_Graduation_Date_Update
    {
        public static bool Prefix(data_girls.girls __instance)
        {
            if (__instance != null && __instance.status == data_girls._status.announced_graduation)
            {
                data_girls._status previousStatus = __instance.previous_status;

                // SetStatus normally refuses to leave announced_graduation, so unlock the
                // stored status first, then call SetStatus to fire the normal UI/update hook.
                __instance.status = previousStatus;
                __instance.SetStatus(previousStatus);
            }

            return false;
        }
    }

    // No automatic or mod-triggered announcement may put an idol onto a graduation countdown.
    [HarmonyPatch(typeof(data_girls.girls), "Graduation_Announce")]
    public static class data_girls_girls_Graduation_Announce
    {
        public static bool Prefix()
        {
            return false;
        }
    }

    // Some story/mod code calls the confirmation method directly, bypassing Graduation_Announce.
    // Block that route too so the no-graduation invariant cannot be sidestepped accidentally.
    [HarmonyPatch(typeof(data_girls.girls), "Graduation_Announce_Confirm")]
    public static class data_girls_girls_Graduation_Announce_Confirm
    {
        public static bool Prefix()
        {
            return false;
        }
    }
}
