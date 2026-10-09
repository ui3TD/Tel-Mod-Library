using HarmonyLib;
using UnityEngine;
using TMPro;
using System;
using System.Globalization;
using static FastForward.FastForward;

namespace FastForward
{
    public class FastForward
    {
        public const string VARID = "FastForward_Multiplier";
        // The speed with no setting or an unreadable one; the same as the Mod Menu slider's default
        public const double DEFAULT_MULTIPLIER = 5d;
        public const double BASE_FAST_SPEED = 200d;
        // A safety limit, not a menu option: the Mod Menu slider stops at 20x, and this caps any value the
        // menu didn't produce (a hand-edited save, an old setting). Each time tick adds speed/4 minutes;
        // above 28.8x a tick spans over a day and skips onNewDay/onNewWeek.
        public const double MAX_MULTIPLIER = 28d;
        public const double EPSILON = 0.001d;

        internal static double GetConfiguredMultiplier()
        {
            // Missing (no ModMenus), unreadable or not a number: the default
            if (!double.TryParse(variables.Get(VARID), NumberStyles.Float, CultureInfo.InvariantCulture, out double multiplier)
                || double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            {
                multiplier = DEFAULT_MULTIPLIER;
            }

            if (multiplier < 1d)
            {
                multiplier = 1d;
            }
            else if (multiplier > MAX_MULTIPLIER)
            {
                multiplier = MAX_MULTIPLIER;
            }

            return multiplier;
        }

        internal static double GetConfiguredSpeed()
        {
            return BASE_FAST_SPEED * GetConfiguredMultiplier();
        }

        internal static void ApplySuperFast(mainScript main)
        {
            if (main == null)
            {
                return;
            }

            // Use vanilla state transition first so all game-side effects still run as expected.
            main.Time_SetState(mainScript._time_state.fast);
            staticVars.dateTimeAddMinutesPerSecond = GetConfiguredSpeed();
            SetFastButtonColor(mainScript.gold32);
        }

        internal static void SetFastButtonColor(Color32 color)
        {
            if (Camera.main == null)
            {
                return;
            }

            mainScript main = Camera.main.GetComponent<mainScript>();
            if (main == null || main.TimeControls_Fast == null)
            {
                return;
            }

            TextMeshProUGUI fastLabel = main.TimeControls_Fast.GetComponent<TextMeshProUGUI>();
            if (fastLabel != null)
            {
                fastLabel.color = color;
            }
        }
    }

    /// <summary>
    /// Patch class for the TimeControlButton's OnClick method to implement faster time acceleration.
    /// </summary>
    [HarmonyPatch(typeof(TimeControlButton), nameof(TimeControlButton.OnClick))]
    public class TimeControlButton_OnClick
    {
        /// <summary>
        /// Prefix method to enhance the fast forward functionality when clicking the fast forward button twice.
        /// </summary>
        /// <param name="__instance">The instance of the TimeControlButton being clicked.</param>
        /// <returns>Boolean indicating whether the original method should be executed.</returns>
        public static bool Prefix(TimeControlButton __instance)
        {
            double speed = GetConfiguredSpeed();

            if (__instance.Type != mainScript._time_state.fast || staticVars.timeState != mainScript._time_state.fast || Math.Abs(staticVars.dateTimeAddMinutesPerSecond - speed) <= EPSILON)
                return true;

            ApplySuperFast(Camera.main != null ? Camera.main.GetComponent<mainScript>() : null);
            return false;
        }
    }

    /// <summary>
    /// Patch class for the Controls' Update method to implement hotkey-based time acceleration.
    /// </summary>
    [HarmonyPatch(typeof(Controls), nameof(Controls.Update))]
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
                ApplySuperFast(Camera.main != null ? Camera.main.GetComponent<mainScript>() : null);
            }
        }
    }
}
