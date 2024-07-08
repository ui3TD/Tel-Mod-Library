using HarmonyLib;
using UnityEngine;
using TMPro;
using static FastForward.FastForward;

namespace FastForward
{
    public class FastForward
    {
        public const string VARID = "FastForward_Multiplier";
        public const string DEFAULT_VAR = "5";
    }

    /// <summary>
    /// Patch class for the TimeControlButton's OnClick method to implement faster time acceleration.
    /// </summary>
    [HarmonyPatch(typeof(TimeControlButton), "OnClick")]
    public class TimeControlButton_OnClick
    {
        /// <summary>
        /// Prefix method to enhance the fast forward functionality when clicking the fast forward button twice.
        /// </summary>
        /// <param name="__instance">The instance of the TimeControlButton being clicked.</param>
        /// <returns>Boolean indicating whether the original method should be executed.</returns>
        public static bool Prefix(TimeControlButton __instance)
        {
            double mult = double.Parse(variables.Get(VARID) ?? DEFAULT_VAR);
            double speed = 200 * mult;

            if (__instance.Type != mainScript._time_state.fast || staticVars.timeState != mainScript._time_state.fast || staticVars.dateTimeAddMinutesPerSecond == speed)
                return true;

            staticVars.dateTimeAddMinutesPerSecond = speed;
            Camera.main.GetComponent<mainScript>().TimeControls_Fast.GetComponent<TextMeshProUGUI>().color = mainScript.gold32;

            return false;
        }

    }

    /// <summary>
    /// Patch class for the Controls' Update method to implement hotkey-based time acceleration.
    /// </summary>
    [HarmonyPatch(typeof(Controls), "Update")]
    public class Controls_Update
    {
        /// <summary>
        /// Postfix method to add hotkey functionality for speeding up time when pressing the '4' key.
        /// </summary>
        public static void Postfix()
        {
            if (mainScript.IsBlockingHotkeys())
                return;

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                double mult = double.Parse(variables.Get(VARID) ?? DEFAULT_VAR);
                double speed = 200 * mult;
                Camera.main.GetComponent<mainScript>().Time_SetState(mainScript._time_state.fast);
                staticVars.dateTimeAddMinutesPerSecond = speed;

                Camera.main.GetComponent<mainScript>().TimeControls_Fast.GetComponent<TextMeshProUGUI>().color = mainScript.gold32;
            }
        }
    }
}
