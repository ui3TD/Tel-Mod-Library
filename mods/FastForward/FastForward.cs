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
        public const string DEFAULT_VAR = "5";
        public const double BASE_FAST_SPEED = 200d;
        public const double MAX_MULTIPLIER = 50d;
        public const double EPSILON = 0.001d;

        // Stores the user's pre-popup super-fast speed so it can be restored after forced popup pause.
        private static double pendingAuditionRestoreSpeed = 0d;

        internal static double GetConfiguredMultiplier()
        {
            string raw = variables.Get(VARID);
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = DEFAULT_VAR;
            }

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double multiplier))
            {
                multiplier = 5d;
            }

            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            {
                multiplier = 5d;
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

        internal static void SuspendSuperFastForAuditionPopup()
        {
            // Only suspend custom speeds; vanilla fast (200) does not need this safety fallback.
            if (staticVars.timeState != mainScript._time_state.fast || staticVars.dateTimeAddMinutesPerSecond <= BASE_FAST_SPEED + EPSILON)
            {
                return;
            }

            if (pendingAuditionRestoreSpeed <= BASE_FAST_SPEED + EPSILON)
            {
                pendingAuditionRestoreSpeed = staticVars.dateTimeAddMinutesPerSecond;
            }

            // Drop to vanilla fast while popup initialization runs to avoid race conditions in recruitment UI.
            staticVars.dateTimeAddMinutesPerSecond = BASE_FAST_SPEED;
            SetFastButtonColor(mainScript.green32);
        }

        internal static void TryRestoreSuperFastAfterResume(mainScript main)
        {
            if (pendingAuditionRestoreSpeed <= BASE_FAST_SPEED + EPSILON)
            {
                return;
            }

            // Respect user state changes made while popup flow was active.
            if (staticVars.timeState != mainScript._time_state.fast)
            {
                pendingAuditionRestoreSpeed = 0d;
                return;
            }

            // Only restore when popup system resumes vanilla fast speed.
            if (staticVars.dateTimeAddMinutesPerSecond > BASE_FAST_SPEED + EPSILON)
            {
                pendingAuditionRestoreSpeed = 0d;
                return;
            }

            staticVars.dateTimeAddMinutesPerSecond = pendingAuditionRestoreSpeed;
            pendingAuditionRestoreSpeed = 0d;
            SetFastButtonColor(mainScript.gold32);
        }

        internal static void ClearPendingRestoreIfNotFast(mainScript._time_state state)
        {
            if (state != mainScript._time_state.fast)
            {
                pendingAuditionRestoreSpeed = 0d;
            }
        }
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
                ApplySuperFast(Camera.main != null ? Camera.main.GetComponent<mainScript>() : null);
            }
        }
    }

    /// <summary>
    /// Ensures custom super-fast speed does not interfere with audition popup initialization.
    /// </summary>
    [HarmonyPatch(typeof(PopupManager), "Open")]
    public class PopupManager_Open
    {
        /// <summary>
        /// Prefix method to temporarily downgrade custom super-fast speed when opening the audition popup.
        /// </summary>
        /// <param name="type">Popup type being opened.</param>
        public static void Prefix(PopupManager._type type)
        {
            if (type != PopupManager._type.audition)
            {
                return;
            }

            SuspendSuperFastForAuditionPopup();
        }
    }

    /// <summary>
    /// Restores previously suspended super-fast speed after popup-driven forced pause is released.
    /// </summary>
    [HarmonyPatch(typeof(mainScript), "Time_Resume")]
    public class mainScript_Time_Resume
    {
        /// <summary>
        /// Postfix method to restore the user's pre-popup custom speed.
        /// </summary>
        /// <param name="__instance">mainScript instance.</param>
        public static void Postfix(mainScript __instance)
        {
            TryRestoreSuperFastAfterResume(__instance);
        }
    }

    /// <summary>
    /// Clears pending speed restoration when user manually switches away from fast mode.
    /// </summary>
    [HarmonyPatch(typeof(mainScript), "Time_SetState")]
    public class mainScript_Time_SetState
    {
        /// <summary>
        /// Postfix method to cancel restore state if current mode is no longer fast.
        /// </summary>
        /// <param name="state">Requested time state.</param>
        public static void Postfix(mainScript._time_state state)
        {
            ClearPendingRestoreIfNotFast(state);
        }
    }
}
